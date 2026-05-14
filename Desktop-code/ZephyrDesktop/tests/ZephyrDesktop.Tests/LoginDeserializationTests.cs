using System.Text.Json;
using Xunit;
using ZephyrDesktop.Models;

namespace ZephyrDesktop.Tests;

public class LoginDeserializationTests
{
    private static readonly string BackendLoginResponseJson = """
        {
            "code": 200,
            "message": "success",
            "data": {
                "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test",
                "refresh_token": "rt_abc123def456",
                "expires_in": 3600,
                "user": {
                    "id": "1",
                    "username": "admin",
                    "name": "张局长",
                    "role": "admin",
                    "rank": "局长",
                    "phone": "13800138000",
                    "email": "admin@test.com",
                    "avatar": "",
                    "dept_id": "1",
                    "dept_name": "信息中心",
                    "permissions": ["read", "write"],
                    "is_active": true
                }
            },
            "timestamp": 1700000000
        }
        """;

    [Fact]
    public void Step1_ApiUnwrappingHandler_ExtractsData_WithSnakeCaseKeys()
    {
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(
            BackendLoginResponseJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.IsSuccess);
        Assert.NotEqual(JsonValueKind.Null, apiResponse.Data.ValueKind);

        var unwrappedJson = apiResponse.Data.GetRawText();
        Assert.Contains("access_token", unwrappedJson);
        Assert.Contains("refresh_token", unwrappedJson);
        Assert.Contains("expires_in", unwrappedJson);
    }

    [Fact]
    public void Step2_LoginResponse_SnakeCaseFields_DeserializedCorrectly()
    {
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(
            BackendLoginResponseJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var unwrappedJson = apiResponse!.Data.GetRawText();

        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(
            unwrappedJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(loginResponse);
        Assert.Equal("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test", loginResponse.AccessToken);
        Assert.Equal("rt_abc123def456", loginResponse.RefreshToken);
        Assert.Equal(3600, loginResponse.ExpiresIn);
    }

    [Fact]
    public void Step3_UserProfile_SnakeCaseFields_DeserializedCorrectly()
    {
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(
            BackendLoginResponseJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var unwrappedJson = apiResponse!.Data.GetRawText();

        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(
            unwrappedJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(loginResponse);
        Assert.NotNull(loginResponse.User);
        Assert.Equal("1", loginResponse.User.Id);
        Assert.Equal("admin", loginResponse.User.Username);
        Assert.Equal("张局长", loginResponse.User.Name);
        Assert.Equal("1", loginResponse.User.DeptId);
        Assert.Equal("信息中心", loginResponse.User.DeptName);
        Assert.True(loginResponse.User.IsActive);
        Assert.Equal(["read", "write"], loginResponse.User.Permissions);
    }

    [Fact]
    public void Step4_LoginResponse_DefaultOptions_StillWorksViaJsonPropertyName()
    {
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(
            BackendLoginResponseJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var unwrappedJson = apiResponse!.Data.GetRawText();

        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(unwrappedJson);

        Assert.NotNull(loginResponse);
        Assert.NotEqual("", loginResponse.AccessToken);
        Assert.NotEqual("", loginResponse.RefreshToken);
        Assert.True(loginResponse.ExpiresIn > 0);
    }
}
