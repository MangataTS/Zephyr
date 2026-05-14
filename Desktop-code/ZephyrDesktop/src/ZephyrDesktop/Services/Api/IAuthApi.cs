using Refit;
using ZephyrDesktop.Models;

namespace ZephyrDesktop.Services.Api;

[Headers("Content-Type: application/json")]
public interface IAuthApi
{
    [Post("/api/v1/auth/login")]
    Task<LoginResponse> LoginAsync([Body] LoginRequest request);

    [Post("/api/v1/auth/refresh")]
    Task<TokenPair> RefreshTokenAsync([Body] RefreshTokenRequest request);

    [Post("/api/v1/auth/logout")]
    Task LogoutAsync();

    [Get("/api/v1/auth/me")]
    Task<UserProfile> GetCurrentUserAsync();
}

public sealed class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string DeviceId { get; set; } = "desktop";
}

public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = "";
}
