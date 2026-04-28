using Hermes.Domain.Enums;

namespace Hermes.Domain.DTOs;

/// <summary>
/// Lightweight projection of <see cref="Entities.News"/> for newsletter scheduling (single-query load).
/// </summary>
/// <param name="NewsId">News profile id (one digest job per row).</param>
/// <param name="UserId">Owner user id.</param>
/// <param name="SendOnWeekdays">Days on which sends are allowed.</param>
/// <param name="SendAtTimes">Clock times (hour and minute) matched against the worker host’s current minute (<see cref="DateTime.Now"/>).</param>
public sealed record NewsScheduleRow(int NewsId, int UserId, List<Weekdays> SendOnWeekdays, List<TimeOnly> SendAtTimes);
