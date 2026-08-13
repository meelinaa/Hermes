using System.Text.Json;
using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.Exceptions;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Validators;
using Hermes.Infrastructure.Adapters.Outbound.Scheduling;
using Microsoft.EntityFrameworkCore;
using Hermes.Domain.ValueObjects;

namespace Hermes.Infrastructure.Adapters.Outbound.Repositories;

/// <summary>
/// Infrastructure store implementing data persistence for newsletter subscriptions.
/// </summary>
public sealed class NewsletterSubscriptionRepository(HermesDbContext db) : INewsletterSubscriptionRepository
{
    /// <summary>
    /// Persists a new newsletter subscription in the database.
    /// </summary>
    public async ValueTask SetNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        if (news.Id.Value != 0)
            throw new ArgumentException("Insert requires news id 0; use update for an existing row.", nameof(news));

        await UserExistenceValidator.EnsureExistsAsync(db, news.UserId, cancellationToken).ConfigureAwait(false);
        await db.NewsletterSubscriptions.AddAsync(news, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates an existing newsletter subscription in the database.
    /// </summary>
    public async ValueTask UpdateNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        if (news.UserId.Value <= 0)
            throw new ArgumentException("News.UserId must be greater than zero.", nameof(news));
        if (news.Id.Value <= 0)
            throw new NewsletterSubscriptionNotFoundException("A valid news id is required for update.");

        NewsletterSubscription? existing = await db.NewsletterSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(newsEntity => newsEntity.Id == news.Id, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
            throw new NewsletterSubscriptionNotFoundException($"News with id {news.Id} was not found.");
        if (existing.UserId != news.UserId)
            throw new NewsletterSubscriptionAccessDeniedException("This news entry belongs to another user.");

        await UserExistenceValidator.EnsureExistsAsync(db, news.UserId, cancellationToken).ConfigureAwait(false);
        db.NewsletterSubscriptions.Update(news);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a specific newsletter subscription from the database.
    /// </summary>
    public async ValueTask DeleteNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        if (news.Id.Value <= 0)
            throw new ArgumentException("News id must be greater than zero.", nameof(news));
        if (news.UserId.Value <= 0)
            throw new ArgumentException("News.UserId must be greater than zero.", nameof(news));

        bool exists = await db.NewsletterSubscriptions.AsNoTracking()
            .AnyAsync(newsEntity => newsEntity.Id == news.Id && newsEntity.UserId == news.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new NewsletterSubscriptionNotFoundException($"News with id {news.Id} was not found for user {news.UserId}.");

        db.NewsletterSubscriptions.Remove(news);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves a paged list of newsletter subscriptions matching the query parameters.
    /// </summary>
    public async ValueTask<NewsletterSubscriptionListResultDto> GetNewsListAsync(NewsletterSubscriptionListQueryDto query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.UserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(query.UserId), query.UserId, "User id must be greater than zero.");
        if (query.Page < 1)
            throw new ArgumentOutOfRangeException(nameof(query.Page), query.Page, "Page must be at least 1.");
        if (query.PageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(query.PageSize), query.PageSize, "Page size must be at least 1.");

        await UserExistenceValidator.EnsureExistsAsync(db, new UserId(query.UserId), cancellationToken).ConfigureAwait(false);

        IQueryable<NewsletterSubscription> filtered = db.NewsletterSubscriptions.AsNoTracking().Where(n => n.UserId == new UserId(query.UserId));

        if (query.Category is NewsCategory category)
            filtered = filtered.Where(n => n.Category != null && n.Category.Contains(category));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            if (term.Length > 200)
                term = term[..200];

            filtered = filtered.Where(n => n.Keywords != null && n.Keywords.Any(k => k.Contains(term)));
        }

        if (query.AfterId is int after)
        {
            IQueryable<NewsletterSubscription> cursorQuery = filtered.OrderBy(n => n.Id).Where(n => n.Id.Value > after);
            List<NewsletterSubscription> window = await cursorQuery
                .Take(query.PageSize + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            bool hasNext = window.Count > query.PageSize;
            if (hasNext)
                window.RemoveAt(window.Count - 1);
            int? nextAfter = hasNext && window.Count > 0 ? window[^1].Id.Value : null;
            return new NewsletterSubscriptionListResultDto(window, 1, query.PageSize, null, null, hasNext, nextAfter);
        }

        IQueryable<NewsletterSubscription> ordered = query.SortDescending
            ? filtered.OrderByDescending(n => n.Id)
            : filtered.OrderBy(n => n.Id);

        int total = await ordered.CountAsync(cancellationToken).ConfigureAwait(false);
        int skip = (query.Page - 1) * query.PageSize;
        List<NewsletterSubscription> items = await ordered
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        int totalPages = query.PageSize > 0 ? (int)Math.Ceiling(total / (double)query.PageSize) : 0;
        bool hasNextPage = skip + items.Count < total;
        return new NewsletterSubscriptionListResultDto(items, query.Page, query.PageSize, total, totalPages, hasNextPage, null);
    }

    /// <summary>
    /// Retrieves newsletter subscription schedules that are due for delivery in the specified slot.
    /// Uses database SQL queries for complex JSON searches (MySQL specific).
    /// </summary>
    public async ValueTask<List<(NewsletterId NewsId, UserId UserId)>> GetDueNewsScheduleForSlotAsync(
        Weekdays weekday,
        int hour,
        int minute,
        DateTime slotStartUtc,
        DateTime slotEndUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(hour, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hour, 23);
        ArgumentOutOfRangeException.ThrowIfLessThan(minute, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minute, 59);

        DateTime slotStart = DateTime.SpecifyKind(slotStartUtc, DateTimeKind.Utc);
        DateTime slotEnd = DateTime.SpecifyKind(slotEndUtc, DateTimeKind.Utc);

        var rawMaterialized = await db.NewsletterSubscriptions.AsNoTracking()
            .Where(n => n.Id.Value > 0 && n.UserId.Value > 0 && n.IsEnabled
                && n.NextDigestSlotUtc != null
                && n.NextDigestSlotUtc >= slotStart
                && n.NextDigestSlotUtc < slotEnd)
            .OrderBy(n => n.Id)
            .Select(n => new { Id = n.Id.Value, UserId = n.UserId.Value })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<(int Id, int UserId)> materialized = rawMaterialized.Select(x => (x.Id, x.UserId)).ToList();

        string weekdayLabel = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(weekday, HermesJsonOptions._forEnums))!;
        string timeLabel = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(new TimeOnly(hour, minute)))!;
        List<DueNewsScheduleSlotRow> fromJson = await db.Database
            .SqlQueryRaw<DueNewsScheduleSlotRow>(
                """
                SELECT n.Id AS Id, n.UserId AS UserId
                FROM news n
                WHERE n.Id > 0 AND n.UserId > 0
                  AND n.IsEnabled = 1
                  AND n.NextDigestSlotUtc IS NULL
                  AND JSON_SEARCH(
                    IF(
                      n.SendOnWeekdays IS NOT NULL
                      AND CHAR_LENGTH(TRIM(n.SendOnWeekdays)) > 0
                      AND JSON_VALID(n.SendOnWeekdays),
                      n.SendOnWeekdays,
                      '[]'),
                      'one',
                      {0},
                      NULL,
                      '$[*]') IS NOT NULL
                  AND JSON_SEARCH(
                    IF(
                      n.SendAtTimes IS NOT NULL
                      AND CHAR_LENGTH(TRIM(n.SendAtTimes)) > 0
                      AND JSON_VALID(n.SendAtTimes),
                      n.SendAtTimes,
                      '[]'),
                      'one',
                      {1},
                      NULL,
                      '$[*]') IS NOT NULL
                ORDER BY n.Id
                """,
                weekdayLabel,
                timeLabel)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<(NewsletterId Id, UserId UserId)> merged = new();
        foreach (var item in materialized)
            merged.Add((new NewsletterId(item.Id), new UserId(item.UserId)));
        foreach (DueNewsScheduleSlotRow row in fromJson)
            merged.Add((new NewsletterId(row.Id), new UserId(row.UserId)));

        return merged.OrderBy(t => t.Id.Value).Select(t => (t.Id, t.UserId)).ToList();
    }

