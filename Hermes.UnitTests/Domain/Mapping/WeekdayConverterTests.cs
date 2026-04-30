using Hermes.Domain.Enums;
using Hermes.Domain.Mapping;
using Xunit;

namespace Hermes.UnitTests.Domain.Mapping;

/// <summary>
/// Specifications mapping local calendar <see cref="DateTime"/> to domain <see cref="Weekdays"/> — host timezone defines "today".
/// </summary>
/// <remarks>
/// Uses <see cref="DateTimeKind.Local"/> dates only; UTC normalization for scheduling windows happens elsewhere.
/// </remarks>
public sealed class WeekdayConverterTests
{
    /// <summary>
    /// Representative local timestamps spanning Mon–Sun in ISO week consistent order for 2026.
    /// </summary>
    public static TheoryData<DateTime, Weekdays> KnownMappings =>
        new()
        {
            { new DateTime(2026, 1, 5, 14, 30, 0, DateTimeKind.Local), Weekdays.Monday },
            { new DateTime(2026, 1, 6, 14, 30, 0, DateTimeKind.Local), Weekdays.Tuesday },
            { new DateTime(2026, 1, 7, 14, 30, 0, DateTimeKind.Local), Weekdays.Wednesday },
            { new DateTime(2026, 1, 8, 14, 30, 0, DateTimeKind.Local), Weekdays.Thursday },
            { new DateTime(2026, 1, 9, 14, 30, 0, DateTimeKind.Local), Weekdays.Friday },
            { new DateTime(2026, 1, 10, 14, 30, 0, DateTimeKind.Local), Weekdays.Saturday },
            { new DateTime(2026, 1, 11, 14, 30, 0, DateTimeKind.Local), Weekdays.Sunday },
        };

    [Theory]
    [MemberData(nameof(KnownMappings))]
    public void ToHermesWeekday_MapsLocalCalendarDay(DateTime local, Weekdays expected)
    {
        Assert.Equal(expected, WeekdayConverter.ToHermesWeekday(local));
    }
}
