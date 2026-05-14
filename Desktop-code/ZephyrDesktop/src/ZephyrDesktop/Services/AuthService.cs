using System.Net.Http;
using Serilog;
using ZephyrDesktop.Models;
using ZephyrDesktop.Services.Api;

namespace ZephyrDesktop.Services;

public sealed class AuthService
{
    private readonly IAuthApi _authApi;
    private readonly TokenStorage _tokenStorage;

    public AuthService(IAuthApi authApi, TokenStorage tokenStorage)
    {
        _authApi = authApi;
        _tokenStorage = tokenStorage;
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        Log.Information("[{Step}] {Message}", "Auth", $"LoginAsync 开始, username={username}");
        try
        {
            var result = await _authApi.LoginAsync(new LoginRequest { Username = username, Password = password });
            if (result == null)
            {
                Log.Information("[{Step}] {Message}", "Auth", "IAuthApi.LoginAsync 返回 null");
                throw new Exception("登录请求失败：服务器未返回数据");
            }

            Log.Information("[{Step}] {Message}", "Auth", $"IAuthApi.LoginAsync 返回成功, AccessToken长度={result.AccessToken?.Length}, RefreshToken长度={result.RefreshToken?.Length}, ExpiresIn={result.ExpiresIn}, User={result.User?.Name}");

            await _tokenStorage.SaveTokensAsync(result.AccessToken, result.RefreshToken, result.ExpiresIn);
            Log.Information("[{Step}] {Message}", "Auth", "Token 已保存");

            return result;
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "[{Step}]", "Auth.HttpRequest");
            throw new Exception($"无法连接到服务器，请检查网络连接（{ex.StatusCode ?? System.Net.HttpStatusCode.ServiceUnavailable}）", ex);
        }
        catch (Refit.ApiException ex)
        {
            Log.Error(ex, "[{Step}]", "Auth.ApiException");
            var msg = ex.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? "用户名或密码错误"
                : ex.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? "账号已被禁用"
                    : $"登录失败: {ex.Message}";
            throw new Exception(msg, ex);
        }
        catch (TaskCanceledException ex)
        {
            Log.Error(ex, "[{Step}]", "Auth.Timeout");
            throw new Exception("登录请求超时，请检查网络连接");
        }
        catch (Exception ex) when (ex is not HttpRequestException and not Refit.ApiException and not TaskCanceledException)
        {
            Log.Error(ex, "[{Step}]", "Auth.Unexpected");
            throw;
        }
    }

    public async Task LogoutAsync()
    {
        try { await _authApi.LogoutAsync(); } catch { }
        await _tokenStorage.ClearAsync();
    }

    public async Task<UserProfile?> GetCurrentUserAsync()
    {
        try { return await _authApi.GetCurrentUserAsync(); }
        catch { return null; }
    }

    public async Task<bool> IsLoggedInAsync()
    {
        return await _tokenStorage.IsTokenValidAsync();
    }
}
