package main

import (
	"fmt"
	"log"
	"math/rand"
	"time"

	"labelpro-server/internal/config"
	"labelpro-server/internal/database"
	"labelpro-server/internal/logger"
	"labelpro-server/internal/models"
	"labelpro-server/internal/utils"

	"github.com/google/uuid"
)

var rng = rand.New(rand.NewSource(time.Now().UnixNano()))

type seedStats struct {
	Permissions      int
	Departments      int
	Users            int
	Tags             int
	Templates        int
	WorkGroups       int
	WorkGroupMembers int
	PresetGroups     int
	PresetMembers    int
	Notes            int
	NoteAssignees    int
	NoteAttachments  int
	Reminders        int
	CollabRooms      int
	WorkReports      int
	LedgerEntries    int
	OperationLogs    int
	AIConfig         int
	ReportTemplates  int
}

func (s seedStats) Total() int {
	return s.Permissions + s.Departments + s.Users + s.Tags + s.Templates +
		s.WorkGroups + s.WorkGroupMembers + s.PresetGroups + s.PresetMembers +
		s.Notes + s.NoteAssignees + s.NoteAttachments + s.Reminders + s.CollabRooms +
		s.WorkReports + s.LedgerEntries + s.OperationLogs + s.AIConfig + s.ReportTemplates
}

func main() {
	cfg, err := config.Load("config.json")
	if err != nil {
		log.Fatalf("加载配置失败: %v", err)
	}

	if err := logger.Init(
		cfg.Log.Level, cfg.Log.Format, cfg.Log.OutputDir,
		cfg.Log.MaxSizeMB, cfg.Log.MaxBackups, cfg.Log.MaxAgeDays, cfg.Log.Compress, cfg.Log.EnableConsole,
	); err != nil {
		log.Fatalf("初始化日志失败: %v", err)
	}
	defer logger.Sync()

	if err := database.InitPostgres(cfg); err != nil {
		log.Fatalf("连接数据库失败: %v", err)
	}

	if err := database.DB.AutoMigrate(
		&models.User{}, &models.Department{}, &models.RolePermission{},
		&models.Note{}, &models.Tag{}, &models.Template{},
		&models.NoteAssignee{}, &models.NoteAttachment{},
		&models.WorkGroup{}, &models.WorkGroupMember{},
		&models.PresetGroup{}, &models.PresetGroupMember{},
		&models.CollaborationRoom{}, &models.Reminder{},
		&models.LedgerEntry{}, &models.AIConfig{},
		&models.ConfigFileHistory{}, &models.AdminLog{},
		&models.OperationLog{}, &models.WorkReport{}, &models.ReportTemplate{},
	); err != nil {
		log.Fatalf("数据库迁移失败: %v", err)
	}
	fmt.Println("数据库迁移完成")

	database.DB.Where("id = ?", "default").FirstOrCreate(&models.ReportTemplate{
		ID: "default", Name: "默认报告模板",
	}, &models.ReportTemplate{Content: `## 工作概览
{{userName}}（{{periodLabel}}）共创建任务 **{{totalCreated}}** 条，完成 **{{totalCompleted}}** 条，完成率为 **{{completionRate}}%**。
## 数据分析
- **创建任务总数**：{{totalCreated}} 条
- **完成任务数**：{{totalCompleted}} 条
- **完成率**：{{completionRate}}%
- **被盯办次数**：{{remindReceived}} 次
- **平均完成耗时**：{{avgCompletionHours}} 小时
## 标签使用分布
{{tagList}}
## 每日任务趋势
{{dailyTrend}}
## 改进建议
1. 继续保持任务推进的节奏，关注高优先级事项
2. 合理分配工作时间，避免任务积压
3. 善用标签分类，提高工作梳理效率
---
*本报告由系统自动生成*`,
	})

	stats := seedAll()
	fmt.Printf("\n========================================\n")
	fmt.Printf("  测试数据初始化完成！\n")
	fmt.Printf("  总记录数: %d 条\n", stats.Total())
	fmt.Printf("========================================\n")
	fmt.Printf("  - 权限矩阵: %d 条\n", stats.Permissions)
	fmt.Printf("  - 部门: %d 个\n", stats.Departments)
	fmt.Printf("  - 用户: %d 个\n", stats.Users)
	fmt.Printf("  - 标签: %d 个\n", stats.Tags)
	fmt.Printf("  - 模板: %d 个\n", stats.Templates)
	fmt.Printf("  - 专项工作组: %d 个 (成员 %d 条)\n", stats.WorkGroups, stats.WorkGroupMembers)
	fmt.Printf("  - 预设组: %d 个 (成员 %d 条)\n", stats.PresetGroups, stats.PresetMembers)
	fmt.Printf("  - 任务: %d 条 (指派 %d 条, 附件 %d 条)\n", stats.Notes, stats.NoteAssignees, stats.NoteAttachments)
	fmt.Printf("  - 盯办提醒: %d 条\n", stats.Reminders)
	fmt.Printf("  - 协同房间: %d 个\n", stats.CollabRooms)
	fmt.Printf("  - 工作报告: %d 条\n", stats.WorkReports)
	fmt.Printf("  - 台账: %d 条\n", stats.LedgerEntries)
	fmt.Printf("  - 操作日志: %d 条\n", stats.OperationLogs)
	fmt.Printf("========================================\n")
	fmt.Println("默认密码均为: Admin@123")
}

func seedAll() seedStats {
	stats := seedStats{}
	stats.Permissions = seedPermissions()
	stats.Departments = seedDepartments()
	stats.Users = seedUsers()
	stats.Tags = seedTags()
	stats.Templates = seedTemplates()
	stats.ReportTemplates = seedReportTemplates()
	stats.AIConfig = seedAIConfig()
	stats.WorkGroups, stats.WorkGroupMembers = seedWorkGroups()
	stats.PresetGroups, stats.PresetMembers = seedPresetGroups()
	stats.Notes, stats.NoteAssignees, stats.NoteAttachments = seedNotes()
	stats.Reminders = seedReminders()
	stats.CollabRooms = seedCollaborationRooms()
	stats.WorkReports = seedWorkReports()
	stats.LedgerEntries = seedLedgerEntries()
	stats.OperationLogs = seedOperationLogs()
	return stats
}

func pick[T any](slice []T) T {
	if len(slice) == 0 {
		var zero T
		return zero
	}
	return slice[rng.Intn(len(slice))]
}

func pickN[T any](slice []T, n int) []T {
	if n >= len(slice) {
		return slice
	}
	rng.Shuffle(len(slice), func(i, j int) { slice[i], slice[j] = slice[j], slice[i] })
	return slice[:n]
}

func randBetween(min, max int) int {
	return rng.Intn(max-min+1) + min
}

func randTime(daysBack int) time.Time {
	offset := time.Duration(rng.Intn(daysBack*24)) * time.Hour
	offset += time.Duration(rng.Intn(3600)) * time.Second
	return time.Now().Add(-offset)
}

func randDueTime() *time.Time {
	variants := []int{-48, -24, -12, -6, -4, -2, 0, 2, 4, 6, 8, 12, 24, 48, 72, 120, 168}
	offset := time.Duration(pick(variants)) * time.Hour
	offset += time.Duration(rng.Intn(3600)) * time.Second
	t := time.Now().Add(offset)
	return &t
}

func ptrTime(t time.Time) *time.Time { return &t }

func randomBool(prob float64) bool {
	return rng.Float64() < prob
}

func seedPermissions() int {
	var count int64
	database.DB.Model(&models.RolePermission{}).Count(&count)
	if count > 0 {
		fmt.Println("权限数据已存在，跳过 (共" + fmt.Sprint(count) + "条)")
		return 0
	}
	perms := []models.RolePermission{
		{Role: "super_admin", Resource: "note", Action: "create", Scope: "global"},
		{Role: "super_admin", Resource: "note", Action: "read", Scope: "global"},
		{Role: "super_admin", Resource: "note", Action: "update", Scope: "global"},
		{Role: "super_admin", Resource: "note", Action: "delete", Scope: "global"},
		{Role: "super_admin", Resource: "note", Action: "remind", Scope: "global"},
		{Role: "super_admin", Resource: "user", Action: "create", Scope: "global"},
		{Role: "super_admin", Resource: "user", Action: "read", Scope: "global"},
		{Role: "super_admin", Resource: "user", Action: "update", Scope: "global"},
		{Role: "super_admin", Resource: "user", Action: "delete", Scope: "global"},
		{Role: "super_admin", Resource: "department", Action: "manage", Scope: "global"},
		{Role: "super_admin", Resource: "tag", Action: "manage", Scope: "global"},
		{Role: "super_admin", Resource: "template", Action: "manage", Scope: "global"},
		{Role: "dept_admin", Resource: "note", Action: "create", Scope: "department"},
		{Role: "dept_admin", Resource: "note", Action: "read", Scope: "department"},
		{Role: "dept_admin", Resource: "note", Action: "update", Scope: "department"},
		{Role: "dept_admin", Resource: "note", Action: "remind", Scope: "department"},
		{Role: "group_leader", Resource: "note", Action: "create", Scope: "group"},
		{Role: "group_leader", Resource: "note", Action: "read", Scope: "group"},
		{Role: "group_leader", Resource: "note", Action: "update", Scope: "group"},
		{Role: "group_leader", Resource: "note", Action: "remind", Scope: "group"},
		{Role: "member", Resource: "note", Action: "create", Scope: "self"},
		{Role: "member", Resource: "note", Action: "read", Scope: "self"},
	}
	database.DB.Create(&perms)
	fmt.Printf("  ✓ 权限矩阵: %d 条\n", len(perms))
	return len(perms)
}

