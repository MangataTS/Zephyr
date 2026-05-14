using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using ZephyrDesktop.Events;
using ZephyrDesktop.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace ZephyrDesktop.Services.Api;

public sealed class ApiUnwrappingHandler : DelegatingHandler
{
    private readonly TokenStorage _tokenStorage;
    private readonly Func<IAuthApi>? _authApiFactory;

    public ApiUnwrappingHandler(TokenStorage tokenStorage, Func<IAuthApi>? authApiFactory)
    {
        _tokenStorage = tokenStorage;
        _authApiFactory = authApiFactory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenStorage.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !IsRefreshRequest(request))
        {
            var refreshed = await TryRefreshTokenAsync(cancellationToken);
            if (refreshed)
            {
                var retryRequest = await CloneRequestAsync(request, cancellationToken);
                response = await base.SendAsync(retryRequest, cancellationToken);
            }
            else
            {
                await _tokenStorage.ClearAsync();
                try
                {
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        WeakReferenceMessenger.Default.Send(new ForceLogoutEvent());
                    });
                }
                catch
                {
                }
                return response;
            }
        }

        if (response.IsSuccessStatusCode)
        {
            await TryUnwrapResponseAsync(response, cancellationToken);
        }

        return response;
    }

    private static async Task TryUnwrapResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var original = await response.Content.ReadAsStringAsync(ct);
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(original, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (apiResponse is { IsSuccess: true } && apiResponse.Data.ValueKind != JsonValueKind.Null)
            {
                var unwrapped = apiResponse.Data.GetRawText();
                response.Content = new StringContent(unwrapped, System.Text.Encoding.UTF8, "application/json");
            }
            else if (apiResponse is { IsSuccess: false })
            {
                var errorContent = JsonSerializer.Serialize(new { error = apiResponse.Message, code = apiResponse.Code });
                response.Content = new StringContent(errorContent, System.Text.Encoding.UTF8, "application/json");
                response.StatusCode = (HttpStatusCode)apiResponse.Code;
            }
        }
        catch
        {
        }
    }

    private static bool IsRefreshRequest(HttpRequestMessage request)
    {
        return request.RequestUri?.AbsolutePath.Contains("/auth/refresh") == true;
    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken ct)
    {
        if (_authApiFactory == null) return false;

        var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken)) return false;

        try
        {
            var authApi = _authApiFactory();
            var result = await authApi.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = refreshToken });
            if (result != null)
            {
                await _tokenStorage.SaveTokensAsync(result.AccessToken, result.RefreshToken, result.ExpiresIn);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content != null)
        {
            var content = await request.Content.ReadAsByteArrayAsync(ct);
            clone.Content = new ByteArrayContent(content);
            if (request.Content.Headers.ContentType != null)
                clone.Content.Headers.ContentType = request.Content.Headers.ContentType;
        }

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
