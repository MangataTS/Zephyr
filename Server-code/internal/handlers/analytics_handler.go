package handlers

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"strconv"
	"strings"
	"time"

	"labelpro-server/internal/logger"
	"labelpro-server/internal/middleware"
	"labelpro-server/internal/models"
	"labelpro-server/internal/repository"
	"labelpro-server/internal/utils"

	"github.com/gin-gonic/gin"
	"github.com/google/uuid"
	"go.uber.org/zap"
)

type AnalyticsHandler struct {
	noteRepo *repository.NoteRepository
	sysRepo  *repository.SystemRepository
}

func NewAnalyticsHandler(noteRepo *repository.NoteRepository, sysRepo *repository.SystemRepository) *AnalyticsHandler {
	return &AnalyticsHandler{noteRepo: noteRepo, sysRepo: sysRepo}
}

func (h *AnalyticsHandler) PersonalStats(c *gin.Context) {
	userID := middleware.GetUserID(c)
	period := c.DefaultQuery("period", "week")

	days := 7
	switch period {
	case "month":
		days = 30
	case "year":
		days = 365
	}

	stats, err := h.noteRepo.GetPersonalStats(userID, days)
	if err != nil {
		utils.InternalError(c, "查询统计数据失败")
		return
	}

	utils.Success(c, stats)
}

type ReportRequest struct {
	Period string `json:"period"`
}

func (h *AnalyticsHandler) GenerateAIReport(c *gin.Context) {
	var body ReportRequest
	if err := c.ShouldBindJSON(&body); err != nil {
		body.Period = "week"
	}

	userID := middleware.GetUserID(c)
	userName := c.GetString("username")
	period := body.Period
	if period == "" {
		period = "week"
	}

	days := 7
	periodLabel := "本周"
	titlePrefix := "本周"
	switch period {
	case "month":
		days = 30
		periodLabel = "本月"
		titlePrefix = "本月"
	case "year":
		days = 365
		periodLabel = "本年度"
		titlePrefix = "本年度"
	}

	stats, err := h.noteRepo.GetPersonalStats(userID, days)
	if err != nil {
		utils.InternalError(c, "查询统计数据失败")
		return
	}

	statsJSON, _ := json.Marshal(stats)

	reportType := "template"
	var reportContent string

	configs, cfgErr := h.sysRepo.ListAIConfigs()
	if cfgErr == nil && len(configs) > 0 {
		var activeConfig *struct {
			endpoint string
			apiKey   string
			model    string
		}
		for _, cfg := range configs {
			if cfg.IsActive {
				decryptedKey, decErr := utils.DecryptAES(cfg.APIKey)
				if decErr != nil {
					continue
				}
				activeConfig = &struct {
					endpoint string
					apiKey   string
					model    string
				}{
					endpoint: cfg.APIEndpoint,
					apiKey:   decryptedKey,
					model:    cfg.ModelName,
				}
				break
			}
		}

		if activeConfig != nil {
			modelName := activeConfig.model
			if modelName == "" {
				modelName = "gpt-3.5-turbo"
			}
			prompt := buildReportPrompt(userName, periodLabel, stats, period)
			aiReport, aiErr := callAIService(activeConfig.endpoint, activeConfig.apiKey, modelName, prompt, 4096)
			if aiErr == nil {
				reportContent = aiReport
				reportType = "ai"
			}
		}
	}

	if reportContent == "" {
		reportContent = h.buildTemplateReport(userName, periodLabel, stats)
	}

	title := fmt.Sprintf("%s工作成效报告 - %s", titlePrefix, time.Now().Format("2006-01-02 15:04"))

	report := &models.WorkReport{
		ID:           uuid.New(),
		UserID:       userID,
		UserName:     userName,
		Period:       period,
		PeriodLabel:  periodLabel,
		ReportType:   reportType,
		Title:        title,
		Content:      reportContent,
		StatsSummary: string(statsJSON),
	}
	_ = h.sysRepo.CreateWorkReport(report)

	utils.Success(c, gin.H{
		"report_id":     report.ID.String(),
		"period":        period,
		"period_label":  periodLabel,
		"report_type":   reportType,
		"stats":         stats,
		"report":        reportContent,
		"generated_at":  report.CreatedAt.Format(time.RFC3339),
	})
}

