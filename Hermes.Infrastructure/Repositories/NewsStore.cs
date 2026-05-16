using Hermes.Application.Models.News;
using Hermes.Application.Ports;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.Exceptions;
using Hermes.Infrastructure.Data;
using Hermes.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Hermes.Infrastructure.Repositories;

/// <inheritdoc />
public sealed class NewsStore(HermesDbContext db) : INewsStore
{
    /// <inheritdoc />
    public async Task SetNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        if (news.Id != 0)
            throw new ArgumentException("Insert requires news id 0; use update for an existing row.", nameof(news));

        await UserExistenceGuard.EnsureExistsAsync(db, news.UserId, cancellationToken).ConfigureAwait(false);
        await db.News.AddAsync(news, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        if (news.UserId <= 0)
            throw new ArgumentException("News.UserId must be greater than zero.", nameof(news));
        if (news.Id <= 0)
            throw new NewsNotFoundException("A valid news id is required for update.");

        News? existing = await db.News.AsNoTracking()
            .FirstOrDefaultAsync(newsEntity => newsEntity.Id == news.Id, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
            throw new NewsNotFoundException($"News with id {news.Id} was not found.");
        if (existing.UserId != news.UserId)
            throw new NewsAccessDeniedException("This news entry belongs to another user.");

        await UserExistenceGuard.EnsureExistsAsync(db, news.UserId, cancellationToken).ConfigureAwait(false);
        db.News.Update(news);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        if (news.Id <= 0)
            throw new ArgumentException("News id must be greater than zero.", nameof(news));
        if (news.UserId <= 0)
            throw new ArgumentException("News.UserId must be greater than zero.", nameof(news));

        bool exists = await db.News.AsNoTracking()
            .AnyAsync(newsEntity => newsEntity.Id == news.Id && newsEntity.UserId == news.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new NewsNotFoundException($"News with id {news.Id} was not found for user {news.UserId}.");

        db.News.Remove(news);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<NewsListResult> GetNewsListAsync(NewsListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.UserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(query.UserId), query.UserId, "User id must be greater than zero.");
        if (query.Page < 1)
            throw new ArgumentOutOfRangeException(nameof(query.Page), query.Page, "Page must be at least 1.");
        if (query.PageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(query.PageSize), query.PageSize, "Page size must be at least 1.");

        await UserExistenceGuard.EnsureExistsAsync(db, query.UserId, cancellationToken).ConfigureAwait(false);

        IQueryable<News> filtered = db.News.AsNoTracking().Where(n => n.UserId == query.UserId);

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
            IQueryable<News> cursorQuery = filtered.OrderBy(n => n.Id).Where(n => n.Id > after);
            List<News> window = await cursorQuery
                .Take(query.PageSize + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            bool hasNext = window.Count > query.PageSize;
            if (hasNext)
                window.RemoveAt(window.Count - 1);
            int? nextAfter = hasNext && window.Count > 0 ? window[^1].Id : null;
            return new NewsListResult(window, 1, query.PageSize, null, null, hasNext, nextAfter);
        }

        IQueryable<News> ordered = query.SortDescending
            ? filtered.OrderByDescending(n => n.Id)
            : filtered.OrderBy(n => n.Id);

        int total = await ordered.CountAsync(cancellationToken).ConfigureAwait(false);
        int skip = (query.Page - 1) * query.PageSize;
        List<News> items = await ordered
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        int totalPages = query.PageSize > 0 ? (int)Math.Ceiling(total / (double)query.PageSize) : 0;
        bool hasNextPage = skip + items.Count < total;
        return new NewsListResult(items, query.Page, query.PageSize, total, totalPages, hasNextPage, null);
    }

    /// <inheritdoc />
    public async Task<List<(int NewsId, int UserId)>> GetDueNewsScheduleForSlotAsync(
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

        var rawMaterialized = await db.News.AsNoTracking()
            .Where(n => n.Id > 0 && n.UserId > 0 && n.IsEnabled
                && n.NextDigestSlotUtc != null
                && n.NextDigestSlotUtc >= slotStart
                && n.NextDigestSlotUtc < slotEnd)
            .OrderBy(n => n.Id)
            .Select(n => new { n.Id, n.UserId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<(int Id, int UserId)> materialized = rawMaterialized.Select(x => (x.Id, x.UserId)).ToList();

        string weekdayLabel = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(weekday, HermesJsonOptions.ForEnums))!;
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

        HashSet<(int Id, int UserId)> merged = new();
        foreach ((int id, int uid) in materialized)
            merged.Add((id, uid));
        foreach (DueNewsScheduleSlotRow row in fromJson)
            merged.Add((row.Id, row.UserId));

        return merged.OrderBy(t => t.Id).Select(t => (t.Id, t.UserId)).ToList();
    }

    /// <inheritdoc />
    public async Task AdvanceNextDigestSlotAsync(
        int newsId,
        int userId,
        TimeZoneInfo newsletterTimeZone,
        DateTime referenceUtcExclusive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newsletterTimeZone);
        News? row = await db.News
            .FirstOrDefaultAsync(n => n.Id == newsId && n.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return;
        if (row.SendOnWeekdays.Count == 0 || row.SendAtTimes.Count == 0)
            return;

        try
        {
            DateTime next = NewsletterNextRunCalculator.ComputeNextOccurrenceUtcAfter(
                row.SendOnWeekdays,
                row.SendAtTimes,
                newsletterTimeZone,
                referenceUtcExclusive);
            row.NextDigestSlotUtc = next;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Leave NextDigestSlotUtc unchanged if no future slot exists (should not occur for valid schedules).
        }
    }

    /// <inheritdoc />
    public async Task<News?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), userId, "User id must be greater than zero.");
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), id, "News id must be greater than zero.");

        News? news = await db.News.AsNoTracking()
            .FirstOrDefaultAsync(newsEntity => newsEntity.Id == id && newsEntity.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        return news is null ? throw new NewsNotFoundException() : news;
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be greater than zero.");
        return await db.News.Where(newsEntity => newsEntity.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed class DueNewsScheduleSlotRow
    {
        public int Id { get; set; }

        public int UserId { get; set; }
    }
}