    /// <summary>
    /// Calculates and advances the next digest run slot for a newsletter subscription.
    /// </summary>
    public async ValueTask AdvanceNextDigestSlotAsync(
        NewsletterId newsId,
        UserId userId,
        TimeZoneInfo newsletterTimeZone,
        DateTime referenceUtcExclusive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newsletterTimeZone);
        NewsletterSubscription? row = await db.NewsletterSubscriptions
            .FirstOrDefaultAsync(n => n.Id == newsId && n.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return;
        if (row.SendOnWeekdays.Count == 0 || row.SendAtTimes.Count == 0)
            return;

        DateTime? next = NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter(
            row.SendOnWeekdays,
            row.SendAtTimes,
            newsletterTimeZone,
            referenceUtcExclusive);

        if (next.HasValue)
        {
            row.SetNextDigestSlot(next.Value);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves a newsletter subscription by ID for a specific user. Throws exception if not found.
    /// </summary>
    public async ValueTask<NewsletterSubscription?> GetNewsByIdAsync(UserId userId, NewsletterId id, CancellationToken cancellationToken = default)
    {
        if (userId.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), userId.Value, "User id must be greater than zero.");
        if (id.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), id.Value, "News id must be greater than zero.");

        NewsletterSubscription? news = await db.NewsletterSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(newsEntity => newsEntity.Id == id && newsEntity.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        return news is null ? throw new NewsletterSubscriptionNotFoundException() : news;
    }

    /// <summary>
    /// Finds a newsletter subscription by its ID. Returns null if not found.
    /// </summary>
    public async ValueTask<NewsletterSubscription?> FindNewsByIdAsync(NewsletterId id, CancellationToken cancellationToken = default)
    {
        if (id.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), id.Value, "News id must be greater than zero.");

        return await db.NewsletterSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(newsEntity => newsEntity.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes all newsletter subscriptions belonging to a user.
    /// </summary>
    public async ValueTask<int> DeleteAllNewsByUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        if (userId.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be greater than zero.");
        return await db.NewsletterSubscriptions.Where(newsEntity => newsEntity.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed class DueNewsScheduleSlotRow
    {
        public int Id { get; set; }
        public int UserId { get; set; }
    }
}