func (h *AnalyticsHandler) ListReports(c *gin.Context) {
	userID := middleware.GetUserID(c)
	page, _ := strconv.Atoi(c.DefaultQuery("page", "1"))
	pageSize, _ := strconv.Atoi(c.DefaultQuery("page_size", "20"))
	if page < 1 {
		page = 1
	}
	if pageSize < 1 || pageSize > 100 {
		pageSize = 20
	}

	f := repository.WorkReportFilter{
		UserID:   userID,
		Period:   c.Query("period"),
		Keyword:  c.Query("keyword"),
		DateFrom: c.Query("date_from"),
		DateTo:   c.Query("date_to"),
		Page:     page,
		PageSize: pageSize,
	}

	reports, total, err := h.sysRepo.ListWorkReports(f)
	if err != nil {
		utils.InternalError(c, "获取报告列表失败")
		return
	}
	if reports == nil {
		reports = []models.WorkReport{}
	}

	utils.Paginated(c, reports, total, page, pageSize)
}

func (h *AnalyticsHandler) GetReport(c *gin.Context) {
	id := c.Param("id")
	report, err := h.sysRepo.GetWorkReport(id)
	if err != nil {
		utils.NotFound(c, "报告不存在")
		return
	}
	utils.Success(c, report)
}

func (h *AnalyticsHandler) DeleteReport(c *gin.Context) {
	id := c.Param("id")
	if err := h.sysRepo.DeleteWorkReport(id); err != nil {
		utils.InternalError(c, "删除报告失败")
		return
	}
	utils.SuccessWithMessage(c, "报告已删除", nil)
}

func (h *AnalyticsHandler) buildTemplateReport(userName, periodLabel string, stats *repository.PersonalStats) string {
	tagList := ""
	for _, t := range stats.TagBreakdown {
		tagList += fmt.Sprintf("- **%s**：%d 次\n", t.TagName, t.Count)
	}
	if tagList == "" {
		tagList = "- 暂无标签数据\n"
	}

	dailyTrend := ""
	for _, d := range stats.DailyTrend {
		dailyTrend += fmt.Sprintf("- %s：创建 %d 条任务\n", d.Date, d.Count)
	}
	if dailyTrend == "" {
		dailyTrend = "- 暂无日趋势数据\n"
	}

	completionDesc := "工作完成情况良好"
	if stats.CompletionRate < 30 {
		completionDesc = "工作任务完成率较低，建议加强任务推进力度"
	} else if stats.CompletionRate < 60 {
		completionDesc = "工作完成率有待提升，建议合理规划任务优先级"
	} else if stats.CompletionRate < 80 {
		completionDesc = "工作推进较为稳健，完成率处于中等水平"
	}

	remindDesc := ""
	if stats.RemindReceived == 0 {
		remindDesc = "期间未被盯办，任务推进及时有效"
	} else if stats.RemindReceived <= 2 {
		remindDesc = fmt.Sprintf("期间被盯办 %d 次，建议关注任务时效性", stats.RemindReceived)
	} else {
		remindDesc = fmt.Sprintf("期间被盯办 %d 次，需重点改善任务执行效率", stats.RemindReceived)
	}

	template := getDefaultTemplate()
	tpl, err := h.sysRepo.GetReportTemplate("default")
	if err == nil && tpl.Content != "" {
		template = tpl.Content
	}

	report := template
	report = strings.ReplaceAll(report, "{{userName}}", userName)
	report = strings.ReplaceAll(report, "{{periodLabel}}", periodLabel)
	report = strings.ReplaceAll(report, "{{totalCreated}}", strconv.FormatInt(stats.TotalCreated, 10))
	report = strings.ReplaceAll(report, "{{totalCompleted}}", strconv.FormatInt(stats.TotalCompleted, 10))
	report = strings.ReplaceAll(report, "{{completionRate}}", fmt.Sprintf("%.1f", stats.CompletionRate))
	report = strings.ReplaceAll(report, "{{completionDesc}}", completionDesc)
	report = strings.ReplaceAll(report, "{{remindDesc}}", remindDesc)
	report = strings.ReplaceAll(report, "{{remindReceived}}", strconv.FormatInt(stats.RemindReceived, 10))
	report = strings.ReplaceAll(report, "{{avgCompletionHours}}", fmt.Sprintf("%.1f", stats.AvgCompletionHours))
	report = strings.ReplaceAll(report, "{{tagList}}", tagList)
	report = strings.ReplaceAll(report, "{{dailyTrend}}", dailyTrend)
	report = strings.ReplaceAll(report, "{{activeTagMsg}}", getActiveTagMsg(stats))
	return report
}

