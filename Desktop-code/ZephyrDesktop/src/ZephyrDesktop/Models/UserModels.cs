using System.Text.Json.Serialization;

namespace ZephyrDesktop.Models;

public class UserDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
}
