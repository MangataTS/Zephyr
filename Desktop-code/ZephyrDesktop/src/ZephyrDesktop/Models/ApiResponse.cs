namespace ZephyrDesktop.Models;

public sealed class ApiResponse<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = "";
    public T? Data { get; set; }
    public long Timestamp { get; set; }

    public bool IsSuccess => Code is 200 or 201;
}

public sealed class PaginatedData<T>
{
    public List<T> Data { get; set; } = [];
    public long Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