func getDefaultTemplate() string {
	return `## 工作概览

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
*本报告由系统自动生成，基于实际工作数据统计分析*`
}

func getActiveTagMsg(stats *repository.PersonalStats) string {
	if len(stats.TagBreakdown) > 0 {
		return fmt.Sprintf("- 最活跃标签为「%s」，共使用 %d 次", stats.TagBreakdown[0].TagName, stats.TagBreakdown[0].Count)
	}
	return ""
}

func (h *AnalyticsHandler) GetReportTemplate(c *gin.Context) {
	tpl, err := h.sysRepo.GetReportTemplate("default")
	if err != nil {
		tpl = &models.ReportTemplate{
			ID:      "default",
			Name:    "默认报告模板",
			Content: getDefaultTemplate(),
		}
		_ = h.sysRepo.SaveReportTemplate(tpl)
	}
	utils.Success(c, tpl)
}

func (h *AnalyticsHandler) SaveReportTemplate(c *gin.Context) {
	var body struct {
		Content string `json:"content" binding:"required"`
		Name    string `json:"name"`
	}
	if err := c.ShouldBindJSON(&body); err != nil {
		utils.BadRequest(c, "请提供模板内容")
		return
	}
	if body.Name == "" {
		body.Name = "默认报告模板"
	}

	tpl := &models.ReportTemplate{
		ID:      "default",
		Name:    body.Name,
		Content: body.Content,
	}
	if err := h.sysRepo.SaveReportTemplate(tpl); err != nil {
		utils.InternalError(c, "保存模板失败")
		return
	}
	utils.SuccessWithMessage(c, "模板保存成功", tpl)
}

func (h *AnalyticsHandler) GenerateDailyReport(c *gin.Context) {
	userID := middleware.GetUserID(c)
	userName := c.GetString("username")
	now := time.Now()
	todayStart := time.Date(now.Year(), now.Month(), now.Day(), 0, 0, 0, 0, now.Location())
	h.generatePeriodReport(c, userID, userName, "day", "今日", todayStart, now)
}

func (h *AnalyticsHandler) GenerateWeeklyReport(c *gin.Context) {
	userID := middleware.GetUserID(c)
	userName := c.GetString("username")
	now := time.Now()
	weekday := now.Weekday()
	if weekday == time.Sunday {
		weekday = 7
	}
	daysFromMonday := int(weekday) - int(time.Monday)
	weekStart := time.Date(now.Year(), now.Month(), now.Day()-daysFromMonday, 0, 0, 0, 0, now.Location())
	h.generatePeriodReport(c, userID, userName, "week", "本周", weekStart, now)
}

func (h *AnalyticsHandler) GenerateMonthlyReport(c *gin.Context) {
	userID := middleware.GetUserID(c)
	userName := c.GetString("username")
	now := time.Now()
	monthStart := time.Date(now.Year(), now.Month(), 1, 0, 0, 0, 0, now.Location())
	h.generatePeriodReport(c, userID, userName, "month", "本月", monthStart, now)
}

