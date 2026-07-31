using Hermes.Domain.Enums;

namespace Hermes.Application.DTOs;

public sealed record NewsScheduleRow(int NewsId, int UserId, List<Weekdays> SendOnWeekdays, List<TimeOnly> SendAtTimes);
