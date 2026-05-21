package services

import (
	"encoding/json"
	"strings"
	"time"

	"labelpro-server/internal/database"
	"labelpro-server/internal/models"
	"labelpro-server/internal/repository"
	apperrors "labelpro-server/pkg/errors"

	"github.com/google/uuid"
)

type NoteService struct {
	noteRepo *repository.NoteRepository
}

func NewNoteService(noteRepo *repository.NoteRepository) *NoteService {
	return &NoteService{noteRepo: noteRepo}
}

type CreateNoteRequest struct {
	Title        string               `json:"title" binding:"required"`
	SubTag       string               `json:"sub_tag"`
	Content      string               `json:"content"`
	ContentDelta string               `json:"content_delta"`
	TagIDs       []string             `json:"tags"`
	SourceType   string               `json:"source_type"`
	TemplateType string               `json:"template_type"`
	TemplateID   string               `json:"template_id"`
	OwnerID      string               `json:"owner_id"`
	DueTime      *time.Time           `json:"due_time"`
	Assignees    []AssigneeRequest    `json:"assignees"`
	GroupConfig  *GroupConfigRequest  `json:"group_config"`
	CanvasConfig *CanvasConfigRequest `json:"canvas_config"`
}

type AssigneeRequest struct {
	UserID     string `json:"user_id"`
	RoleInNote string `json:"role_in_note"`
}

type GroupConfigRequest struct {
	GroupName string            `json:"group_name"`
	SubGroups []SubGroupRequest `json:"sub_groups"`
}

type SubGroupRequest struct {
	Name      string   `json:"name"`
	LeaderID  string   `json:"leader_id"`
	MemberIDs []string `json:"member_ids"`
}

type CanvasConfigRequest struct {
	Columns     int      `json:"columns"`
	ColumnUsers []string `json:"column_users"`
}

type UpdateNoteRequest struct {
	Title        *string    `json:"title"`
	SubTag       *string    `json:"sub_tag"`
	Content      *string    `json:"content"`
	ContentDelta *string    `json:"content_delta"`
	TagIDs       *[]string  `json:"tags"`
	DueTime      *time.Time `json:"due_time"`
	ColorStatus  *string    `json:"color_status"`
	OwnerID      *string    `json:"owner_id"`
}

type CompleteNoteRequest struct {
	FeedbackContent string              `json:"feedback_content"`
	Attachments     []AttachmentRequest `json:"attachments"`
}

type AttachmentRequest struct {
	FileName string `json:"file_name"`
	FilePath string `json:"file_path"`
}

type RemindRequest struct {
	TargetID   string `json:"target_id" binding:"required"`
	Message    string `json:"message"`
	RemindType string `json:"remind_type"`
}

