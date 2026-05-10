namespace Hermes.Application.Options;

/// <summary>Defaults and limits for offset/cursor pagination of collection endpoints.</summary>
public sealed class PaginationOptions
{
    public const string SECTION_NAME = "Pagination";

    /// <summary>Used when the client omits <c>pageSize</c>.</summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>Maximum allowed <c>pageSize</c>; larger values are clamped server-side.</summary>
    public int MaxPageSize { get; set; } = 100;
}