func seedDepartments() int {
	var count int64
	database.DB.Model(&models.Department{}).Count(&count)
	if count > 0 {
		fmt.Println("部门数据已存在，跳过 (共" + fmt.Sprint(count) + "个)")
		return 0
	}
	gaID := uuid.New(); xjID := uuid.New(); zaID := uuid.New(); waID := uuid.New()
	jwID := uuid.New(); jbID := uuid.New(); xfID := uuid.New(); dbID := uuid.New()
	sgID := uuid.New(); bgID := uuid.New(); jkID := uuid.New()
	jszdID := uuid.New(); qbzdID := uuid.New(); xszdID := uuid.New()
	jzzdID := uuid.New(); yjzdID := uuid.New(); zzzdID := uuid.New()
	wjzdID := uuid.New(); xxzdID := uuid.New(); zhzdID := uuid.New()
	depts := []models.Department{
		{ID: gaID, Name: "市公安局", ParentID: nil, Level: 1, SortOrder: 1},
		{ID: xjID, Name: "刑警支队", ParentID: &gaID, Level: 2, SortOrder: 1},
		{ID: zaID, Name: "治安支队", ParentID: &gaID, Level: 2, SortOrder: 2},
		{ID: waID, Name: "网安支队", ParentID: &gaID, Level: 2, SortOrder: 3},
		{ID: jwID, Name: "经侦支队", ParentID: &gaID, Level: 2, SortOrder: 4},
		{ID: jbID, Name: "禁毒支队", ParentID: &gaID, Level: 2, SortOrder: 5},
		{ID: xfID, Name: "巡（特）警支队", ParentID: &gaID, Level: 2, SortOrder: 6},
		{ID: dbID, Name: "情报指挥中心", ParentID: &gaID, Level: 2, SortOrder: 7},
		{ID: sgID, Name: "科技信息化支队", ParentID: &gaID, Level: 2, SortOrder: 8},
		{ID: jkID, Name: "监所管理支队", ParentID: &gaID, Level: 2, SortOrder: 9},
		{ID: bgID, Name: "办公室", ParentID: &gaID, Level: 2, SortOrder: 10},
		{ID: jszdID, Name: "技术侦查中队", ParentID: &xjID, Level: 3, SortOrder: 1},
		{ID: qbzdID, Name: "情报研判中队", ParentID: &xjID, Level: 3, SortOrder: 2},
		{ID: xszdID, Name: "刑事科学技术中队", ParentID: &xjID, Level: 3, SortOrder: 3},
		{ID: jzzdID, Name: "基层基础中队", ParentID: &zaID, Level: 3, SortOrder: 1},
		{ID: yjzdID, Name: "应急管理大队", ParentID: &zaID, Level: 3, SortOrder: 2},
		{ID: zzzdID, Name: "作战指挥中队", ParentID: &waID, Level: 3, SortOrder: 1},
		{ID: wjzdID, Name: "网络监控中队", ParentID: &waID, Level: 3, SortOrder: 2},
		{ID: xxzdID, Name: "信息研判中队", ParentID: &dbID, Level: 3, SortOrder: 1},
		{ID: zhzdID, Name: "综合保障中队", ParentID: &bgID, Level: 3, SortOrder: 1},
	}
	database.DB.Create(&depts)
	fmt.Printf("  ✓ 部门: %d 个 (3级树形结构)\n", len(depts))
	return len(depts)
}

type userSeed struct {
	Username string
	Name     string
	DeptName string
	Role     string
	Rank     string
	Position string
	Skills   string
	Phone    string
	Email    string
}

func getUsers() []userSeed {
	return []userSeed{
		{Username: "admin", Name: "张振国", DeptName: "市公安局", Role: "super_admin", Rank: "一级警监", Position: "局长", Skills: "统筹指挥,决策分析,危机管理,跨部门协调", Phone: "13800001001", Email: "admin@police.cn"},
		{Username: "wang", Name: "王强", DeptName: "刑警支队", Role: "dept_admin", Rank: "二级警督", Position: "支队长", Skills: "刑侦指挥,案件分析,审讯突破,行动部署", Phone: "13800001002", Email: "wangqiang@police.cn"},
		{Username: "li", Name: "李志勇", DeptName: "技术侦查中队", Role: "group_leader", Rank: "三级警督", Position: "中队长", Skills: "技术侦查,电子取证,轨迹分析,图侦研判", Phone: "13800001003", Email: "lizhiyong@police.cn"},
		{Username: "zhang3", Name: "张伟", DeptName: "技术侦查中队", Role: "member", Rank: "一级警员", Position: "技术侦查员", Skills: "指纹比对,DNA鉴定,现场勘查,物证分析", Phone: "13800001004", Email: "zhangwei@police.cn"},
		{Username: "zhao", Name: "赵敏", DeptName: "情报研判中队", Role: "member", Rank: "二级警员", Position: "情报分析员", Skills: "数据分析,情报研判,犯罪画像,关联图谱", Phone: "13800001005", Email: "zhaomin@police.cn"},
		{Username: "sun", Name: "孙建国", DeptName: "治安支队", Role: "dept_admin", Rank: "二级警督", Position: "支队长", Skills: "治安管理,行业监管,大型活动安保,应急处突", Phone: "13800001006", Email: "sunjg@police.cn"},
		{Username: "zhou", Name: "周文博", DeptName: "应急管理大队", Role: "member", Rank: "一级警员", Position: "应急协调员", Skills: "应急响应,预案制定,跨单位协调,后勤保障", Phone: "13800001007", Email: "zhouwb@police.cn"},
		{Username: "wu", Name: "吴海龙", DeptName: "网安支队", Role: "dept_admin", Rank: "二级警督", Position: "支队长", Skills: "网络安全,渗透测试,网络取证,态势感知", Phone: "13800001008", Email: "wuhl@police.cn"},
		{Username: "chen", Name: "陈雪梅", DeptName: "网络监控中队", Role: "member", Rank: "一级警员", Position: "网络监控员", Skills: "舆情监控,信息检索,数据挖掘,网络巡查", Phone: "13800001009", Email: "chenxm@police.cn"},
		{Username: "liu", Name: "刘志强", DeptName: "经侦支队", Role: "dept_admin", Rank: "三级警督", Position: "支队长", Skills: "经济犯罪侦查,资金追踪,会计审计,数据建模", Phone: "13800001010", Email: "liuzq@police.cn"},
		{Username: "huang", Name: "黄丽", DeptName: "经侦支队", Role: "member", Rank: "二级警员", Position: "经侦民警", Skills: "账目核查,税务分析,金融监管,洗钱追踪", Phone: "13800001011", Email: "huangli@police.cn"},
		{Username: "yang", Name: "杨刚", DeptName: "情报指挥中心", Role: "dept_admin", Rank: "二级警督", Position: "主任", Skills: "指挥调度,情报汇总,应急决策,综合研判", Phone: "13800001012", Email: "yanggang@police.cn"},
		{Username: "ma", Name: "马晓燕", DeptName: "信息研判中队", Role: "group_leader", Rank: "三级警督", Position: "中队长", Skills: "大数据分析,可视化研判,趋势预测,风险评估", Phone: "13800001013", Email: "maxy@police.cn"},
		{Username: "xu", Name: "徐磊", DeptName: "信息研判中队", Role: "member", Rank: "一级警员", Position: "数据分析师", Skills: "SQL,Python,数据清洗,BI可视化", Phone: "13800001014", Email: "xulei@police.cn"},
		{Username: "he", Name: "何建华", DeptName: "禁毒支队", Role: "dept_admin", Rank: "三级警督", Position: "支队长", Skills: "毒品查缉,卧底侦查,化装侦查,禁毒宣教", Phone: "13800001015", Email: "hejh@police.cn"},
		{Username: "lin", Name: "林志明", DeptName: "禁毒支队", Role: "member", Rank: "二级警员", Position: "禁毒民警", Skills: "毒品检验,涉毒情报,跟踪监视,抓捕战术", Phone: "13800001016", Email: "linzm@police.cn"},
		{Username: "tang", Name: "唐辉", DeptName: "巡（特）警支队", Role: "dept_admin", Rank: "二级警督", Position: "支队长", Skills: "反恐处突,特种战术,武器操作,防暴处置", Phone: "13800001017", Email: "tanghui@police.cn"},
		{Username: "cao", Name: "曹鑫", DeptName: "巡（特）警支队", Role: "member", Rank: "一级警员", Position: "特警队员", Skills: "狙击,突击,绳索技术,急救医疗", Phone: "13800001018", Email: "caoxin@police.cn"},
		{Username: "deng", Name: "邓敏", DeptName: "科技信息化支队", Role: "dept_admin", Rank: "三级警督", Position: "支队长", Skills: "系统架构,信息安全,项目管理,云计算", Phone: "13800001019", Email: "dengmin@police.cn"},
		{Username: "peng", Name: "彭涛", DeptName: "科技信息化支队", Role: "member", Rank: "一级警员", Position: "技术保障员", Skills: "网络运维,数据库管理,前端开发,系统集成", Phone: "13800001020", Email: "pengtao@police.cn"},
		{Username: "shen", Name: "沈丽华", DeptName: "刑事科学技术中队", Role: "member", Rank: "一级警员", Position: "法医", Skills: "法医病理,法医临床,DNA鉴定,毒物分析", Phone: "13800001021", Email: "shenlh@police.cn"},
		{Username: "yu", Name: "余伟", DeptName: "作战指挥中队", Role: "member", Rank: "一级警员", Position: "网安民警", Skills: "Web安全,漏洞挖掘,恶意代码分析,安全评估", Phone: "13800001022", Email: "yuwei@police.cn"},
		{Username: "feng", Name: "冯佳", DeptName: "综合保障中队", Role: "member", Rank: "二级警员", Position: "综合文秘", Skills: "公文写作,档案管理,会务组织,统计报表", Phone: "13800001023", Email: "fengjia@police.cn"},
		{Username: "jiang", Name: "蒋文斌", DeptName: "基层基础中队", Role: "group_leader", Rank: "三级警督", Position: "中队长", Skills: "社区警务,矛盾调解,巡逻防控,行业场所管理", Phone: "13800001024", Email: "jiangwb@police.cn"},
		{Username: "luo", Name: "罗明", DeptName: "基层基础中队", Role: "member", Rank: "一级警员", Position: "社区民警", Skills: "入户走访,信息采集,纠纷调解,治安巡逻", Phone: "13800001025", Email: "luoming@police.cn"},
	}
}