func (h *AnalyticsHandler) generatePeriodReport(c *gin.Context, userID, userName, period, periodLabel string, since, now time.Time) {
	days := int(now.Sub(since).Hours()/24) + 1
	stats, err := h.noteRepo.GetPersonalStats(userID, days)
	if err != nil {
		utils.InternalError(c, "查询统计数据失败")
		return
	}

	sourceStats := h.getSourceTypeDistribution(userID, since)
	tagSummary := buildTagSummary(stats.TagBreakdown)
	sourceSummary := buildSourceSummary(sourceStats)
	dateRange := fmt.Sprintf("%s ~ %s", since.Format("2006-01-02"), now.Format("2006-01-02"))

	reportType := "template"
	var reportContent string

	configs, cfgErr := h.sysRepo.ListAIConfigs()
	if cfgErr == nil && len(configs) > 0 {
		var activeEndpoint, activeAPIKey, activeModel string
		for _, cfg := range configs {
			if cfg.IsActive {
				decryptedKey, decErr := utils.DecryptAES(cfg.APIKey)
				if decErr != nil {
					continue
				}
				activeEndpoint = cfg.APIEndpoint
				activeAPIKey = decryptedKey
				activeModel = cfg.ModelName
				break
			}
		}
		if activeEndpoint != "" {
			if activeModel == "" {
				activeModel = "gpt-3.5-turbo"
			}
			prompt := buildPeriodReportPrompt(userName, periodLabel, dateRange, stats, tagSummary, sourceSummary)
			aiReport, aiErr := callAIService(activeEndpoint, activeAPIKey, activeModel, prompt, 4096)
			if aiErr == nil {
				reportContent = aiReport
				reportType = "ai"
			}
		}
	}

	if reportContent == "" {
		completionDesc := "工作完成情况良好"
		if stats.CompletionRate < 30 {
			completionDesc = "工作任务完成率较低，建议加强任务推进力度"
		} else if stats.CompletionRate < 60 {
			completionDesc = "工作完成率有待提升，建议合理规划任务优先级"
		} else if stats.CompletionRate < 80 {
			completionDesc = "工作推进较为稳健，完成率处于中等水平"
		}

		template := getDefaultReportTemplateForPeriod()
		tpl, err := h.sysRepo.GetReportTemplate("default")
		if err == nil && tpl.Content != "" {
			template = tpl.Content
		}

		reportContent = template
		reportContent = strings.ReplaceAll(reportContent, "{{userName}}", userName)
		reportContent = strings.ReplaceAll(reportContent, "{{periodLabel}}", periodLabel)
		reportContent = strings.ReplaceAll(reportContent, "{{totalCreated}}", strconv.FormatInt(stats.TotalCreated, 10))
		reportContent = strings.ReplaceAll(reportContent, "{{totalCompleted}}", strconv.FormatInt(stats.TotalCompleted, 10))
		reportContent = strings.ReplaceAll(reportContent, "{{completionRate}}", fmt.Sprintf("%.1f", stats.CompletionRate))
		reportContent = strings.ReplaceAll(reportContent, "{{completionDesc}}", completionDesc)
		reportContent = strings.ReplaceAll(reportContent, "{{tagSummary}}", tagSummary)
		reportContent = strings.ReplaceAll(reportContent, "{{sourceSummary}}", sourceSummary)
		reportContent = strings.ReplaceAll(reportContent, "{{dateRange}}", fmt.Sprintf("%s ~ %s", since.Format("01-02"), now.Format("01-02")))
	}

	statsJSON, _ := json.Marshal(map[string]interface{}{
		"stats":        stats,
		"source_stats": sourceStats,
	})

	title := fmt.Sprintf("%s工作报告 - %s", periodLabel, now.Format("2006-01-02 15:04"))

	workReport := &models.WorkReport{
		ID:           uuid.New(),
		UserID:       userID,
		UserName:     userName,
		Period:       period,
		PeriodLabel:  periodLabel,
		ReportType:   reportType,
		Title:        title,
		Content:      reportContent,
		StatsSummary: string(statsJSON),
	}
	_ = h.sysRepo.CreateWorkReport(workReport)

	utils.Success(c, gin.H{
		"report_id":     workReport.ID.String(),
		"period":        period,
		"period_label":  periodLabel,
		"report_type":   reportType,
		"stats":         stats,
		"source_stats":  sourceStats,
		"report":        reportContent,
		"generated_at":  workReport.CreatedAt.Format(time.RFC3339),
	})
}

func (h *AnalyticsHandler) getSourceTypeDistribution(userID string, since time.Time) []repository.SourceTypeStat {
	results, _ := h.noteRepo.SourceTypeDistribution(userID, since)
	return results
}

func buildTagSummary(breakdown []repository.TagBreakdown) string {
	if len(breakdown) == 0 {
		return "- 暂无标签数据"
	}
	var lines []string
	for _, t := range breakdown {
		lines = append(lines, fmt.Sprintf("- **%s**：%d 条", t.TagName, t.Count))
	}
	return strings.Join(lines, "\n")
}

