using System.Text.Json.Serialization;

namespace ZephyrDesktop.Models;

public enum SyncOperationType
{
    None = 0,
    Created = 1,
    Updated = 2
}

public sealed class NoteDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    [JsonPropertyName("content_delta")]
    public string ContentDelta { get; set; } = "{}";
    [JsonPropertyName("color_status")]
    public string ColorStatus { get; set; } = "yellow";
    [JsonPropertyName("source_type")]
    public string SourceType { get; set; } = "self";
    [JsonPropertyName("template_type")]
    public string TemplateType { get; set; } = "default";
    [JsonPropertyName("creator_id")]
    public string CreatorId { get; set; } = "";
    [JsonPropertyName("owner_id")]
    public string OwnerIds { get; set; } = "";
    [JsonPropertyName("dept_id")]
    public string? DepartmentId { get; set; }
    [JsonPropertyName("group_id")]
    public string? GroupId { get; set; }
    [JsonPropertyName("is_archived")]
    public bool IsArchived { get; set; }
    [JsonPropertyName("serial_no")]
    public string SerialNo { get; set; } = "";
    [JsonPropertyName("sub_tag")]
    public string SubTag { get; set; } = "";
    public List<TagDto> Tags { get; set; } = [];
    [JsonPropertyName("due_time")]
    public DateTime? DueTime { get; set; }
    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }
    [JsonPropertyName("remind_count")]
    public int RemindCount { get; set; }
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class TagDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
}

public sealed class CreateNoteRequest
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    [JsonPropertyName("source_type")]
    public string SourceType { get; set; } = "self";
    [JsonPropertyName("template_type")]
    public string TemplateType { get; set; } = "default";
    [JsonPropertyName("owner_id")]
    public string OwnerIds { get; set; } = "";
    [JsonPropertyName("due_time")]
    public DateTime? DueTime { get; set; }
    [JsonPropertyName("sub_tag")]
    public string? SubTag { get; set; }
}

public sealed class UpdateNoteRequest
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public List<string>? Tags { get; set; }
    [JsonPropertyName("color_status")]
    public string? ColorStatus { get; set; }
    [JsonPropertyName("owner_id")]
    public string? OwnerIds { get; set; }
    [JsonPropertyName("due_time")]
    public DateTime? DueTime { get; set; }
    [JsonPropertyName("sub_tag")]
    public string? SubTag { get; set; }
}

public sealed class CompleteNoteRequest
{
    [JsonPropertyName("feedback_content")]
    public string FeedbackContent { get; set; } = "";
}

public sealed class RemindRequest
{
    [JsonPropertyName("target_id")]
    public string TargetId { get; set; } = "";
    public string Message { get; set; } = "";
    [JsonPropertyName("remind_type")]
    public string RemindType { get; set; } = "normal";
}

public sealed class NoteStatsDto
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }
    [JsonPropertyName("active_count")]
    public int ActiveCount { get; set; }
    [JsonPropertyName("completed_count")]
    public int CompletedCount { get; set; }
    [JsonPropertyName("archived_count")]
    public int ArchivedCount { get; set; }
}
