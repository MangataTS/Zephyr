using Refit;
using ZephyrDesktop.Models;

namespace ZephyrDesktop.Services.Api;

[Headers("Content-Type: application/json")]
public interface IWorkGroupApi
{
    [Post("/api/v1/groups")]
    Task<WorkGroupResponse> CreateWorkGroupAsync([Body] CreateWorkGroupRequest request);
}