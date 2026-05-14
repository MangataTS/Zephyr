using Refit;
using ZephyrDesktop.Models;

namespace ZephyrDesktop.Services.Api;

[Headers("Content-Type: application/json")]
public interface ITagApi
{
    [Get("/api/v1/tags")]
    Task<Refit.ApiResponse<List<TagDto>>> GetTagsAsync();
}
