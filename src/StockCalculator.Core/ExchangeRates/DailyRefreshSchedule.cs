namespace StockCalculator.Core.ExchangeRates;

public sealed class DailyRefreshSchedule(TimeOnly dailyRefreshTime)
{
    private DateOnly? _lastClaimedDate;

    public bool TryClaimRefresh(DateTime localNow)
    {
        var date = DateOnly.FromDateTime(localNow);
        if (TimeOnly.FromDateTime(localNow) < dailyRefreshTime || _lastClaimedDate == date)
        {
            return false;
        }

        _lastClaimedDate = date;
        return true;
    }
}
