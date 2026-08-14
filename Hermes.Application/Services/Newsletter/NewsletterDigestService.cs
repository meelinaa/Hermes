using System.Globalization;
using FluentResults;
using Hermes.Application.DTOs.Email;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Mapping;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Services.Newsletter;

public sealed class NewsletterDigestService(
    IUserRepository users,
    INewsletterSubscriptionRepository newsletterSubscriptions,
    IArticleFetchingService articleFetchingService,
    IEmailProvider emailSender,
    INewsletterHtmlService newsletterRenderer,
    TimeProvider timeProvider) : INewsletterDigestService
{
    private const int MAX_ARTICLES_IN_NEWSLETTER = 5;
    private readonly CultureInfo _digestCulture = new("de-DE");

    public async Task<Result<bool>> SendAsync(UserId userId, NewsletterId newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default)
    {
        if (userId.Value <= 0)
            return Result.Fail("User ID must be positive.");
        if (newsId.Value <= 0)
            return Result.Fail("News ID must be positive.");

        User? user = await users.GetUserEntityByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email.Value))
            return Result.Ok(false);

        NewsletterSubscription? subscription = await newsletterSubscriptions.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || !subscription.IsEnabled)
            return Result.Ok(false);

        IReadOnlyList<NewsArticle> articles = await articleFetchingService.FetchArticlesForSubscriptionAsync(subscription, cancellationToken).ConfigureAwait(false);
        if (articles.Count == 0)
            return Result.Ok(false);

        string? subject = $"Hermes Newsletter (#{newsId.Value}) — {timeProvider.GetUtcNow().UtcDateTime.ToString("d", _digestCulture)}";

        List<NewsletterArticleItemDto> articleItems = articles
            .Take(MAX_ARTICLES_IN_NEWSLETTER)
            .Select(a => new NewsletterArticleItemDto(
                Category: a.Category?.FirstOrDefault() ?? "News",
                Title: a.Title ?? string.Empty,
                Content: TruncatePlainText(a.Description, 150),
                Url: a.Link ?? "#",
                ImageUrl: a.ImageUrl ?? string.Empty))
            .ToList();

        NewsletterRenderRequestDto renderRequest = new(user.Name, articleItems);
        string body = await newsletterRenderer
            .RenderNewsletterAsync(renderRequest, cancellationToken)
            .ConfigureAwait(false);

        await emailSender.SendAsync(
            new EmailMessageDto(
                new EmailRecipientDto(user.Email!.Value.Trim(), string.IsNullOrWhiteSpace(user.Name) ? null : user.Name),
                subject,
                body),
            cancellationToken).ConfigureAwait(false);

        return Result.Ok(true);
    }

    private static string TruncatePlainText(string? value, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.Length <= maxLength)
            return value;
        return string.Concat(value.AsSpan(0, maxLength - suffix.Length), suffix);
    }
}
