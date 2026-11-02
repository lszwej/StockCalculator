namespace StockCalculator.Web.ExchangeRates;

public sealed class ExchangeRateRefreshOptions
{
    public const string SectionName = "ExchangeRates";

    public TimeOnly DailyRefreshTime { get; set; } = new(12, 30);
}
