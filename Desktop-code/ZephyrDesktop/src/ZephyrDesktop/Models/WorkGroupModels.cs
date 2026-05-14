namespace ZephyrDesktop.Models;

using System.Text.Json.Serialization;

public sealed class CreateWorkGroupRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("template_type")]
    public string TemplateType { get; set; } = "default";

    [JsonPropertyName("due_time")]
    public string? DueTime { get; set; }

    [JsonPropertyName("members")]
    public List<GroupMemberRequest> Members { get; set; } = [];

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];
}

public sealed class GroupMemberRequest
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "member";

    [JsonPropertyName("sub_group")]
    public string SubGroup { get; set; } = "";
}

public sealed class WorkGroupResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("template_type")]
    public string TemplateType { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";
}