func buildSourceSummary(stats []repository.SourceTypeStat) string {
	if len(stats) == 0 {
		return "- 暂无来源类型数据"
	}
	var lines []string
	for _, s := range stats {
		label := s.SourceType
		switch s.SourceType {
		case "self":
			label = "自主创建"
		case "assigned":
			label = "上级交办"
		default:
			label = s.SourceType
		}
		lines = append(lines, fmt.Sprintf("- **%s**：%d 条", label, s.Count))
	}
	return strings.Join(lines, "\n")
}

func getDefaultReportTemplateForPeriod() string {
	return `## 工作概览

{{userName}}（{{periodLabel}}，{{dateRange}}）共创建任务 **{{totalCreated}}** 条，完成 **{{totalCompleted}}** 条，完成率为 **{{completionRate}}%**。{{completionDesc}}。

## 任务分类统计

{{tagSummary}}

## 来源类型分布

{{sourceSummary}}

## 总结

{{periodLabel}}期间共创建 {{totalCreated}} 条任务，其中自主创建和上级交办任务分布如上所示。需要在后续工作中继续保持任务推进的节奏，关注高优先级事项的处理。

---
*本报告由系统自动生成，基于实际工作数据统计分析*`
}

func buildReportPrompt(userName, periodLabel string, stats *repository.PersonalStats, period string) string {
	tagList := ""
	for i, t := range stats.TagBreakdown {
		if i > 0 {
			tagList += ", "
		}
		tagList += fmt.Sprintf("%s(%d次)", t.TagName, t.Count)
	}
	if tagList == "" {
		tagList = "无标签数据"
	}

	dailyTrendDesc := ""
	for _, d := range stats.DailyTrend {
		dailyTrendDesc += fmt.Sprintf("%s: %d条\n", d.Date, d.Count)
	}
	if dailyTrendDesc == "" {
		dailyTrendDesc = "无日趋势数据"
	}

	return fmt.Sprintf(`你是一位专业的工作效能分析师。请根据以下数据，为%s生成一份%s的结构化个人工作报告。

## 统计数据
- 周期：%s
- 创建任务总数：%d
- 完成任务数：%d
- 完成率：%.1f%%
- 被盯办次数：%d
- 平均完成耗时：%.1f 小时

## 标签使用分布
%s

## 每日任务趋势
%s

## 要求
请生成一份包含以下部分的报告（使用 Markdown 格式）：
1. **工作概览**：用一段话总结整体表现
2. **数据分析**：解读关键数字的含义和趋势
3. **趋势总结**：分析变化趋势
4. **成果亮点**：指出值得肯定的成绩
5. **改进建议**：提出针对性的改进方向

报告语言使用中文，语气专业且鼓励性。直接输出报告内容，不需要前言。`,
		userName, periodLabel, periodLabel,
		stats.TotalCreated,
		stats.TotalCompleted,
		stats.CompletionRate,
		stats.RemindReceived,
		stats.AvgCompletionHours,
		tagList,
		dailyTrendDesc,
	)
}

func buildPeriodReportPrompt(userName, periodLabel, dateRange string, stats *repository.PersonalStats, tagSummary, sourceSummary string) string {
	dailyTrendDesc := ""
	for _, d := range stats.DailyTrend {
		dailyTrendDesc += fmt.Sprintf("%s: %d条\n", d.Date, d.Count)
	}
	if dailyTrendDesc == "" {
		dailyTrendDesc = "无日趋势数据"
	}

	return fmt.Sprintf(`你是一位专业的工作效能分析师。请根据以下数据，为%s生成一份%s（%s）的结构化个人工作报告。

## 统计数据
- 周期：%s
- 日期范围：%s
- 创建任务总数：%d
- 完成任务数：%d
- 完成率：%.1f%%
- 被盯办次数：%d
- 平均完成耗时：%.1f 小时

## 任务分类统计
%s

## 来源类型分布
%s

## 每日任务趋势
%s

## 要求
请生成一份专业的工作报告（使用 Markdown 格式），包含以下部分：
1. **工作概览**：用一段话总结%s的整体工作表现，突出关键数据
2. **任务分类分析**：解读任务标签分布，说明工作重点集中在哪些领域
3. **来源类型分析**：分析自主创建与上级交办任务的占比，说明工作主动性与被动性
4. **趋势总结**：分析每日任务变化趋势
5. **成果亮点**：指出值得肯定的成绩
6. **改进建议**：提出针对性的改进方向

报告语言使用中文，语气专业且鼓励性。直接输出报告内容，不需要前言。`,
		userName, periodLabel, dateRange,
		periodLabel, dateRange,
		stats.TotalCreated,
		stats.TotalCompleted,
		stats.CompletionRate,
		stats.RemindReceived,
		stats.AvgCompletionHours,
		tagSummary,
		sourceSummary,
		dailyTrendDesc,
		periodLabel,
	)
}