func seedUsers() int {
	var count int64
	database.DB.Model(&models.User{}).Count(&count)
	if count > 0 {
		fmt.Println("用户数据已存在，跳过 (共" + fmt.Sprint(count) + "个)")
		return 0
	}

	var depts []models.Department
	database.DB.Find(&depts)
	deptMap := make(map[string]uuid.UUID)
	for _, d := range depts {
		deptMap[d.Name] = d.ID
	}

	hashedPwd, _ := utils.HashPassword("Admin@123")
	users := getUsers()

	created := 0
	var createdUsers []models.User
	for _, u := range users {
		userID := uuid.New()
		deptID, ok := deptMap[u.DeptName]
		if !ok {
			continue
		}
		user := models.User{
			ID:           userID,
			Username:     u.Username,
			PasswordHash: hashedPwd,
			Name:         u.Name,
			Email:        u.Email,
			Phone:        u.Phone,
			Role:         u.Role,
			Rank:         u.Rank,
			Position:     u.Position,
			Skills:       u.Skills,
			DepartmentID: &deptID,
			IsActive:     true,
			CreatedAt: time.Now().Add(-time.Duration(randBetween(30, 365)) * 24 * time.Hour),
			UpdatedAt: time.Now(),
		}
		database.DB.Create(&user)
		createdUsers = append(createdUsers, user)
		created++
	}
	fmt.Printf("  ✓ 用户: %d 个\n", created)
	return created
}

func seedTags() int {
	var count int64
	database.DB.Model(&models.Tag{}).Count(&count)
	if count > 0 {
		fmt.Println("标签数据已存在，跳过 (共" + fmt.Sprint(count) + "个)")
		return 0
	}
	colors := []string{"#EF4444", "#F59E0B", "#10B981", "#3B82F6", "#8B5CF6", "#EC4899",
		"#F97316", "#14B8A6", "#6366F1", "#84CC16", "#06B6D4", "#D946EF", "#0EA5E9", "#E11D48", "#22C55E"}

	tags := []models.Tag{
		{Name: "刑事案件", SubTag: "重案要案", Color: "#EF4444", Category: "案件", Scope: "system", SortOrder: 1},
		{Name: "治安案件", SubTag: "日常巡查", Color: "#F59E0B", Category: "治安", Scope: "system", SortOrder: 2},
		{Name: "情报研判", SubTag: "数据分析", Color: "#3B82F6", Category: "情报", Scope: "system", SortOrder: 3},
		{Name: "网络犯罪", SubTag: "反诈", Color: "#8B5CF6", Category: "网安", Scope: "system", SortOrder: 4},
		{Name: "经济犯罪", SubTag: "洗钱", Color: "#10B981", Category: "经侦", Scope: "system", SortOrder: 5},
		{Name: "毒品案件", SubTag: "禁毒", Color: "#EC4899", Category: "禁毒", Scope: "system", SortOrder: 6},
		{Name: "应急处突", SubTag: "突发事件", Color: "#F97316", Category: "应急", Scope: "system", SortOrder: 7},
		{Name: "专项行动", SubTag: "集中整治", Color: "#14B8A6", Category: "行政", Scope: "system", SortOrder: 8},
		{Name: "舆情监控", SubTag: "网络巡查", Color: "#6366F1", Category: "网安", Scope: "system", SortOrder: 9},
		{Name: "证据管理", SubTag: "物证", Color: "#84CC16", Category: "刑侦", Scope: "system", SortOrder: 10},
		{Name: "法制审核", SubTag: "案件审核", Color: "#0EA5E9", Category: "法制", Scope: "system", SortOrder: 11},
		{Name: "安保任务", SubTag: "大型活动", Color: "#D946EF", Category: "治安", Scope: "system", SortOrder: 12},
		{Name: "技术保障", SubTag: "系统运维", Color: "#22C55E", Category: "科技", Scope: "system", SortOrder: 13},
		{Name: "宣传教育", SubTag: "反诈宣传", Color: "#06B6D4", Category: "宣传", Scope: "system", SortOrder: 14},
		{Name: "综合事务", SubTag: "行政管理", Color: "#E11D48", Category: "行政", Scope: "system", SortOrder: 15},
	}
	for i := range tags {
		tags[i].Color = colors[i%len(colors)]
	}
	database.DB.Create(&tags)
	fmt.Printf("  ✓ 标签: %d 个\n", len(tags))
	return len(tags)
}

func seedTemplates() int {
	var count int64
	database.DB.Model(&models.Template{}).Count(&count)
	if count > 0 {
		fmt.Println("模板数据已存在，跳过 (共" + fmt.Sprint(count) + "个)")
		return 0
	}
	templates := []models.Template{
		{Name: "默认", Type: "default", Fields: `[{"key":"title","label":"标题","type":"text","required":true},{"key":"content","label":"内容","type":"richtext","required":true},{"key":"tags","label":"标签","type":"tags"}]`, Layout: "1", IsSystem: true},
		{Name: "数据分析报告", Type: "data_analysis", Fields: `[{"key":"title","label":"报告标题","type":"text","required":true},{"key":"data_source","label":"数据来源","type":"text","required":true},{"key":"analysis_method","label":"分析方法","type":"select","options":["统计分析","关联分析","时空分析","画像分析","趋势预测"]},{"key":"findings","label":"分析发现","type":"richtext","required":true},{"key":"conclusion","label":"结论建议","type":"richtext"},{"key":"attachments","label":"附件","type":"file"}]`, Layout: "1", IsSystem: true},
		{Name: "案件侦查进展", Type: "special_project", Fields: `[{"key":"case_no","label":"案件编号","type":"text","required":true},{"key":"case_name","label":"案件名称","type":"text","required":true},{"key":"current_progress","label":"当前进展","type":"richtext","required":true},{"key":"evidence_status","label":"证据情况","type":"richtext"},{"key":"suspect_status","label":"涉案人员","type":"richtext"},{"key":"next_steps","label":"下一步措施","type":"richtext"},{"key":"difficulty","label":"困难问题","type":"richtext"}]`, Layout: "12", IsSystem: true},
		{Name: "突发事件快报", Type: "emergency_canvas", Fields: `[{"key":"event_name","label":"事件名称","type":"text","required":true},{"key":"event_level","label":"事件等级","type":"select","options":["I级","II级","III级","IV级"],"required":true},{"key":"event_time","label":"发生时间","type":"datetime","required":true},{"key":"event_location","label":"发生地点","type":"text","required":true},{"key":"brief","label":"事件概述","type":"richtext","required":true},{"key":"response","label":"处置措施","type":"richtext"},{"key":"casualty","label":"伤亡情况","type":"text"},{"key":"material_loss","label":"财产损失","type":"text"},{"key":"social_impact","label":"社会影响","type":"richtext"}]`, Layout: "21", IsSystem: true},
		{Name: "协作写作", Type: "collaborative_writing", Fields: `[{"key":"section1","label":"第一部分","type":"richtext"},{"key":"section2","label":"第二部分","type":"richtext"},{"key":"section3","label":"第三部分","type":"richtext"},{"key":"section4","label":"第四部分","type":"richtext"},{"key":"review_notes","label":"审阅意见","type":"richtext"}]`, Layout: "22", IsSystem: true},
	}
	database.DB.Create(&templates)
	fmt.Printf("  ✓ 模板: %d 个\n", len(templates))
	return len(templates)
}

func seedReportTemplates() int {
	var count int64
	database.DB.Model(&models.ReportTemplate{}).Where("id != ?", "default").Count(&count)
	if count > 0 {
		fmt.Println("报告模板数据已存在，跳过 (共" + fmt.Sprint(count) + "个)")
		return 0
	}
	templates := []models.ReportTemplate{
		{
			ID:   "weekly_summary",
			Name: "周报总结模板",
			Content: `## 本周工作概览
{{userName}}（{{periodLabel}}）
### 任务统计
- 本周创建任务：{{totalCreated}} 条
- 本周完成任务：{{totalCompleted}} 条
- 完成率：{{completionRate}}%
- 被盯办次数：{{remindReceived}} 次
### 重点工作事项
{{highlightList}}
### 下周计划
1. 
### 问题与建议
-
`,
		},
		{
			ID:   "case_report",
			Name: "案件工作报告模板",
			Content: `## 案件工作汇报
### 基本情况
- 报告人：{{userName}}
- 时间：{{periodLabel}}
- 涉及案件数：{{totalCreated}}
- 已办结案件：{{totalCompleted}}
### 工作成效
{{highlightList}}
### 典型案例 / 疑难问题
-
### 下步工作打算
1.
`,
		},
	}
	database.DB.Create(&templates)
	fmt.Printf("  ✓ 报告模板: %d 个\n", len(templates))
	return len(templates)
}

func seedAIConfig() int {
	var count int64
	database.DB.Model(&models.AIConfig{}).Count(&count)
	if count > 0 {
		fmt.Println("AI配置已存在，跳过 (共" + fmt.Sprint(count) + "个)")
		return 0
	}
	cfg := models.AIConfig{
		ProviderName: "OpenAI",
		APIEndpoint:  "https://api.openai.com/v1",
		APIKey:       "sk-demo-placeholder-key-not-real-12345",
		ModelName:    "gpt-4o",
		Description:  "默认AI模型配置（演示环境，请替换为真实Key）",
		IsActive:     true,
	}
	database.DB.Create(&cfg)
	fmt.Println("  ✓ AI配置: 1 个")
	return 1
}

func loadUsers() []models.User {
	var users []models.User
	database.DB.Preload("Department").Find(&users)
	return users
}

func loadTags() []models.Tag {
	var tags []models.Tag
	database.DB.Find(&tags)
	return tags
}

func loadNotes() []models.Note {
	var notes []models.Note
	database.DB.Find(&notes)
	return notes
}

