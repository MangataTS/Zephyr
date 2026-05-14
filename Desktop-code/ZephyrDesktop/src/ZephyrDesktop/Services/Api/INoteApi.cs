using Refit;
using ZephyrDesktop.Models;

namespace ZephyrDesktop.Services.Api;

[Headers("Content-Type: application/json")]
public interface INoteApi
{
    [Get("/api/v1/notes")]
    Task<PaginatedData<NoteDto>> ListNotesAsync(
        [Query] string status = "active",
        [Query] string? source_type = null,
        [Query] string? keyword = null,
        [Query] string? updated_after = null,
        [Query] int page = 1,
        [Query] int page_size = 50,
        [Query] string sort_by = "updated_at",
        [Query] string sort_order = "desc");

    [Post("/api/v1/notes")]
    Task<NoteDto> CreateNoteAsync([Body] CreateNoteRequest request);

    [Get("/api/v1/notes/{id}")]
    Task<NoteDto> GetNoteAsync(string id);

    [Put("/api/v1/notes/{id}")]
    Task<NoteDto> UpdateNoteAsync(string id, [Body] UpdateNoteRequest request);

    [Post("/api/v1/notes/{id}/complete")]
    Task<NoteDto> CompleteNoteAsync(string id, [Body] CompleteNoteRequest request);

    [Post("/api/v1/notes/{id}/remind")]
    Task<NoteDto> RemindNoteAsync(string id, [Body] RemindRequest request);

    [Delete("/api/v1/notes/{id}")]
    Task DeleteNoteAsync(string id);

    [Post("/api/v1/notes/{id}/restore")]
    Task<NoteDto> RestoreNoteAsync(string id);

    [Get("/api/v1/notes/stats")]
    Task<NoteStatsDto> GetNoteStatsAsync(
        [Query] string? status = null,
        [Query] int? days = null);
}
