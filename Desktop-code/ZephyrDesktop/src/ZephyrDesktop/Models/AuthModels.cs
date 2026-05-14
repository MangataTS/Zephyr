using System.Text.Json.Serialization;

namespace ZephyrDesktop.Models;

public sealed class UserProfile
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Rank { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Avatar { get; set; } = "";
    [JsonPropertyName("dept_id")]
    public string DeptId { get; set; } = "";
    [JsonPropertyName("dept_name")]
    public string DeptName { get; set; } = "";
    public List<string> Permissions { get; set; } = [];
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
}

public sealed class LoginResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";
    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }
    public UserProfile? User { get; set; }
}

public sealed class TokenPair
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";
    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }
}