func seedWorkGroups() (int, int) {
	var count int64
	database.DB.Model(&models.WorkGroup{}).Count(&count)
	if count > 0 {
		var mc int64
		database.DB.Model(&models.WorkGroupMember{}).Count(&mc)
		fmt.Println("工作组数据已存在，跳过 (共" + fmt.Sprint(count) + "个, 成员" + fmt.Sprint(mc) + "条)")
		return 0, 0
	}

	users := loadUsers()
	if len(users) == 0 {
		return 0, 0
	}

	type wgDef struct {
		name         string
		description  string
		templateType string
		subGroups    []struct {
			subGroupName string
			reason       string
			leaderIdx    int
			memberIdx    []int
		}
	}

	defs := []wgDef{
		{
			name: "网络诈骗专案侦查组", templateType: "special_project",
			description: "针对近期高发电信网络诈骗案件，组建专案侦查组，开展跨区域串联分析、资金追踪及嫌疑人落地抓捕工作。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{":指挥协调组", "负责整体案件指挥协调", 2, []int{4, 11}},
				{"数据分析组", "负责诈骗数据建模与资金链路分析", 12, []int{13, 9}},
				{"侦查取证组", "负责电子取证与嫌疑人落地核查", 3, []int{20, 14}},
			},
		},
		{
			name: "社会治安专项整治工作组", templateType: "special_project",
			description: "结合辖区治安形势，开展为期一个月的治安专项整治行动，重点打击黄赌毒违法犯罪活动。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"行动指挥组", "负责专项行动的统筹指挥与督导", 5, []int{6, 22}},
				{"打击整治组", "负责重点区域巡查与违法犯罪打击", 23, []int{24, 17}},
				{"情报支撑组", "负责行动情报收集与研判分析", 12, []int{4, 13}},
			},
		},
		{
			name: "大数据情报会商工作组", templateType: "data_analysis",
			description: "整合刑侦、网安、经侦等多警种数据资源，开展月度情报会商，形成综合研判报告。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"刑侦分析组", "负责刑事案件数据汇总与趋势分析", 1, []int{4, 20}},
				{"网安技术组", "负责网络数据采集与技术支撑", 7, []int{8, 21}},
				{"综合研判组", "负责跨部门数据整合与报告撰写", 11, []int{12, 13}},
			},
		},
		{
			name: "禁毒集群打零专项行动组", templateType: "special_project",
			description: "针对零包贩毒网络，整合情报资源开展集群打击，从吸毒人员顺线追踪上线贩毒团伙。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"前线侦查组", "负责目标跟踪监控与现场抓捕", 14, []int{15, 17}},
				{"情报分析组", "负责涉毒情报研判与关系图谱分析", 12, []int{4}},
				{"证据采集组", "负责毒品检验鉴定与证据固定", 3, []int{20}},
			},
		},
		{
			name: "重要会议安保工作组", templateType: "default",
			description: "配合市委市政府重要会议安保工作，制定安保方案，组织警力部署，确保会议期间安全稳定。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"现场安保组", "负责会场及周边安全保卫", 5, []int{6, 22, 23}},
				{"交通管制组", "负责交通疏导与车辆管控", 16, []int{17}},
				{"应急备勤组", "负责突发事件应急处置", 11, []int{6, 16}},
			},
		},
		{
			name: "网络舆情监测应对组", templateType: "emergency_canvas",
			description: "针对近期网络热点舆情事件，建立快速响应机制，实时监测、分析研判、及时处置，维护网络空间清朗。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"舆情监测组", "负责7x24小时网络舆情动态监测", 7, []int{8, 21}},
				{"分析研判组", "负责舆情态势分析与风险评估", 11, []int{12, 13}},
				{"处置应对组", "负责舆情引导与事件处置", 5, []int{6}},
			},
		},
		{
			name: "年终工作总结起草组", templateType: "collaborative_writing",
			description: "起草全局年度工作总结报告，汇总各警种各部门全年工作数据，形成年度工作汇报材料。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"材料起草组", "负责报告主体内容撰写", 22, []int{11, 12}},
				{"数据汇总组", "负责各条线工作数据收集整理", 13, []int{4}},
				{"审核修订组", "负责报告审核与文字润色", 0, []int{1, 5}},
			},
		},
		{
			name: "科信系统升级改造项目组", templateType: "default",
			description: "推进公安信息化系统升级改造工程，包括数据库迁移、应用系统更新、网络安全加固等内容。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"技术开发组", "负责系统代码开发与接口联调", 18, []int{19, 21}},
				{"安全审计组", "负责系统安全评估与漏洞修复", 7, []int{21}},
				{"项目协调组", "负责需求对接与进度管理", 11, []int{13}},
			},
		},
		{
			name: "经侦领域专案分析组", templateType: "data_analysis",
			description: "对近期涉及非法集资、虚开增值税发票等经济犯罪线索进行分析研判，确定重点侦查方向。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"资金追踪组", "负责银行账户与资金流向分析", 9, []int{10, 13}},
				{"数据建模组", "负责经济数据建模与异常检测", 12, []int{13}},
				{"案件协调组", "负责与税务、金融监管等部门对接", 1, []int{9}},
			},
		},
		{
			name: "应急演练筹备工作组", templateType: "emergency_canvas",
			description: "筹备全市公安系统年度应急处突综合演练，制定演练方案，协调各部门参演力量，做好后勤保障。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"方案制定组", "负责演练方案设计与场景编写", 11, []int{12, 6}},
				{"后勤保障组", "负责演练物资筹备与场地布置", 6, []int{22}},
				{"评估考核组", "负责演练成效评估与总结报告", 0, []int{1, 5}},
			},
		},
		{
			name: "反电诈宣传教育工作组", templateType: "default",
			description: "开展全民反电信网络诈骗宣传教育活动，制作宣传材料，组织社区宣讲，提升群众防诈骗意识和能力。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"宣传策划组", "负责宣传方案策划与材料制作", 22, []int{8}},
				{"社区宣讲组", "负责深入社区开展面对面宣讲", 23, []int{24, 22}},
				{"媒体联络组", "负责与媒体对接及线上宣传推广", 8, []int{21}},
			},
		},
		{
			name: "刑事技术鉴定支撑组", templateType: "special_project",
			description: "为全市重大刑事案件提供技术鉴定支持，包括DNA比对、指纹鉴定、文书检验、电子物证分析等。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"生物物证组", "负责DNA检验与指纹比对鉴定", 3, []int{20}},
				{"理化分析组", "负责毒物、微量物证分析", 20, []int{3}},
				{"电子物证组", "负责电子数据取证与恢复", 7, []int{8, 21}},
			},
		},
		{
			name: "跨区域警务协作工作组", templateType: "default",
			description: "建立与周边地市公安机关的跨区域警务协作机制，实现情报共享、联动打击、快速响应。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"情报共享组", "负责跨区域情报交换与研判", 11, []int{4, 12}},
				{"联动打击组", "负责协同抓捕与联合办案", 1, []int{2, 5}},
				{"技术保障组", "负责协作平台技术支撑", 18, []int{19}},
			},
		},
		{
			name: "智慧警务平台建设组", templateType: "default",
			description: "推进智慧警务大数据平台一期建设，整合多源数据，建设可视化指挥调度系统。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"需求分析组", "负责各部门需求调研与分析", 13, []int{12}},
				{"平台开发组", "负责平台架构设计与编码实现", 18, []int{19, 21}},
				{"测试验收组", "负责功能测试与上线部署", 7, []int{18}},
			},
		},
		{
			name: "扫黑除恶常态化工作组", templateType: "special_project",
			description: "持续推进扫黑除恶专项斗争常态化，深挖黑恶势力违法犯罪线索，强化重点行业领域整治。",
			subGroups: []struct {
				subGroupName string
				reason       string
				leaderIdx    int
				memberIdx    []int
			}{
				{"线索核查组", "负责涉黑涉恶线索初步核查", 1, []int{2, 4}},
				{"案件侦办组", "负责涉黑案件深入侦查与取证", 2, []int{3, 20}},
				{"行业整治组", "负责重点行业乱象排查整治", 5, []int{23}},
			},
		},
	}

	wgCount := 0
	memberCount := 0
	for _, d := range defs {
		initiator := users[rng.Intn(len(users))]
		dueTime := time.Now().Add(time.Duration(randBetween(7, 90)) * 24 * time.Hour)
		statuses := []string{"active", "active", "active", "active", "active", "completed", "dissolved"}
		wg := models.WorkGroup{
			Name:         d.name,
			Description:  d.description,
			InitiatorID:  initiator.ID,
			TemplateType: d.templateType,
			Status:       pick(statuses),
			DueTime:      &dueTime,
			CreatedAt:    randTime(90),
			UpdatedAt:    time.Now(),
		}
		database.DB.Create(&wg)

		added := map[uuid.UUID]bool{}
		for _, sg := range d.subGroups {
			leader := users[sg.leaderIdx%len(users)]
			if !added[leader.ID] {
				dbMember := models.WorkGroupMember{
					GroupID:      wg.ID,
					UserID:       leader.ID,
					Role:         "leader",
					SubGroupName: sg.subGroupName,
				}
				database.DB.Create(&dbMember)
				added[leader.ID] = true
				memberCount++
			}

			for _, mi := range sg.memberIdx {
				uid := users[mi%len(users)].ID
				if !added[uid] {
					dbMember := models.WorkGroupMember{
						GroupID:      wg.ID,
						UserID:       uid,
						Role:         "member",
						SubGroupName: sg.subGroupName,
					}
					database.DB.Create(&dbMember)
					added[uid] = true
					memberCount++
				}
			}
		}
		wgCount++
	}

	fmt.Printf("  ✓ 专项工作组: %d 个 (成员 %d 条)\n", wgCount, memberCount)
	return wgCount, memberCount
}

