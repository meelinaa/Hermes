namespace Hermes.Application.Ports;

public sealed record NewsArticle(
    string? ArticleId,
    string? Link,
    string? Title,
    string? Description,
    IReadOnlyList<string>? Category,
    string? ImageUrl);
