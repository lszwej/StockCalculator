using FluentAssertions;
using StockCalculator.Core.ExchangeRates;

namespace StockCalculator.Core.Tests;

public class DailyRefreshScheduleTests
{
    private readonly DailyRefreshSchedule _schedule = new(new TimeOnly(12, 0));

    [Fact]
    public void TryClaimRefresh_BeforeRefreshTime_ReturnsFalse()
    {
        _schedule.TryClaimRefresh(new DateTime(2026, 11, 2, 11, 59, 59)).Should().BeFalse();
    }

    [Fact]
    public void TryClaimRefresh_AtRefreshTime_ReturnsTrue()
    {
        _schedule.TryClaimRefresh(new DateTime(2026, 11, 2, 12, 0, 0)).Should().BeTrue();
    }

    [Fact]
    public void TryClaimRefresh_AfterMissedRefreshTime_ReturnsTrue()
    {
        _schedule.TryClaimRefresh(new DateTime(2026, 11, 2, 15, 30, 0)).Should().BeTrue();
    }

    [Fact]
    public void TryClaimRefresh_SecondTimeOnSameDay_ReturnsFalse()
    {
        _schedule.TryClaimRefresh(new DateTime(2026, 11, 2, 12, 0, 0));

        _schedule.TryClaimRefresh(new DateTime(2026, 11, 2, 12, 0, 30)).Should().BeFalse();
    }

    [Fact]
    public void TryClaimRefresh_OnNextDay_OnlyAfterRefreshTime()
    {
        _schedule.TryClaimRefresh(new DateTime(2026, 11, 2, 12, 0, 0));

        _schedule.TryClaimRefresh(new DateTime(2026, 11, 3, 0, 0, 0)).Should().BeFalse();
        _schedule.TryClaimRefresh(new DateTime(2026, 11, 3, 12, 0, 0)).Should().BeTrue();
    }
}
