package models

import (
	"time"

	"github.com/google/uuid"
	"gorm.io/gorm"
)

type Note struct {
	ID           uuid.UUID      `gorm:"type:uuid;primaryKey;default:gen_random_uuid()" json:"id"`
	Title        string         `gorm:"type:varchar(200);not null" json:"title"`
	SubTag       string         `gorm:"type:varchar(100);default:''" json:"sub_tag"`
	Content      string         `gorm:"type:text" json:"content"`
	ContentDelta string         `gorm:"type:jsonb;default:'{}'" json:"content_delta,omitempty"`
	ColorStatus  string         `gorm:"type:varchar(20);default:'yellow'" json:"color_status"`
	SourceType   string         `gorm:"type:varchar(20);default:'self'" json:"source_type"`
	TemplateType string         `gorm:"type:varchar(30);default:'default'" json:"template_type"`
	CreatorID    uuid.UUID      `gorm:"type:uuid;not null;index" json:"creator_id"`
	Creator      *User          `gorm:"foreignKey:CreatorID" json:"creator,omitempty"`
	OwnerID      uuid.UUID      `gorm:"type:uuid;not null;index" json:"owner_id"`
	Owner        *User          `gorm:"foreignKey:OwnerID" json:"owner,omitempty"`
	AssignerID   *uuid.UUID     `gorm:"type:uuid" json:"assigner_id"`
	Assigner     *User          `gorm:"foreignKey:AssignerID" json:"assigner,omitempty"`
	DepartmentID *uuid.UUID     `gorm:"type:uuid;index" json:"dept_id"`
	Department   *Department    `gorm:"foreignKey:DepartmentID" json:"department,omitempty"`
	GroupID      *uuid.UUID     `gorm:"type:uuid" json:"group_id"`
	IsArchived   bool           `gorm:"default:false;index" json:"is_archived"`
	ArchiveTime  *time.Time     `json:"archive_time"`
	DueTime      *time.Time     `json:"due_time"`
	CompletedAt  *time.Time     `json:"completed_at"`
	RemindCount  int            `gorm:"default:0" json:"remind_count"`
	LastRemindAt *time.Time     `json:"last_remind_at"`
	SerialNo     string         `gorm:"type:varchar(50)" json:"serial_no"`
	CreatedAt    time.Time      `json:"created_at"`
	UpdatedAt    time.Time      `json:"updated_at"`
	DeletedAt    gorm.DeletedAt `gorm:"index" json:"-"`

	Tags        []Tag            `gorm:"many2many:note_tags;" json:"tags,omitempty"`
	Assignees   []NoteAssignee   `gorm:"foreignKey:NoteID" json:"assignees,omitempty"`
	Attachments []NoteAttachment `gorm:"foreignKey:NoteID" json:"attachments,omitempty"`
	Group       *WorkGroup       `gorm:"foreignKey:GroupID" json:"group,omitempty"`
	Reminders   []Reminder       `gorm:"foreignKey:NoteID" json:"reminders,omitempty"`
}

func (Note) TableName() string {
	return "notes"
}

type NoteAssignee struct {
	NoteID          uuid.UUID  `gorm:"type:uuid;primaryKey" json:"note_id"`
	UserID          uuid.UUID  `gorm:"type:uuid;primaryKey" json:"user_id"`
	User            *User      `gorm:"foreignKey:UserID" json:"user,omitempty"`
	RoleInNote      string     `gorm:"type:varchar(20);default:'member'" json:"role_in_note"`
	FeedbackContent string     `gorm:"type:text" json:"feedback_content"`
	FeedbackAt      *time.Time `json:"feedback_at"`
	IsRead          bool       `gorm:"default:false" json:"is_read"`
}

func (NoteAssignee) TableName() string {
	return "note_assignees"
}

type NoteAttachment struct {
	ID       uuid.UUID `gorm:"type:uuid;primaryKey;default:gen_random_uuid()" json:"id"`
	NoteID   uuid.UUID `gorm:"type:uuid;not null;index" json:"note_id"`
	FileName string    `gorm:"type:varchar(255)" json:"file_name"`
	FilePath string    `gorm:"type:varchar(500)" json:"file_path"`
	FileSize int64     `json:"file_size"`
	MimeType string    `gorm:"type:varchar(100)" json:"mime_type"`
}

func (NoteAttachment) TableName() string {
	return "note_attachments"
}
