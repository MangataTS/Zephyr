using System.Text.Json.Serialization;

namespace ZephyrDesktop.Models;

public sealed class GroupDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    [JsonPropertyName("template_type")]
    public string TemplateType { get; set; } = "default";
    [JsonPropertyName("creator_id")]
    public string CreatorId { get; set; } = "";
    [JsonPropertyName("member_count")]
    public int MemberCount { get; set; }
    [JsonPropertyName("note_count")]
    public int NoteCount { get; set; }
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class GroupDetailDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    [JsonPropertyName("template_type")]
    public string TemplateType { get; set; } = "default";
    [JsonPropertyName("creator_id")]
    public string CreatorId { get; set; } = "";
    public List<GroupMemberDto> Members { get; set; } = [];
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class GroupMemberDto
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = "";
    [JsonPropertyName("role_in_group")]
    public string RoleInGroup { get; set; } = "member";
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";
}
