package main

import (
	"context"
	"fmt"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"labelpro-server/internal/config"
	"labelpro-server/internal/database"
	"labelpro-server/internal/logger"
	"labelpro-server/internal/models"
	"labelpro-server/internal/router"
	"labelpro-server/internal/utils"

	"go.uber.org/zap"
)

func main() {
	cfg, err := config.Load("config.json")
	if err != nil {
		fmt.Fprintf(os.Stderr, "Failed to load config: %v\n", err)
		os.Exit(1)
	}
	config.SetActive(cfg, "config.json")

	if err := logger.Init(
		cfg.Log.Level,
		cfg.Log.Format,
		cfg.Log.OutputDir,
		cfg.Log.MaxSizeMB,
		cfg.Log.MaxBackups,
		cfg.Log.MaxAgeDays,
		cfg.Log.Compress,
		cfg.Log.EnableConsole,
	); err != nil {
		fmt.Fprintf(os.Stderr, "Failed to initialize logger: %v\n", err)
		os.Exit(1)
	}
	defer logger.Sync()

	logger.Info("Starting LabelPro Server...",
		zap.String("version", "1.0.0"),
		zap.String("mode", cfg.Server.Mode),
	)

	if err := database.InitPostgres(cfg); err != nil {
		logger.Fatal("Failed to connect to PostgreSQL", zap.Error(err))
	}
	defer database.ClosePostgres()

	if tx := database.DB.Exec("CREATE EXTENSION IF NOT EXISTS pgcrypto"); tx.Error != nil {
		logger.Fatal("Failed to create pgcrypto extension (required for gen_random_uuid on PostgreSQL < 13)", zap.Error(tx.Error))
	}
	if err := database.DB.AutoMigrate(
		&models.User{},
		&models.Department{},
		&models.RolePermission{},
		&models.Note{},
		&models.Tag{},
		&models.Template{},
		&models.NoteAssignee{},
		&models.NoteAttachment{},
		&models.WorkGroup{},
		&models.WorkGroupMember{},
		&models.PresetGroup{},
		&models.PresetGroupMember{},
		&models.CollaborationRoom{},
		&models.Reminder{},
		&models.LedgerEntry{},
		&models.AIConfig{},
		&models.ConfigFileHistory{},
		&models.AdminLog{},
		&models.OperationLog{},
		&models.WorkReport{},
		&models.ReportTemplate{},
	); err != nil {
		logger.Fatal("Failed to auto migrate database", zap.Error(err))
	}
	logger.Info("Database migration completed")

	database.DB.FirstOrCreate(&models.ReportTemplate{
		ID:   "default",
		Name: "默认报告模板",
	}, &models.ReportTemplate{
		Content: `## 工作概览

{{userName}}（{{periodLabel}}）共创建任务 **{{totalCreated}}** 条，完成 **{{totalCompleted}}** 条，完成率为 **{{completionRate}}%**。{{completionDesc}}。{{remindDesc}}。

## 数据分析

- **创建任务总数**：{{totalCreated}} 条，反映了{{userName}}的工作投入量
- **完成任务数**：{{totalCompleted}} 条，体现了任务执行效率
- **完成率**：{{completionRate}}%，{{completionDesc}}
- **被盯办次数**：{{remindReceived}} 次
- **平均完成耗时**：{{avgCompletionHours}} 小时

## 标签使用分布

{{tagList}}
## 每日任务趋势

{{dailyTrend}}
## 成果亮点

基于以上数据，{{periodLabel}}期间的工作展现出以下亮点：

- 保持了任务创建的持续性和稳定性
- 在重点关注领域有明确的工作投入
{{activeTagMsg}}

## 改进建议

1. 继续保持任务推进的节奏，关注高优先级事项
2. 合理分配工作时间，避免任务积压
3. 善用标签分类，提高工作梳理效率
4. 定期回顾工作成效，及时调整工作策略

---
*本报告由系统自动生成，基于实际工作数据统计分析*`,
	})

	seedDefaultAdmin(cfg)

	if err := database.InitRedis(cfg); err != nil {
		logger.Warn("Failed to connect to Redis, continuing without cache", zap.Error(err))
	}
	defer database.CloseRedis()

	if err := utils.InitJWT(&cfg.JWT); err != nil {
		logger.Warn("Failed to initialize JWT, generating temporary keys", zap.Error(err))
	}

	engine := router.Setup(cfg)

	srv := &http.Server{
		Addr:         cfg.ServerAddr(),
		Handler:      engine,
		ReadTimeout:  time.Duration(cfg.Server.ReadTimeoutSeconds) * time.Second,
		WriteTimeout: time.Duration(cfg.Server.WriteTimeoutSeconds) * time.Second,
	}

	go func() {
		logger.Info("Server listening", zap.String("addr", cfg.ServerAddr()))
		if err := srv.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			logger.Fatal("Failed to start server", zap.Error(err))
		}
	}()

	quit := make(chan os.Signal, 1)
	signal.Notify(quit, syscall.SIGINT, syscall.SIGTERM)
	<-quit

	logger.Info("Shutting down server...")

	ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
	defer cancel()

	if err := srv.Shutdown(ctx); err != nil {
		logger.Error("Server forced to shutdown", zap.Error(err))
	}

	logger.Info("Server exited")
}

func seedDefaultAdmin(cfg *config.Config) {
	var count int64
	if err := database.DB.Model(&models.User{}).Count(&count).Error; err != nil {
		logger.Warn("Failed to check user count, skipping default admin creation", zap.Error(err))
		return
	}
	if count > 0 {
		logger.Info("Database already has users, skipping default admin creation")
		return
	}

	hash, err := utils.HashPassword("admin")
	if err != nil {
		logger.Error("Failed to hash default admin password", zap.Error(err))
		return
	}

	admin := models.User{
		Username:     "admin",
		Name:         "管理员",
		Role:         "super_admin",
		PasswordHash: hash,
		IsActive:     true,
	}
	if err := database.DB.Create(&admin).Error; err != nil {
		logger.Error("Failed to create default admin user", zap.Error(err))
		return
	}

	logger.Info("Default admin user created", zap.String("username", "admin"))
}