func (s *NoteService) Create(userID, role, deptID string, req CreateNoteRequest) (*models.Note, error) {
	creatorID, err := uuid.Parse(userID)
	if err != nil {
		return nil, err
	}

	sourceType := req.SourceType
	if sourceType == "" {
		sourceType = "self"
	}

	if role == "member" && sourceType == "assigned" {
		return nil, apperrors.ErrPermissionDenied
	}

	ownerID := creatorID
	if req.OwnerID != "" {
		oid, err := uuid.Parse(req.OwnerID)
		if err != nil {
			return nil, err
		}
		ownerID = oid
	}

	var deptUUID *uuid.UUID
	if deptID != "" {
		d, _ := uuid.Parse(deptID)
		deptUUID = &d
	}

	if req.TemplateID != "" {
		var tmpl models.Template
		if err := database.DB.First(&tmpl, "id = ?", req.TemplateID).Error; err == nil {
			var templateFields []map[string]interface{}
			if json.Unmarshal([]byte(tmpl.Fields), &templateFields) == nil {
				var fieldLines []string
				for _, f := range templateFields {
					if name, ok := f["name"].(string); ok {
						fieldLines = append(fieldLines, "【"+name+"】")
					}
				}
				if len(fieldLines) > 0 {
					templatePrefix := "📋 模板：" + tmpl.Name + "\n" + strings.Join(fieldLines, "\n") + "\n\n"
					req.Content = templatePrefix + req.Content
				}
			}
		}
	}

	now := time.Now()
	initialColorStatus := "yellow"
	if sourceType == "assigned" {
		initialColorStatus = "red"
	}
	if sourceType == "collaboration" {
		initialColorStatus = "blue"
	}

	note := &models.Note{
		Title:        req.Title,
		SubTag:       req.SubTag,
		Content:      req.Content,
		ContentDelta: req.ContentDelta,
		ColorStatus:  initialColorStatus,
		SourceType:   sourceType,
		TemplateType: req.TemplateType,
		CreatorID:    creatorID,
		OwnerID:      ownerID,
		DepartmentID: deptUUID,
		DueTime:      req.DueTime,
	}

	if note.TemplateType == "" {
		note.TemplateType = "default"
	}

	if len(req.TagIDs) > 0 {
		for _, tid := range req.TagIDs {
			id, err := uuid.Parse(tid)
			if err != nil {
				continue
			}
			note.Tags = append(note.Tags, models.Tag{ID: id})
		}
	}

	note.Assignees = append(note.Assignees, models.NoteAssignee{
		UserID:     creatorID,
		RoleInNote: "initiator",
	})

	for _, a := range req.Assignees {
		uid, err := uuid.Parse(a.UserID)
		if err != nil {
			continue
		}
		roleInNote := a.RoleInNote
		if roleInNote == "" {
			roleInNote = "member"
		}
		note.Assignees = append(note.Assignees, models.NoteAssignee{
			UserID:     uid,
			RoleInNote: roleInNote,
		})
	}

	year := now.Year()
	seq, _ := s.noteRepo.GetNextSerialNumber(year)
	note.SerialNo = repository.GenerateSerialNo(year, seq)

	if err := s.noteRepo.Create(note); err != nil {
		return nil, err
	}

	_ = s.recordLedger(note.ID.String(), userID, "create", "任务创建", "", "")

	return s.noteRepo.FindByID(note.ID.String())
}

func (s *NoteService) GetByID(id string) (*models.Note, error) {
	note, err := s.noteRepo.FindByID(id)
	if err != nil {
		return nil, err
	}
	if note == nil {
		return nil, apperrors.ErrNoteNotFound
	}
	return note, nil
}

func (s *NoteService) List(filter repository.NoteFilter, scope repository.NoteScope) ([]models.Note, int64, error) {
	return s.noteRepo.List(filter, scope)
}

func (s *NoteService) Update(id, userID string, req UpdateNoteRequest) (*models.Note, error) {
	note, err := s.noteRepo.FindByID(id)
	if err != nil || note == nil {
		return nil, apperrors.ErrNoteNotFound
	}

	if req.Title != nil {
		note.Title = *req.Title
	}
	if req.SubTag != nil {
		note.SubTag = *req.SubTag
	}
	if req.Content != nil {
		note.Content = *req.Content
	}
	if req.ContentDelta != nil {
		note.ContentDelta = *req.ContentDelta
	}
	if req.DueTime != nil {
		note.DueTime = req.DueTime
	}
	if req.ColorStatus != nil {
		note.ColorStatus = *req.ColorStatus
	}
	if req.OwnerID != nil {
		oid, err := uuid.Parse(*req.OwnerID)
		if err == nil {
			note.OwnerID = oid
		}
	}

	if req.TagIDs != nil {
		note.Tags = nil
		for _, tid := range *req.TagIDs {
			id, err := uuid.Parse(tid)
			if err != nil {
				continue
			}
			note.Tags = append(note.Tags, models.Tag{ID: id})
		}
	}

	if err := s.noteRepo.Update(note); err != nil {
		return nil, err
	}

	_ = s.recordLedger(id, userID, "update", "任务更新", "", "")

	return s.noteRepo.FindByID(id)
}