func seedPresetGroups() (int, int) {
	var count int64
	database.DB.Model(&models.PresetGroup{}).Count(&count)
	if count > 0 {
		var mc int64
		database.DB.Model(&models.PresetGroupMember{}).Count(&mc)
		fmt.Println("预设组数据已存在，跳过 (共" + fmt.Sprint(count) + "个, 成员" + fmt.Sprint(mc) + "条)")
		return 0, 0
	}

	users := loadUsers()
	if len(users) == 0 {
		return 0, 0
	}

	type presetDef struct {
		name         string
		description  string
		templateType string
		members      []struct {
			userIdx      int
			role         string
			subGroupName string
		}
	}

	defs := []presetDef{
		{
			name: "刑侦专案常用配置", templateType: "special_project",
			description: "刑事专案侦查常用人员配置，适用于各类刑事案件侦办",
			members: []struct {
				userIdx      int
				role         string
				subGroupName string
			}{
				{1, "leader", "指挥协调组"},
				{2, "member", "侦查取证组"},
				{3, "member", "侦查取证组"},
				{4, "member", "情报支撑组"},
				{12, "leader", "情报支撑组"},
				{20, "member", "技术鉴定组"},
			},
		},
		{
			name: "治安整治常用配置", templateType: "special_project",
			description: "社会治安专项整治行动常用人员配置",
			members: []struct {
				userIdx      int
				role         string
				subGroupName string
			}{
				{5, "leader", "行动指挥组"},
				{6, "member", "行动指挥组"},
				{23, "leader", "打击整治组"},
				{24, "member", "打击整治组"},
				{17, "member", "应急备勤组"},
			},
		},
		{
			name: "情报会商常用配置", templateType: "data_analysis",
			description: "多警种情报会商与分析研判常用人员配置",
			members: []struct {
				userIdx      int
				role         string
				subGroupName string
			}{
				{11, "leader", "综合研判组"},
				{12, "member", "综合研判组"},
				{4, "member", "刑侦分析组"},
				{7, "member", "网安技术组"},
				{8, "member", "网安技术组"},
				{13, "member", "数据分析组"},
				{9, "member", "经侦分析组"},
			},
		},
		{
			name: "应急响应常用配置", templateType: "emergency_canvas",
			description: "突发事件应急处置常用人员快速配置",
			members: []struct {
				userIdx      int
				role         string
				subGroupName string
			}{
				{0, "leader", "总指挥部"},
				{11, "leader", "现场指挥组"},
				{5, "member", "现场指挥组"},
				{6, "member", "现场指挥组"},
				{16, "leader", "行动处置组"},
				{17, "member", "行动处置组"},
				{12, "member", "情报支撑组"},
				{22, "member", "后勤保障组"},
			},
		},
		{
			name: "网安工作常用配置", templateType: "data_analysis",
			description: "网络安全与舆情监测常用人员配置",
			members: []struct {
				userIdx      int
				role         string
				subGroupName string
			}{
				{7, "leader", "网络监控组"},
				{8, "member", "网络监控组"},
				{21, "member", "技术支撑组"},
				{12, "leader", "信息研判组"},
				{13, "member", "信息研判组"},
				{18, "member", "系统运维组"},
			},
		},
		{
			name: "材料写作常用配置", templateType: "collaborative_writing",
			description: "重要文件材料起草与审核常用配置",
			members: []struct {
				userIdx      int
				role         string
				subGroupName string
			}{
				{22, "leader", "材料起草组"},
				{11, "member", "材料起草组"},
				{13, "member", "数据支撑组"},
				{0, "leader", "审核组"},
				{1, "member", "审核组"},
			},
		},
		{
			name: "技术保障常用配置", templateType: "default",
			description: "信息化系统建设与运维常用人员配置",
			members: []struct {
				userIdx      int
				role         string
				subGroupName string
			}{
				{18, "leader", "技术开发组"},
				{19, "member", "技术开发组"},
				{21, "member", "安全审计组"},
				{7, "leader", "安全审计组"},
				{13, "member", "数据分析组"},
			},
		},
		{
			name: "禁毒工作常用配置", templateType: "special_project",
			description: "毒品案件侦查与治理常用配置",
			members: []struct {
				userIdx      int
				role         string
				subGroupName string
			}{
				{14, "leader", "前线侦查组"},
				{15, "member", "前线侦查组"},
				{12, "leader", "情报分析组"},
				{4, "member", "情报分析组"},
				{20, "member", "证据采集组"},
				{17, "member", "行动支援组"},
			},
		},
	}

	presetCount := 0
	memberCount := 0
	for _, d := range defs {
		creator := users[rng.Intn(len(users))]
		preset := models.PresetGroup{
			Name:         d.name,
			Description:  d.description,
			TemplateType: d.templateType,
			CreatorID:    creator.ID,
		}
		database.DB.Create(&preset)
		presetCount++

		for _, m := range d.members {
			if m.userIdx < len(users) {
				pm := models.PresetGroupMember{
					PresetID:     preset.ID,
					UserID:       users[m.userIdx].ID,
					Role:         m.role,
					SubGroupName: m.subGroupName,
				}
				database.DB.Create(&pm)
				memberCount++
			}
		}
	}

	fmt.Printf("  ✓ 预设组: %d 个 (成员 %d 条)\n", presetCount, memberCount)
	return presetCount, memberCount
}

type noteDef struct {
	title        string
	subTag       string
	content      string
	contentDelta string
	templateType string
	colorStatus  string
	sourceType   string
	isArchived   bool
	archivedDays int
}