type chatMessage struct {
	Role    string `json:"role"`
	Content string `json:"content"`
}

type chatRequest struct {
	Model       string        `json:"model"`
	Messages    []chatMessage `json:"messages"`
	MaxTokens   int           `json:"max_tokens,omitempty"`
}

type chatResponse struct {
	Choices []struct {
		Message struct {
			Content string `json:"content"`
		} `json:"message"`
	} `json:"choices"`
}

func callAIService(endpoint, apiKey, model, prompt string, maxTokens int) (string, error) {
	logger.Debug("callAIService starting",
		zap.String("model", model),
		zap.Int("prompt_len", len(prompt)),
		zap.Int("max_tokens", maxTokens),
		zap.String("endpoint_prefix", endpoint[:minInt(len(endpoint), 50)]),
	)

	reqBody := chatRequest{
		Model:     model,
		Messages: []chatMessage{
			{Role: "user", Content: prompt},
		},
		MaxTokens: maxTokens,
	}

	jsonBody, err := json.Marshal(reqBody)
	if err != nil {
		logger.Error("callAIService marshal failed", zap.Error(err))
		return "", fmt.Errorf("请求序列化失败: %w", err)
	}

	url := endpoint
	if !strings.HasSuffix(url, "/chat/completions") {
		url = strings.TrimRight(url, "/") + "/chat/completions"
	}
	req, err := http.NewRequest("POST", url, bytes.NewReader(jsonBody))
	if err != nil {
		logger.Error("callAIService create request failed", zap.Error(err))
		return "", fmt.Errorf("创建请求失败: %w", err)
	}

	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("Authorization", "Bearer "+apiKey)

	client := &http.Client{
		Timeout: 180 * time.Second,
		Transport: &http.Transport{
			DialContext:         (&net.Dialer{Timeout: 15 * time.Second}).DialContext,
			TLSHandshakeTimeout: 15 * time.Second,
		},
	}
	resp, err := client.Do(req)
	if err != nil {
		logger.Error("callAIService HTTP request failed",
			zap.Error(err),
			zap.String("url", url[:minInt(len(url), 80)]),
			zap.Int("body_len", len(jsonBody)),
		)
		return "", fmt.Errorf("AI服务请求失败: %w", err)
	}
	defer resp.Body.Close()

	respBody, _ := io.ReadAll(resp.Body)
	if resp.StatusCode != 200 {
		errBody := string(respBody)
		if len(errBody) > 1000 {
			errBody = errBody[:1000]
		}
		logger.Error("callAIService non-200 response",
			zap.Int("status_code", resp.StatusCode),
			zap.String("response_body", errBody),
			zap.Int("prompt_len", len(prompt)),
		)
		return "", fmt.Errorf("AI服务返回错误 (状态码 %d): %s", resp.StatusCode, errBody)
	}

	var chatResp chatResponse
	if err := json.Unmarshal(respBody, &chatResp); err != nil {
		logger.Error("callAIService unmarshal response failed",
			zap.Error(err),
			zap.Int("resp_len", len(respBody)),
		)
		return "", fmt.Errorf("AI响应解析失败: %w", err)
	}

	if len(chatResp.Choices) == 0 {
		logger.Error("callAIService empty choices",
			zap.Int("resp_len", len(respBody)),
		)
		return "", fmt.Errorf("AI返回空响应")
	}

	logger.Debug("callAIService succeeded",
		zap.Int("response_len", len(chatResp.Choices[0].Message.Content)),
	)
	return chatResp.Choices[0].Message.Content, nil
}

func minInt(a, b int) int {
	if a < b {
		return a
	}
	return b
}
