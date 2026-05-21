package router

import (
	"net/http"
	"time"

	"labelpro-server/internal/config"
	"labelpro-server/internal/database"
	"labelpro-server/internal/handlers"
	"labelpro-server/internal/middleware"
	"labelpro-server/internal/repository"
	"labelpro-server/internal/services"
	"labelpro-server/internal/utils"
	"labelpro-server/internal/ws"

	"github.com/gin-gonic/gin"
)

func Setup(cfg *config.Config) *gin.Engine {
	if cfg.Server.Mode == "release" {
		gin.SetMode(gin.ReleaseMode)
	}

	r := gin.New()

	r.Use(middleware.RequestID())
	r.Use(middleware.Recovery())
	r.Use(middleware.SecurityHeaders())
	r.Use(middleware.CORS(cfg))
	r.Use(middleware.RequestLogger())
	r.Use(middleware.RateLimit(cfg))

	r.GET("/health", healthCheck)

	userRepo := repository.NewUserRepository(database.DB)
	deptRepo := repository.NewDepartmentRepository(database.DB)
	noteRepo := repository.NewNoteRepository(database.DB)
	tagRepo := repository.NewTagRepository(database.DB)
	tmplRepo := repository.NewTemplateRepository(database.DB)
	groupRepo := repository.NewWorkGroupRepository(database.DB)
	roomRepo := repository.NewCollaborationRoomRepository(database.DB)
	ledgerRepo := repository.NewLedgerRepository(database.DB)
	sysRepo := repository.NewSystemRepository(database.DB)
	presetRepo := repository.NewPresetGroupRepository(database.DB)
	middleware.SetOperationLogRepo(sysRepo)

	authService := services.NewAuthService(userRepo, cfg)
	userService := services.NewUserService(userRepo, deptRepo)
	noteService := services.NewNoteService(noteRepo)

	authHandler := handlers.NewAuthHandler(authService)
	userHandler := handlers.NewUserHandler(userService, groupRepo)
	deptHandler := handlers.NewDepartmentHandler(deptRepo)
	noteHandler := handlers.NewNoteHandler(noteService)
	tagHandler := handlers.NewTagHandler(tagRepo)
	tmplHandler := handlers.NewTemplateHandler(tmplRepo)
	groupHandler := handlers.NewWorkGroupHandler(groupRepo, noteRepo, userRepo, sysRepo, presetRepo)
	roomHandler := handlers.NewRoomHandler(roomRepo)
	ledgerHandler := handlers.NewLedgerHandler(ledgerRepo)
	sysHandler := handlers.NewSystemHandler(sysRepo)
	analyticsHandler := handlers.NewAnalyticsHandler(noteRepo, sysRepo)
	presetHandler := handlers.NewPresetGroupHandler(presetRepo)
	uploadHandler := handlers.NewUploadHandler()

	if cfg.WebSocket.Enabled {
		hub := ws.InitHub()
		r.GET("/ws/:note_id", ws.HandleWebSocket(hub))
		r.GET("/ws/group/:group_id", ws.HandleGroupWebSocket(hub))
	}

	r.Static("/uploads", "./uploads")

	api := r.Group("/api/v1")
	{
		api.GET("/ping", func(c *gin.Context) {
			utils.Success(c, gin.H{"ping": "pong"})
		})

		auth := api.Group("/auth")
		auth.Use(middleware.OperationLogger())
		{
			auth.POST("/login", authHandler.Login)
			auth.POST("/refresh", authHandler.RefreshToken)
			auth.POST("/logout", middleware.AuthMiddleware(cfg), authHandler.Logout)
			auth.GET("/me", middleware.AuthMiddleware(cfg), authHandler.GetCurrentUser)
		}

		api.Use(middleware.AuthMiddleware(cfg))
		api.Use(middleware.OperationLogger())

		departments := api.Group("/departments")
		{
			departments.GET("", deptHandler.GetTree)
			departments.GET("/:id", deptHandler.GetDetail)
			departments.POST("", middleware.RequireRoles("super_admin", "dept_admin"), deptHandler.Create)
			departments.PUT("/:id", middleware.RequireRoles("super_admin", "dept_admin"), deptHandler.Update)
			departments.DELETE("/:id", middleware.RequireRoles("super_admin"), deptHandler.Delete)
		}

		users := api.Group("/users")
		{
			users.GET("", userHandler.ListUsers)
			users.GET("/visible", userHandler.GetVisibleUsers)
			users.GET("/recommend", userHandler.RecommendUsers)
			users.GET("/work-type-options", userHandler.WorkTypeOptions)
			users.GET("/with-stats", userHandler.ListUsersWithStats)
			users.GET("/:id", userHandler.GetUser)
			users.GET("/:id/profile", userHandler.GetUserProfile)
			users.PUT("/:id", middleware.RequireRoles("super_admin", "dept_admin"), userHandler.UpdateUser)
			users.DELETE("/:id", middleware.RequireRoles("super_admin"), userHandler.DeleteUser)
			users.POST("", middleware.RequireRoles("super_admin", "dept_admin"), authHandler.Register)
		}

		notes := api.Group("/notes")
		{
			notes.GET("", noteHandler.ListNotes)
			notes.POST("", noteHandler.CreateNote)
			notes.GET("/export", noteHandler.ExportNotes)
			notes.GET("/:id", noteHandler.GetNote)
			notes.PUT("/:id", noteHandler.UpdateNote)
			notes.POST("/:id/complete", noteHandler.CompleteNote)
			notes.POST("/:id/remind", noteHandler.RemindNote)
			notes.DELETE("/:id", noteHandler.DeleteNote)
			notes.POST("/:id/restore", noteHandler.RestoreNote)
			notes.GET("/stats", noteHandler.Stats)
			notes.GET("/heatmap", noteHandler.Heatmap)
			notes.GET("/:id/export", noteHandler.ExportNote)
			notes.POST("/:id/attachments", uploadHandler.Upload)
			notes.GET("/:id/attachments", uploadHandler.ListAttachments)
			notes.DELETE("/:id/attachments/:attachmentId", uploadHandler.DeleteAttachment)
		}

		tags := api.Group("/tags")
		{
			tags.GET("", tagHandler.List)
			tags.POST("", tagHandler.Create)
			tags.PUT("/:id", tagHandler.Update)
			tags.DELETE("/:id", tagHandler.Delete)
		}

		templates := api.Group("/templates")
		{
			templates.GET("", tmplHandler.List)
			templates.GET("/:id", tmplHandler.Get)
			templates.POST("", tmplHandler.Create)
			templates.PUT("/:id", tmplHandler.Update)
			templates.DELETE("/:id", tmplHandler.Delete)
		}

		groups := api.Group("/groups")
		{
			groups.GET("", groupHandler.Search)
			groups.GET("/mine", groupHandler.MyGroups)
	groups.POST("/ai-suggest", groupHandler.AISuggestGroups)
	groups.POST("", groupHandler.Create)
			groups.GET("/:id", groupHandler.GetDetail)
			groups.DELETE("/:id", groupHandler.Delete)
			groups.GET("/:id/members", groupHandler.GetMembers)
			groups.POST("/:id/members", groupHandler.AddMember)
			groups.PUT("/:id/members/:user_id", groupHandler.UpdateMember)
			groups.DELETE("/:id/members/:user_id", groupHandler.RemoveMember)
			groups.GET("/:id/notes", groupHandler.GetGroupNotes)
			groups.POST("/:id/notes", groupHandler.CreateGroupNote)
			groups.GET("/:id/dashboard", groupHandler.GetDashboard)
			groups.POST("/:id/reports", groupHandler.GenerateReport)
			groups.GET("/:id/reports", groupHandler.ListReports)
			groups.GET("/:id/reports/:reportId", groupHandler.GetReport)
			groups.DELETE("/:id/reports/:reportId", groupHandler.DeleteReport)
			groups.GET("/:id/reports/:reportId/export", groupHandler.ExportReport)
		}

		rooms := api.Group("/rooms")
		{
			rooms.GET("/:note_id/canvas", roomHandler.GetCanvas)
			rooms.POST("/:note_id/command", middleware.RequireRoles("super_admin", "dept_admin", "group_leader"), roomHandler.SendCommand)
		}

		presets := api.Group("/presets")
		{
			presets.GET("", presetHandler.List)
			presets.POST("", middleware.RequireRoles("super_admin", "dept_admin"), presetHandler.Create)
			presets.PUT("/:id", middleware.RequireRoles("super_admin", "dept_admin"), presetHandler.Update)
			presets.DELETE("/:id", middleware.RequireRoles("super_admin"), presetHandler.Delete)
		}

		ledger := api.Group("/ledger")
		{
			ledger.GET("", ledgerHandler.List)
			ledger.GET("/stats", middleware.RequireRoles("super_admin", "dept_admin"), ledgerHandler.Stats)
		}

		analytics := api.Group("/analytics")
		{
			analytics.GET("/personal-stats", analyticsHandler.PersonalStats)
			analytics.POST("/ai-report", analyticsHandler.GenerateAIReport)
			analytics.GET("/reports", analyticsHandler.ListReports)
			analytics.GET("/reports/:id", analyticsHandler.GetReport)
			analytics.DELETE("/reports/:id", analyticsHandler.DeleteReport)
			analytics.GET("/report-template", analyticsHandler.GetReportTemplate)
			analytics.PUT("/report-template", analyticsHandler.SaveReportTemplate)
			analytics.POST("/daily-report", analyticsHandler.GenerateDailyReport)
			analytics.POST("/weekly-report", analyticsHandler.GenerateWeeklyReport)
			analytics.POST("/monthly-report", analyticsHandler.GenerateMonthlyReport)
		}

		system := api.Group("/system")
		system.Use(middleware.RequireRoles("super_admin"))
		{
			system.GET("/config", sysHandler.GetConfig)
			system.PUT("/config", sysHandler.UpdateConfig)
			system.GET("/ai-configs", sysHandler.ListAIConfigs)
			system.POST("/ai-configs", sysHandler.CreateAIConfig)
			system.PUT("/ai-configs/:id", sysHandler.UpdateAIConfig)
			system.DELETE("/ai-configs/:id", sysHandler.DeleteAIConfig)
			system.GET("/config-files", sysHandler.ListConfigFiles)
			system.GET("/config-files/:name", sysHandler.GetConfigFile)
			system.PUT("/config-files/:name", sysHandler.UpdateConfigFile)
			system.GET("/config-files/:name/history", sysHandler.GetConfigFileHistory)
			system.GET("/logs", sysHandler.ListAdminLogs)
			system.GET("/operations", sysHandler.ListOperations)
			system.GET("/operations/actions", sysHandler.GetOperationActions)
		}
	}

	r.NoRoute(authHandler.NoRoute)

	return r
}

func healthCheck(c *gin.Context) {
	c.JSON(http.StatusOK, gin.H{
		"status":    "ok",
		"timestamp": time.Now().Unix(),
		"version":   "1.0.0",
	})
}
