using FluentResults;
using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services.Newsletter;

/// <summary>
/// Service implementation for managing newsletter subscription domain entities, schedule window assignments, and persistence operations.
/// </summary>
public sealed class NewsletterSubscriptionService(
    INewsletterSubscriptionRepository db,
    IOptions<NewsletterOptions> newsletterOptions,
    TimeProvider timeProvider) : INewsletterSubscriptionService
{
    public async ValueTask<Result<NewsletterId>> SetNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default)
    {
        if (news is null)
            return Result.Fail("News subscription cannot be null.");
        if (news.UserId.Value <= 0)
            return Result.Fail("Owning user ID must be greater than zero.");
            
        ScheduleWindow window = ScheduleWindow.EnsureForDigestScheduling(news.SendOnWeekdays, news.SendAtTimes);
        news.AssignDigestSchedule(window);
        await db.SetNewsAsync(news, cancellationToken).ConfigureAwait(false);
        await AdvanceDigestSlotAfterMutationAsync(news, cancellationToken).ConfigureAwait(false);
        return Result.Ok(news.Id);
    }

    public async ValueTask<Result> UpdateNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default)
    {
        if (news is null)
            return Result.Fail("News subscription cannot be null.");
            
        ScheduleWindow window = ScheduleWindow.EnsureForDigestScheduling(news.SendOnWeekdays, news.SendAtTimes);
        news.AssignDigestSchedule(window);
        await db.UpdateNewsAsync(news, cancellationToken).ConfigureAwait(false);
        await AdvanceDigestSlotAfterMutationAsync(news, cancellationToken).ConfigureAwait(false);
        return Result.Ok();
    }

    private async ValueTask AdvanceDigestSlotAfterMutationAsync(NewsletterSubscription news, CancellationToken cancellationToken)
    {
        TimeZoneInfo zone = NewsletterSchedulingProvider.ResolveTimeZone(newsletterOptions.Value.TimeZoneId);
        await db.AdvanceNextDigestSlotAsync(news.Id, news.UserId, zone, timeProvider.GetUtcNow().UtcDateTime, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<Result> DeleteNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default)
    {
        if (news is null)
            return Result.Fail("News subscription cannot be null.");
            
        await db.DeleteNewsAsync(news, cancellationToken).ConfigureAwait(false);
        return Result.Ok();
    }

    public async ValueTask<Result<NewsletterSubscription>> GetNewsByIdAsync(UserId userId, NewsletterId id, CancellationToken cancellationToken = default)
    {
        if (userId.Value <= 0)
            return Result.Fail("User id must be greater than zero.");
        if (id.Value <= 0)
            return Result.Fail("News id must be greater than zero.");
            
        NewsletterSubscription? news = await db.GetNewsByIdAsync(userId, id, cancellationToken).ConfigureAwait(false);
        if (news is null)
            return Result.Fail($"Newsletter subscription with id '{id.Value}' not found for user '{userId.Value}'.");
            
        return Result.Ok(news);
    }

    public async ValueTask<Result<NewsletterSubscription>> FindNewsByIdAsync(NewsletterId id, CancellationToken cancellationToken = default)
    {
        if (id.Value <= 0)
            return Result.Fail("News id must be greater than zero.");
            
        NewsletterSubscription? news = await db.FindNewsByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (news is null)
            return Result.Fail($"Newsletter subscription with id '{id.Value}' not found.");
            
        return Result.Ok(news);
    }

    public async ValueTask<Result<NewsletterSubscriptionListResultDto>> GetNewsListAsync(NewsletterSubscriptionListQueryDto query, CancellationToken cancellationToken = default)
    {
        if (query is null)
            return Result.Fail("Query cannot be null.");
        if (query.UserId <= 0)
            return Result.Fail("User id must be greater than zero.");
        if (query.Page < 1)
            return Result.Fail("Page must be at least 1.");
        if (query.PageSize < 1)
            return Result.Fail("Page size must be at least 1.");
        if (query.AfterId is not null && query.SortDescending)
            return Result.Fail("Cursor pagination (afterId) is only supported with ascending id order (sort=id or omit sort).");

        NewsletterSubscriptionListResultDto result = await db.GetNewsListAsync(query, cancellationToken).ConfigureAwait(false);
        return Result.Ok(result);
    }

    public async ValueTask<Result<int>> DeleteAllNewsByUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        if (userId.Value <= 0)
            return Result.Fail("User id must be greater than zero.");
            
        int count = await db.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Result.Ok(count);
    }
}
