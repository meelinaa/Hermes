namespace Hermes.Application.Options;

public sealed class PaginationOptions
{
    public const string SECTION_NAME = "Pagination";

    public int DefaultPageSize { get; set; } = 20;

    public int MaxPageSize { get; set; } = 100;
}
