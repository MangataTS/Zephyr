using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

namespace ZephyrDesktop.Services;

public sealed class TokenStorage
{
    private readonly string _storagePath;
    private TokenData? _cached;

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ZephyrDesktop-TokenStorage-v1");

    public TokenStorage(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "data", "token.dat");
    }

    public async Task SaveTokensAsync(string accessToken, string refreshToken, long expiresIn)
    {
        _cached = new TokenData
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn
        };

        var dir = Path.GetDirectoryName(_storagePath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(_cached);
        var bytes = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(_storagePath, encrypted);
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        var data = await GetTokenDataAsync();
        return data?.AccessToken;
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        var data = await GetTokenDataAsync();
        return data?.RefreshToken;
    }

    public async Task<bool> IsTokenValidAsync()
    {
        var data = await GetTokenDataAsync();
        return data != null && data.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;
    }

    public async Task ClearAsync()
    {
        _cached = null;
        if (File.Exists(_storagePath))
            File.Delete(_storagePath);
        await Task.CompletedTask;
    }

    private async Task<TokenData?> GetTokenDataAsync()
    {
        if (_cached != null) return _cached;
        if (!File.Exists(_storagePath)) return null;

        try
        {
            var encrypted = await File.ReadAllBytesAsync(_storagePath);
            var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(decrypted);
            _cached = JsonSerializer.Deserialize<TokenData>(json);
            return _cached;
        }
        catch
        {
            return null;
        }
    }

    private sealed class TokenData
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public long ExpiresAt { get; set; }
    }
}
