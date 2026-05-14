using Refit;
using ZephyrDesktop.Models;

namespace ZephyrDesktop.Services.Api;

[Headers("Content-Type: application/json")]
public interface IUserApi
{
    [Get("/api/v1/users/visible")]
    Task<Refit.ApiResponse<List<UserDto>>> GetVisibleUsersAsync();
}