func getNotePool() []noteDef {
	active := []noteDef{
		{title: "【刑事案件】XX小区入室盗窃案侦查推进", subTag: "刑事案侦", content: "对XX小区入室盗窃案进行现场复核，调取周边监控录像，走访周边居民收集线索。目前已完成指纹提取工作，待比对结果反馈。", templateType: "special_project", colorStatus: "red", sourceType: "self"},
		{title: "【情报研判】本周辖区治安态势分析", subTag: "情报研判", content: "汇总本周辖区110接警数据、刑事案件发案数、治安案件查处数，形成周度治安态势分析报告。重点关注夜间侵财类案件上升趋势。", templateType: "data_analysis", colorStatus: "yellow", sourceType: "self"},
		{title: "【网安工作】反诈预警信息核查处置", subTag: "反诈预警", content: "通过反诈平台推送的预警信息，对疑似被骗人员进行电话劝阻和上门核实。本月已成功劝阻32人次，避免潜在损失约80万元。", templateType: "default", colorStatus: "red", sourceType: "assigned"},
		{title: "【治安管理】娱乐场所突击检查行动", subTag: "治安检查", content: "联合市场监管部门对辖区内KTV、网吧、洗浴中心等娱乐场所进行突击检查，重点查处涉黄涉赌线索。检查方案已报批。", templateType: "special_project", colorStatus: "yellow", sourceType: "self"},
		{title: "【技术侦查】涉案手机数据提取分析", subTag: "技术取证", content: "对嫌疑人扣押的3部手机进行电子数据取证，提取通讯录、通话记录、微信聊天记录、位置轨迹数据，形成电子数据提取报告。", templateType: "data_analysis", colorStatus: "yellow", sourceType: "assigned"},
		{title: "【经侦案件】P2P平台非法吸收公众存款案", subTag: "经侦案件", content: "某P2P网络借贷平台涉嫌非法吸收公众存款，需对平台资金流水进行全面梳理，锁定资金去向，查清涉案人员组织结构。受害群众约500人。", templateType: "special_project", colorStatus: "red", sourceType: "self"},
		{title: "【紧急事务】省厅督导组即将到访检查", subTag: "迎检准备", content: "省公安厅督导组将于下周三到访，检查全市治安防控体系建设情况。需准备汇报材料、台账资料、迎检方案及路线安排。", templateType: "emergency_canvas", colorStatus: "red", sourceType: "assigned"},
		{title: "【禁毒工作】吸毒人员管控排查专项行动", subTag: "禁毒管控", content: "对辖区登记在册吸毒人员开展季度管控排查，上门核实现状，进行尿检抽查，更新人员档案信息。发现复吸人员及时送戒。", templateType: "special_project", colorStatus: "yellow", sourceType: "self"},
		{title: "【舆情监测】涉警网络舆情实时监测日报", subTag: "舆情监测", content: "对微博、微信、抖音等平台涉警舆情进行24小时滚动监测，重点关注群体性事件、执法争议、负面评价等敏感信息，及时编发舆情日报。", templateType: "data_analysis", colorStatus: "yellow", sourceType: "self"},
		{title: "【安保任务】市两会期间安保方案制定", subTag: "大型安保", content: "制定市人大、政协两会期间安全保障工作方案，包括会场安检、代表驻地保卫、沿线交通管控、应急处突力量部署等内容。", templateType: "emergency_canvas", colorStatus: "red", sourceType: "assigned"},
		{title: "【证据管理】XX杀人案物证流转跟踪", subTag: "证据管理", content: "对XX故意杀人案全部物证进行清点登记，按照鉴定需求分批次送检，跟踪鉴定进度并及时反馈结果给办案中队。", templateType: "special_project", colorStatus: "red", sourceType: "self"},
		{title: "【技术保障】警务通终端升级部署", subTag: "技术部署", content: "全市警务通移动终端系统版本升级至V3.2，需协调各基层单位分批进行升级操作，处理升级过程中出现的技术问题。", templateType: "default", colorStatus: "yellow", sourceType: "assigned"},
		{title: "【法制工作】重点案件法制审核", subTag: "法制审核", content: "对近期移送审查起诉的12起案件进行法制审核，重点审查证据链完整性、程序合法性、定性准确性，出具法制审核意见书。", templateType: "default", colorStatus: "yellow", sourceType: "self"},
		{title: "【反恐处突】重点目标单位安全检查", subTag: "反恐防范", content: "对辖区内党政机关、学校、医院、商场等重点目标单位进行反恐安全措施检查，督促落实人防物防技防措施。", templateType: "special_project", colorStatus: "red", sourceType: "assigned"},
		{title: "【数据分析】辖区犯罪热点地图绘制", subTag: "数据建模", content: "利用GIS系统将近期案件发案数据空间化分析，绘制犯罪热点分布图，识别高发区域和重点时段，为巡逻防控部署提供数据支撑。", templateType: "data_analysis", colorStatus: "yellow", sourceType: "self"},
		{title: "【综合协调】年度考核指标完成情况汇总", subTag: "综合协调", content: "统计各警种各部门年度核心考核指标完成进度，梳理未达标项目及原因，提出冲刺阶段工作建议。截止日期前必须完成所有指标。", templateType: "collaborative_writing", colorStatus: "yellow", sourceType: "assigned"},
		{title: "【宣传工作】122全国交通安全日主题宣传活动", subTag: "宣传教育", content: "策划122全国交通安全日主题宣传活动方案，包括社区宣传点设置、宣传展板制作、媒体采访对接、中小学生交通安全教育等内容。", templateType: "default", colorStatus: "yellow", sourceType: "self"},
		{title: "【系统建设】出入境证件自助办理终端上线", subTag: "系统建设", content: "市民中心出入境大厅新增3台证件办理自助终端设备，需完成系统联调测试、操作指引制作、工作人员培训等工作。", templateType: "default", colorStatus: "green", sourceType: "assigned"},
		{title: "【警务实战】多警种联合抓捕演练方案", subTag: "实战演练", content: "制定由刑侦、特警、网安、情报多警种参与的联合抓捕实战演练方案，包含目标人物信息研判、抓捕时机选择、力量编组、安全预案等。", templateType: "emergency_canvas", colorStatus: "green", sourceType: "self"},
		{title: "【队伍建设】新入职民警岗前培训计划", subTag: "队伍建设", content: "制定本年度新招录民警岗前培训计划，涵盖政治理论、法律法规、警务技能、信息化应用四个模块，为期45天。", templateType: "default", colorStatus: "green", sourceType: "self"},
		{title: "【专项行动】打击盗窃机动车犯罪集中行动", subTag: "专项行动", content: "针对近期机动车盗窃案件高发态势，开展为期两周的集中打击行动。梳理串并案件线索，锁定重点嫌疑人员，部署蹲守和抓捕力量。", templateType: "special_project", colorStatus: "red", sourceType: "assigned"},
		{title: "【内部管理】警用装备清点更换台账", subTag: "装备管理", content: "对全局警用装备进行全面清点，统计损坏、过期、缺失情况，编制装备更新采购计划，更新装备管理电子台账。", templateType: "default", colorStatus: "green", sourceType: "self"},
		{title: "【经侦工作】虚开增值税专用发票专案侦查", subTag: "经侦案件", content: "接税务部门移送线索，某商贸公司涉嫌虚开增值税专用发票，涉案金额初步估算超500万元。需调取银行流水、发票底联、公司账册。", templateType: "special_project", colorStatus: "red", sourceType: "self"},
		{title: "【安保工作】大型演唱会现场安保方案", subTag: "大型安保", content: "某明星演唱会将于本周六在体育中心举行，预计到场观众3万人。制定现场安保与应急疏散方案，部署安保力量300人。", templateType: "emergency_canvas", colorStatus: "yellow", sourceType: "assigned"},
		{title: "【网安巡查】网络谣言线索核查处置", subTag: "网络安全", content: "接上级通报，本地微信群流传涉及食品安全的不实谣言信息，需追查信息源头、发布者身份，及时发布辟谣信息，依法处理造谣传谣行为。", templateType: "default", colorStatus: "red", sourceType: "assigned"},
		{title: "【基层警务】社区矛盾纠纷排查化解台账", subTag: "基层警务", content: "对辖区内近期摸排的矛盾纠纷进行全面梳理，分类登记、逐件分析、明确责任人，对重点纠纷启动多元调解机制。", templateType: "default", colorStatus: "yellow", sourceType: "self"},
		{title: "【数据分析】110接处警数据月度分析报告", subTag: "数据分析", content: "统计本月110接警总量、有效警情、无效警情、非警务警情分流等数据，分析接处警效率、高发警情类型趋势、出警时间达标率。", templateType: "data_analysis", colorStatus: "green", sourceType: "self"},
		{title: "【禁毒宣传】626国际禁毒日系列活动策划", subTag: "宣传教育", content: "策划626国际禁毒日主题系列宣传活动方案：校园禁毒讲座、社区禁毒展板、禁毒主题文艺汇演、志愿者招募等。", templateType: "default", colorStatus: "green", sourceType: "self"},
		{title: "【技术侦查】视频监控图像研判分析", subTag: "技术取证", content: "对案发时间段内周边的社会监控和公安天网监控视频进行筛查分析，提取嫌疑人图像特征，制作嫌疑人模拟画像和体态特征描述。", templateType: "data_analysis", colorStatus: "yellow", sourceType: "assigned"},
		{title: "【协同写作】年度公安工作会议讲话稿起草", subTag: "文秘写作", content: "起草年度公安工作会议党委书记讲话稿，需涵盖上年度工作回顾、当前形势分析、新年度工作部署三个部分，字数控制在5000字以内。", templateType: "collaborative_writing", colorStatus: "green", sourceType: "assigned"},
		{title: "【内部事务】OA办公系统流程优化需求收集", subTag: "内部管理", content: "面向全局各部门征集OA办公系统使用意见建议，重点收集审批流程、公文流转、会议管理、考勤系统等模块的优化需求。", templateType: "default", colorStatus: "green", sourceType: "self"},
		{title: "【应急管理】防汛抗台应急预案修订", subTag: "应急管理", content: "根据气象部门预测今年汛期降雨偏多，修订防汛抗台应急预案，更新应急物资储备清单、群众转移安置点、抢险救援力量编组。", templateType: "emergency_canvas", colorStatus: "red", sourceType: "assigned"},
		{title: "【科技项目】人像识别系统三期建设方案", subTag: "科技项目", content: "编制城市人像识别系统三期建设方案，计划新增前端智能摄像机200路，扩容后端算力节点，升级人脸比对算法精度。", templateType: "default", colorStatus: "green", sourceType: "self"},
		{title: "【证据管理】DNA数据库样本采集与录入", subTag: "证据管理", content: "对在押犯罪嫌疑人和重点管控人员开展DNA样本采集工作，按规范完成采样、登记、送检、入库全流程，确保数据准确率100%。", templateType: "special_project", colorStatus: "yellow", sourceType: "self"},
		{title: "【后勤保障】新办公楼搬迁方案制定", subTag: "后勤保障", content: "刑警支队新办公楼即将交付使用，制定搬迁方案，包括物资打包、设备拆卸安装、网络布线、搬迁时序、科室分配等详细内容。", templateType: "default", colorStatus: "green", sourceType: "self"},
		{title: "【基层走访】社区民警入户走访记录", subTag: "基层警务", content: "本周走访辖区3个重点小区、2个商业街区，走访群众约120户，收集社情民意及治安隐患线索共8条。重点关注独居老人安全和流动人口管理。", templateType: "default", colorStatus: "green", sourceType: "self"},
		{title: "【智慧警务】智能巡防系统上线测试", subTag: "智慧警务", content: "新一代智能巡防系统开发完成进入测试阶段，需组织各基层所队进行试点应用，收集操作反馈并修复已知问题。", templateType: "default", colorStatus: "yellow", sourceType: "assigned"},
		{title: "【人资管理】年度警力资源调配方案", subTag: "人资管理", content: "根据各部门编制现状、业务量变化和新警招录计划，制定下年度警力资源调配方案，统筹优化各岗位人员配置。", templateType: "data_analysis", colorStatus: "green", sourceType: "self"},
		{title: "【执法监督】执法记录仪使用情况专项督查", subTag: "执法监督", content: "对全局一线执法民警执法记录仪佩戴、开启、数据上传情况进行专项督查，通报不合格情况并督促整改。", templateType: "default", colorStatus: "yellow", sourceType: "assigned"},
		{title: "【专项行动】打击涉枪涉爆违法犯罪排查", subTag: "专项行动", content: "根据上级部署开展涉枪涉爆重点人员排查管控工作，对辖区重点行业、物流寄递、旧货市场等进行全面检查。", templateType: "special_project", colorStatus: "red", sourceType: "self"},
		{title: "【信访工作】涉法涉诉信访案件化解", subTag: "信访工作", content: "对上级交办和自行排查的5件涉法涉诉信访积案逐案研究化解方案，落实包案领导和责任人，如期上报化解进展。", templateType: "default", colorStatus: "yellow", sourceType: "assigned"},
		{title: "【数据分析】刑事案件发案趋势预测模型", subTag: "数据建模", content: "利用近三年刑事案件发案数据构建趋势预测模型，结合季节因素、区域特征、经济指标等进行综合预测分析。", templateType: "data_analysis", colorStatus: "yellow", sourceType: "self"},
		{title: "【协同写作】政协提案办理情况答复材料", subTag: "文秘写作", content: "汇总各部门承办的政协提案办理情况，起草向市政协常委会的提案办理工作报告，包含办理数量、办结率、典型事例。", templateType: "collaborative_writing", colorStatus: "green", sourceType: "assigned"},
		{title: "【治安整治】校园周边治安环境专项整治", subTag: "治安整治", content: "新学期开学在即，联合教育局开展校园周边治安环境专项整治，排查整治黑网吧、售卖违禁品商铺、交通隐患点。", templateType: "special_project", colorStatus: "red", sourceType: "self"},
		{title: "【警务协作】跨省追逃嫌疑人协调联络", subTag: "警务协作", content: "在逃犯罪嫌疑人张某疑似潜逃至外省藏匿，需与当地公安机关建立协作机制，通报案件信息，协调抓捕方案。", templateType: "emergency_canvas", colorStatus: "red", sourceType: "self"},
		{title: "【后勤项目】警用车辆更新采购招标", subTag: "后勤保障", content: "本年度计划更新购置巡逻车8辆、执法执勤车3辆，需编制招标文件、技术参数、预算方案并报市财政局审批。", templateType: "default", colorStatus: "green", sourceType: "self"},
		{title: "【技术培训】视频侦查技术应用培训", subTag: "技术培训", content: "组织全局刑侦民警开展视频侦查技术应用培训，内容涵盖监控视频分析、人像比对技术、视频追踪技巧等实战课程。", templateType: "default", colorStatus: "yellow", sourceType: "assigned"},
		{title: "【案件侦办】跨区域系列盗窃案串并分析", subTag: "刑事案侦", content: "本辖区近期连续发生5起技术开锁入室盗窃案，经串并分析发现与相邻两市的3起案件手法特征一致，启动侦查协作机制。", templateType: "special_project", colorStatus: "red", sourceType: "self"},
		{title: "【舆情应对】重大案件舆情引导方案", subTag: "舆情应对", content: "某重大刑事案件引发社会广泛关注和网络热议，制定舆情引导方案，规范信息发布口径，确定官方发言人。", templateType: "emergency_canvas", colorStatus: "red", sourceType: "assigned"},
		{title: "【内部审计】年度财务收支合规性审查", subTag: "内部审计", content: "对全局上年度财务收支进行内部合规性审计，重点审查专项经费使用、政府采购程序、差旅报销等环节的合规性。", templateType: "default", colorStatus: "green", sourceType: "self"},
		{title: "【通信保障】应急通信系统压力测试", subTag: "通信保障", content: "对全市公安应急通信系统开展年度压力测试，模拟大规模突发事件场景下集群通信、视频回传、数据共享等功能的承载能力。", templateType: "default", colorStatus: "yellow", sourceType: "self"},
		{title: "【基层减负】移动警务应用推广使用", subTag: "智慧警务", content: "推广使用移动警务终端应用，实现移动审批、移动查询、移动采集等功能的全面覆盖，减少基层民警往返机关的时间成本。", templateType: "default", colorStatus: "green", sourceType: "assigned"},
		{title: "【安全排查】高层建筑消防隐患排查", subTag: "安全排查", content: "联合消防救援支队对辖区内高层建筑、大型商业综合体开展消防隐患排查整治，重点检查消防通道、灭火设施、应急预案。", templateType: "special_project", colorStatus: "red", sourceType: "self"},
		{title: "【基础建设】派出所标准化规范化建设验收", subTag: "基础建设", content: "对辖区内已完成标准化改造的5个派出所进行验收评估，检查办公场所功能分区、信息化设备配备、警营文化建设等内容。", templateType: "default", colorStatus: "green", sourceType: "assigned"},
		{title: "【会务保障】全市公安工作推进会会务保障", subTag: "会务保障", content: "筹备全市公安工作推进会会务工作，包括会场布置、会议材料编印、参会人员通知、后勤保障、会议记录整理等全流程。", templateType: "default", colorStatus: "yellow", sourceType: "assigned"},
		{title: "【数据质量】警综平台数据质量核查整改", subTag: "数据质量", content: "对警综平台基础数据进行质量核查，发现身份证号错误、案件类别标记错误、时间逻辑错误等问题数据共82条，逐条修正。", templateType: "data_analysis", colorStatus: "green", sourceType: "self"},
	}
	archived := []noteDef{
		{title: "【已归档】社区治安巡防月度总结", subTag: "社区警务", content: "回顾上个月社区治安巡防工作开展情况、发现隐患及处置结果。巡防覆盖率100%，辖区内治安形势总体平稳可控。", templateType: "default", colorStatus: "green", sourceType: "self", isArchived: true, archivedDays: 45},
		{title: "【已归档】某明星演唱会安保工作总结", subTag: "大型安保", content: "本次演唱会场馆内外安保工作总体平稳，入场观众约2.8万人。现场处置轻微治安事件12起，无重大安全事故发生。", templateType: "default", colorStatus: "green", sourceType: "self", isArchived: true, archivedDays: 60},
		{title: "【已归档】端午节龙舟赛安保方案", subTag: "大型安保", content: "制定端午节期间沿江龙舟赛事安保方案，包括交通管制、河堤警戒、应急救援、通讯保障等内容，保障赛事安全有序进行。", templateType: "emergency_canvas", colorStatus: "green", sourceType: "assigned", isArchived: true, archivedDays: 90},
		{title: "【已归档】假期治安防控工作方案", subTag: "假期安保", content: "制定春节假期期间社会治安整体防控工作方案，部署警力加强重点时段重点区域巡逻管控，确保人民群众平安过节。", templateType: "special_project", colorStatus: "green", sourceType: "self", isArchived: true, archivedDays: 120},
		{title: "【已归档】反诈集中宣传月活动总结", subTag: "宣传教育", content: "为期一个月的反电信网络诈骗集中宣传活动圆满结束。共举办社区宣讲45场，发放宣传资料2万份，全市电诈发案率同比下降12%。", templateType: "default", colorStatus: "green", sourceType: "self", isArchived: true, archivedDays: 70},
		{title: "【已归档】上半年刑事技术支撑工作统计", subTag: "数据统计", content: "上半年共完成DNA检验380份、指纹比对1200人次、文书检验230份、电子数据取证65件，为各类案件侦破提供有力支撑。", templateType: "data_analysis", colorStatus: "green", sourceType: "self", isArchived: true, archivedDays: 150},
		{title: "【已归档】干部警务人员年度考核材料", subTag: "人资管理", content: "组织全局200名民警年度考核工作，包括德能勤绩廉综合考评、民主测评、主管评价、考核等次确定等内容。", templateType: "default", colorStatus: "green", sourceType: "assigned", isArchived: true, archivedDays: 180},
		{title: "【已归档】信息化项目验收文档汇总", subTag: "项目管理", content: "汇总智慧安防小区项目、视频图像综合应用平台升级项目等5个信息化项目的验收文档、技术报告和用户使用反馈。", templateType: "default", colorStatus: "green", sourceType: "self", isArchived: true, archivedDays: 200},
	}
	return append(active, archived...)
}