func (s *NoteService) Complete(id, userID, role string, req CompleteNoteRequest) (*models.Note, error) {
	note, err := s.noteRepo.FindByID(id)
	if err != nil || note == nil {
		return nil, apperrors.ErrNoteNotFound
	}

	isGroupNote := note.GroupID != nil
	if !isGroupNote && note.SourceType == "assigned" && note.OwnerID.String() != userID &&
		role != "super_admin" && role != "dept_admin" {
		return nil, apperrors.ErrPermissionDenied
	}

	now := time.Now()
	note.ColorStatus = "green"
	note.IsArchived = true
	note.ArchiveTime = &now
	note.CompletedAt = &now

	if err := s.noteRepo.Update(note); err != nil {
		return nil, err
	}

	_ = s.recordLedger(id, userID, "complete", "任务办结归档", req.FeedbackContent, "")

	return s.noteRepo.FindByID(id)
}

func (s *NoteService) Remind(id, userID string, req RemindRequest) (*models.Note, error) {
	note, err := s.noteRepo.FindByID(id)
	if err != nil || note == nil {
		return nil, apperrors.ErrNoteNotFound
	}

	reminderID, _ := uuid.Parse(userID)
	targetID, _ := uuid.Parse(req.TargetID)

	remindType := req.RemindType
	if remindType == "" {
		remindType = "normal"
	}

	now := time.Now()
	note.ColorStatus = "red"
	note.RemindCount++
	note.LastRemindAt = &now

	reminder := &models.Reminder{
		NoteID:     note.ID,
		ReminderID: reminderID,
		TargetID:   targetID,
		Message:    req.Message,
		RemindType: remindType,
	}

	if err := s.noteRepo.Update(note); err != nil {
		return nil, err
	}

	_ = s.noteRepo.CreateReminder(reminder)
	_ = s.recordLedger(id, userID, "remind", "盯办提醒", req.Message, "")

	return s.noteRepo.FindByID(id)
}

func (s *NoteService) Delete(id string, hardDelete bool) error {
	if hardDelete {
		return s.noteRepo.HardDelete(id)
	}
	return s.noteRepo.SoftDelete(id)
}

func (s *NoteService) Restore(id, userID string) (*models.Note, error) {
	if err := s.noteRepo.Restore(id); err != nil {
		return nil, err
	}
	_ = s.recordLedger(id, userID, "update", "任务恢复", "", "")
	return s.noteRepo.FindByID(id)
}

func (s *NoteService) recordLedger(noteID, userID, action, detail, ip, ua string) error {
	nid, _ := uuid.Parse(noteID)
	uid, _ := uuid.Parse(userID)

	entry := &models.LedgerEntry{
		NoteID:       nid,
		UserID:       uid,
		Action:       action,
		ActionDetail: detail,
		IPAddress:    ip,
		UserAgent:    ua,
	}
	return s.noteRepo.CreateLedger(entry)
}

type NoteStats struct {
	TotalNotes  int64                    `json:"total_notes"`
	ActiveNotes int64                    `json:"active_notes"`
	Trend       []repository.NoteDayStat `json:"trend"`
}

type NoteHeatmap struct {
	TotalArchived int64                    `json:"total_archived"`
	Year          int                      `json:"year"`
	Daily         []repository.NoteDayStat `json:"daily"`
}

func (s *NoteService) GetHeatmap(userID string, year int) (*NoteHeatmap, error) {
	total, err := s.noteRepo.CountArchivedByUser(userID)
	if err != nil {
		return nil, err
	}
	daily, err := s.noteRepo.HeatmapByYear(userID, year)
	if err != nil {
		return nil, err
	}
	return &NoteHeatmap{
		TotalArchived: total,
		Year:          year,
		Daily:         daily,
	}, nil
}

func (s *NoteService) GetStats(days int, deptID string, status string) (*NoteStats, error) {
	total, err := s.noteRepo.CountAll()
	if err != nil {
		return nil, err
	}
	active, err := s.noteRepo.CountActive()
	if err != nil {
		return nil, err
	}
	archivedOnly := status == "archived"
	var trend []repository.NoteDayStat
	if deptID != "" {
		trend, err = s.noteRepo.StatsByDayAndDept(days, deptID, archivedOnly)
	} else {
		trend, err = s.noteRepo.StatsByDay(days, archivedOnly)
	}
	if err != nil {
		return nil, err
	}
	return &NoteStats{
		TotalNotes:  total,
		ActiveNotes: active,
		Trend:       trend,
	}, nil
}