func seedNotes() (int, int, int) {
	var count int64
	database.DB.Model(&models.Note{}).Count(&count)
	if count > 0 {
		fmt.Printf("任务数据已存在，跳过 (共%d条)\n", count)
		return 0, 0, 0
	}

	users := loadUsers()
	tags := loadTags()
	pool := getNotePool()
	noteCount := 0
	assigneeCount := 0
	attachCount := 0

	for i, nd := range pool {
		creator := users[rng.Intn(len(users))]
		now := time.Now()

		var createdAt time.Time
		var dueTime *time.Time
		var completedAt *time.Time
		var archiveTime *time.Time
		var isArchived bool

		if nd.isArchived {
			isArchived = true
			createdAt = now.Add(-time.Duration(nd.archivedDays)*24*time.Hour - time.Duration(rng.Intn(48))*time.Hour)
			archiveTimeVal := createdAt.Add(time.Duration(randBetween(14, 60)) * 24 * time.Hour)
			archiveTime = &archiveTimeVal
			if archiveTimeVal.Before(now.Add(-24 * time.Hour)) {
				completedAtVal := archiveTimeVal.Add(-time.Duration(randBetween(1, 7)) * 24 * time.Hour)
				completedAt = &completedAtVal
			}
		} else {
			createdAt = randTime(90)
			if randomBool(0.6) {
				dueTime = randDueTime()
			}
			if randomBool(0.7) {
				completedAtVal := createdAt.Add(time.Duration(randBetween(1, 14)) * 24 * time.Hour)
				if completedAtVal.Before(now) {
					completedAt = &completedAtVal
				}
			}
		}

		serialNo := fmt.Sprintf("NT-%04d-%02d", i+1, now.Year()%100)
		remindCount := 0
		if randomBool(0.3) {
			remindCount = randBetween(1, 5)
		}

		note := models.Note{
			Title:        nd.title,
			SubTag:       nd.subTag,
			Content:      nd.content,
			ContentDelta: nd.contentDelta,
			TemplateType: nd.templateType,
			ColorStatus:  nd.colorStatus,
			SourceType:   nd.sourceType,
			CreatorID:    creator.ID,
			OwnerID:      creator.ID,
			DepartmentID: creator.DepartmentID,
			SerialNo:     serialNo,
			IsArchived:   isArchived,
			ArchiveTime:  archiveTime,
			DueTime:      dueTime,
			CompletedAt:  completedAt,
			RemindCount:  remindCount,
			CreatedAt:    createdAt,
			UpdatedAt:    createdAt,
		}

		if randomBool(0.3) {
			assigner := users[rng.Intn(len(users))]
			note.AssignerID = &assigner.ID
		}

		if err := database.DB.Create(&note).Error; err != nil {
			continue
		}
		noteCount++

		assigneePool := users
		if randomBool(0.6) {
			assigneePool = pickN(users, randBetween(1, 4))
		}
		for j, a := range assigneePool {
			if a.ID == creator.ID && len(assigneePool) == 1 {
				continue
			}
			roleInNote := "member"
			if j == 0 {
				roleInNote = "leader"
			}
			feedback := ""
			isRead := false
			if note.CompletedAt != nil && randomBool(0.8) {
				feedback = pick([]string{
					"任务已完成，相关材料已整理归档。",
					"已按要求完成各项工作内容，请审核。",
					"工作进展顺利，成果已提交上级审阅。",
				})
				isRead = true
			}
			if note.DueTime != nil && time.Now().After(*note.DueTime) && note.CompletedAt == nil {
				if randomBool(0.5) {
					feedback = pick([]string{
						"因案件侦查需要延长时限，预计还需1周完成。",
						"任务执行中遇到客观困难，申请延长办理时限。",
					})
				}
			}
			na := models.NoteAssignee{
				NoteID:          note.ID,
				UserID:          a.ID,
				RoleInNote:      roleInNote,
				FeedbackContent: feedback,
				IsRead:          isRead,
			}
			if feedback != "" {
				ft := now.Add(-time.Duration(randBetween(1, 24)) * time.Hour)
				na.FeedbackAt = &ft
			}
			if err := database.DB.Create(&na).Error; err != nil {
				continue
			}
			assigneeCount++
		}

		if randomBool(0.25) {
			attachFiles := []struct {
				name string
				size int64
				mime string
			}{
				{"现场照片.zip", 2048000, "application/zip"},
				{"案件研判报告.pdf", 512000, "application/pdf"},
				{"监控视频片段.mp4", 10240000, "video/mp4"},
				{"嫌疑人信息表.xlsx", 256000, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"},
				{"询问笔录.docx", 128000, "application/vnd.openxmlformats-officedocument.wordprocessingml.document"},
				{"电子数据取证报告.pdf", 768000, "application/pdf"},
				{"现场勘验记录.pdf", 384000, "application/pdf"},
				{"物证鉴定报告.docx", 192000, "application/vnd.openxmlformats-officedocument.wordprocessingml.document"},
			}
			nAttach := randBetween(1, 3)
			for k := 0; k < nAttach; k++ {
				f := pick(attachFiles)
				att := models.NoteAttachment{
					NoteID:   note.ID,
					FileName: f.name,
					FilePath: "/uploads/" + uuid.New().String()[:8] + "/" + f.name,
					FileSize: f.size,
					MimeType: f.mime,
				}
				if err := database.DB.Create(&att).Error; err != nil {
					continue
				}
				attachCount++
				_ = k
			}
		}

		if randomBool(0.7) {
			selectedTags := pickN(tags, randBetween(1, 3))
			database.DB.Model(&note).Association("Tags").Append(selectedTags)
		}
	}

	fmt.Printf("  ✓ 任务: %d 条 (指派 %d 条, 附件 %d 条)\n", noteCount, assigneeCount, attachCount)
	return noteCount, assigneeCount, attachCount
}

func seedReminders() int {
	var count int64
	database.DB.Model(&models.Reminder{}).Count(&count)
	if count > 0 {
		fmt.Printf("盯办提醒数据已存在，跳过 (共%d条)\n", count)
		return 0
	}

	users := loadUsers()
	notes := loadNotes()
	if len(notes) == 0 {
		fmt.Println("  跳过盯办提醒: 无任务记录")
		return 0
	}

	remindCount := 0
	for _, note := range notes {
		if note.IsArchived || note.CompletedAt != nil {
			continue
		}
		if !randomBool(0.4) {
			continue
		}
		nReminds := randBetween(1, 4)
		for i := 0; i < nReminds; i++ {
			reminder := users[rng.Intn(len(users))]
			target := users[rng.Intn(len(users))]
			if reminder.ID == target.ID {
				continue
			}
			remindType := pick([]string{"normal", "urgent"})
			message := pick([]string{
				fmt.Sprintf("请关注任务「%s」的推进进度，如有困难请及时反馈。", note.Title),
				fmt.Sprintf("任务「%s」即将到期，请抓紧完成。", note.Title),
				fmt.Sprintf("上级要求在明日前反馈任务「%s」的办理情况。", note.Title),
				fmt.Sprintf("提醒：任务「%s」已逾期%d天，请尽快办理！", note.Title, randBetween(1, 10)),
				fmt.Sprintf("案件会商需要你提供任务「%s」的最新进展情况。", note.Title),
			})
			r := models.Reminder{
				NoteID:         note.ID,
				ReminderID:     reminder.ID,
				TargetID:       target.ID,
				Message:        message,
				RemindType:     remindType,
				IsAcknowledged: randomBool(0.6),
				CreatedAt:      randTime(60),
			}
			if err := database.DB.Create(&r).Error; err != nil {
				continue
			}
			remindCount++
		}
	}

	fmt.Printf("  ✓ 盯办提醒: %d 条\n", remindCount)
	return remindCount
}

func seedCollaborationRooms() int {
	var count int64
	database.DB.Model(&models.CollaborationRoom{}).Count(&count)
	if count > 0 {
		fmt.Printf("协同房间数据已存在，跳过 (共%d个)\n", count)
		return 0
	}

	notes := loadNotes()
	collabNotes := []int{}
	for i, note := range notes {
		if note.TemplateType == "emergency_canvas" || note.TemplateType == "collaborative_writing" || note.TemplateType == "data_analysis" {
			collabNotes = append(collabNotes, i)
		}
	}
	if len(collabNotes) == 0 {
		fmt.Println("  跳过协同房间: 无可协同任务")
		return 0
	}

	picked := pickN(collabNotes, min(len(collabNotes), 12))
	roomCount := 0
	for _, idx := range picked {
		note := notes[idx]
		if randomBool(0.05) {
			continue
		}
		canvasData := `{"blocks": [], "connections": []}`
		columns := pick([]int{1, 2, 3})
		lat := time.Now().Add(-time.Duration(randBetween(1, 72)) * time.Hour)
		room := models.CollaborationRoom{
			NoteID:         note.ID,
			CanvasData:     canvasData,
			Columns:        columns,
			Version:        randBetween(0, 15),
			LastActivityAt: &lat,
			IsActive:       true,
			CreatedAt:      note.CreatedAt,
			UpdatedAt:      time.Now(),
		}
		if err := database.DB.Create(&room).Error; err != nil {
			continue
		}
		roomCount++
	}

	fmt.Printf("  ✓ 协同房间: %d 个\n", roomCount)
	return roomCount
}

func seedWorkReports() int {
	var count int64
	database.DB.Model(&models.WorkReport{}).Count(&count)
	if count > 0 {
		fmt.Printf("工作报告数据已存在，跳过 (共%d条)\n", count)
		return 0
	}

	users := loadUsers()
	reportCount := 0
	periods := []string{"weekly", "monthly", "quarterly"}
	periodLabels := []string{"周报", "月报", "季报"}

	for _, user := range users {
		nReports := randBetween(1, 3)
		for i := 0; i < nReports; i++ {
			pIdx := rng.Intn(len(periods))
			reportType := pick([]string{"ai", "manual"})
			period := periods[pIdx]
			periodLabel := periodLabels[pIdx]

			title := fmt.Sprintf("%s%s-%s", user.Name, periodLabel, fmt.Sprintf("第%02d期", i+1))
			content := fmt.Sprintf(`## 工作概览
%s（%s）期间共创建任务 %d 条，完成 %d 条，完成率为 %d%%。

## 主要工作
1. 案件侦查与办理工作持续推进
2. 情报信息收集与分析研判
3. 各项勤务安保任务执行
4. 内部事务与业务学习

## 存在问题与改进方向
1. 部分案件证据收集进度需加快
2. 加强与其他警种部门沟通协作
3. 进一步提升信息化应用水平

---
*本报告由系统自动生成*`, user.Name, periodLabel, randBetween(5, 25), randBetween(3, 20), randBetween(60, 100))

			statsSummary := fmt.Sprintf(`{"total_created":%d,"total_completed":%d,"completion_rate":%d,"remind_received":%d,"avg_completion_hours":%d}`,
				randBetween(5, 25), randBetween(3, 20), randBetween(60, 100), randBetween(0, 8), randBetween(2, 48))

			report := models.WorkReport{
				UserID:       user.ID.String(),
				UserName:     user.Name,
				Period:       period,
				PeriodLabel:  periodLabel,
				ReportType:   reportType,
				Title:        title,
				Content:      content,
				StatsSummary: statsSummary,
				CreatedAt:    randTime(90),
			}
			if err := database.DB.Create(&report).Error; err != nil {
				continue
			}
			reportCount++
		}
	}

	fmt.Printf("  ✓ 工作报告: %d 条\n", reportCount)
	return reportCount
}

func seedLedgerEntries() int {
	var count int64
	database.DB.Model(&models.LedgerEntry{}).Count(&count)
	if count > 0 {
		fmt.Printf("台账数据已存在，跳过 (共%d条)\n", count)
		return 0
	}

	users := loadUsers()
	notes := loadNotes()
	if len(notes) == 0 {
		fmt.Println("  跳过台账: 无任务记录")
		return 0
	}

	actions := []string{"create", "update", "assign", "complete", "remind", "archive", "tag", "comment"}
	actionDetails := map[string][]string{
		"create":   {"创建了新的工作任务", "发起一项协同工作", "新建一条工作记录"},
		"update":   {"更新了工作内容描述", "修改了任务截止时间", "调整了任务优先级"},
		"assign":   {"将任务指派给了新的协作者", "增加了任务协同人员", "变更了任务负责人"},
		"complete": {"标注任务为已完成", "提交了最终工作成果", "完成了任务并归档材料"},
		"remind":   {"向协作者发送了盯办提醒", "催办任务进度", "提醒任务即将逾期"},
		"archive":  {"将已完成任务归档", "归档历史工作记录"},
		"tag":      {"为任务添加了标签标记", "修改了任务标签分类"},
		"comment":  {"添加了工作备注说明", "发表了任务进展评论"},
	}

	entryCount := 0
	for _, note := range notes {
		nEntries := randBetween(1, 6)
		for i := 0; i < nEntries; i++ {
			action := pick(actions)
			detail := pick(actionDetails[action])
			user := users[rng.Intn(len(users))]
			entry := models.LedgerEntry{
				NoteID:       note.ID,
				UserID:       user.ID,
				Action:       action,
				ActionDetail: detail,
				IPAddress:    fmt.Sprintf("10.0.%d.%d", randBetween(1, 250), randBetween(1, 250)),
				UserAgent:    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
				CreatedAt:    note.CreatedAt.Add(time.Duration(randBetween(0, 72)) * time.Hour),
			}
			if err := database.DB.Create(&entry).Error; err != nil {
				continue
			}
			entryCount++
		}
	}

	fmt.Printf("  ✓ 台账: %d 条\n", entryCount)
	return entryCount
}

func seedOperationLogs() int {
	var count int64
	database.DB.Model(&models.OperationLog{}).Count(&count)
	if count > 0 {
		fmt.Printf("操作日志数据已存在，跳过 (共%d条)\n", count)
		return 0
	}

	users := loadUsers()
	logCount := 0

	type logTemplate struct {
		action string
		method string
		path   string
		detail string
	}
	templates := []logTemplate{
		{"用户登录", "POST", "/api/v1/auth/login", "用户通过账号密码成功登录系统"},
		{"创建任务", "POST", "/api/v1/notes", "在个人工作区新建了一条工作任务"},
		{"更新任务", "PUT", "/api/v1/notes/{id}", "修改了任务的基本信息或内容描述"},
		{"删除任务", "DELETE", "/api/v1/notes/{id}", "删除了一条不再需要的工作任务"},
		{"指派任务", "POST", "/api/v1/notes/{id}/assignees", "为协同任务新增了协作参与人员"},
		{"完成反馈", "PUT", "/api/v1/notes/{id}/feedback", "提交了任务完成情况反馈信息"},
		{"创建工作组", "POST", "/api/v1/groups", "发起了一个新的专项工作协同组"},
		{"加入工作组", "POST", "/api/v1/groups/{id}/members", "被添加为专项工作组的成员"},
		{"发送盯办", "POST", "/api/v1/reminders", "向任务协作者发送了盯办提醒通知"},
		{"生成报告", "POST", "/api/v1/reports/generate", "使用AI自动生成了个人工作报告"},
		{"更新模板", "PUT", "/api/v1/templates/{id}", "修改了系统模板的字段配置"},
		{"查询数据", "GET", "/api/v1/notes", "查询了工作任务的列表数据"},
		{"导出台账", "GET", "/api/v1/ledger/export", "导出了个人工作台账记录"},
		{"标签管理", "POST", "/api/v1/tags", "新增了一个自定义工作标签"},
		{"系统设置", "PUT", "/api/v1/admin/settings", "修改了系统全局配置参数"},
	}
	resources := []string{"note", "group", "reminder", "report", "template", "tag", "system", "ledger"}

	for i := 0; i < 65; i++ {
		tmpl := pick(templates)
		user := users[rng.Intn(len(users))]
		resource := pick(resources)
		resourceID := uuid.New().String()
		statusCode := 200
		if randomBool(0.05) {
			statusCode = pick([]int{400, 403, 404, 500})
		}
		log := models.OperationLog{
			UserID:     user.ID.String(),
			UserName:   user.Name,
			Role:       user.Role,
			Action:     tmpl.action,
			Method:     tmpl.method,
			Path:       tmpl.path,
			Resource:   resource,
			ResourceID: resourceID,
			Detail:     fmt.Sprintf("%s对%s执行了%s操作", user.Name, resource, tmpl.detail),
			StatusCode: statusCode,
			IPAddress:  fmt.Sprintf("10.0.%d.%d", randBetween(1, 250), randBetween(1, 250)),
			CreatedAt:  randTime(90),
		}
		if err := database.DB.Create(&log).Error; err != nil {
			continue
		}
		logCount++
	}

	fmt.Printf("  ✓ 操作日志: %d 条\n", logCount)
	return logCount
}