namespace StockCalculator.Core.ExchangeRates;

public sealed class ExchangeRatesSnapshot
{
    public static readonly ExchangeRatesSnapshot Empty = From([]);

    private ExchangeRatesSnapshot(
        Dictionary<string, decimal> ratesBySymbol,
        Dictionary<string, DateOnly> effectiveDatesBySymbol)
    {
        RatesBySymbol = ratesBySymbol;
        EffectiveDatesBySymbol = effectiveDatesBySymbol;
    }

    public IReadOnlyDictionary<string, decimal> RatesBySymbol { get; }

    public IReadOnlyDictionary<string, DateOnly> EffectiveDatesBySymbol { get; }

    public static ExchangeRatesSnapshot From(IEnumerable<ExchangeRate> rates)
    {
        var ratesBySymbol = rates.ToDictionary(rate => rate.Currency.ToString());

        return new(
            ratesBySymbol.ToDictionary(entry => entry.Key, entry => entry.Value.AverageRateInPln),
            ratesBySymbol.ToDictionary(entry => entry.Key, entry => entry.Value.EffectiveDate));
    }

    public ExchangeRate? Find(CurrencyCode currency)
    {
        var symbol = currency.ToString();

        return RatesBySymbol.TryGetValue(symbol, out var rate)
               && EffectiveDatesBySymbol.TryGetValue(symbol, out var effectiveDate)
            ? new ExchangeRate(currency, rate, effectiveDate)
            : null;
    }

    public bool HasSameRatesAs(ExchangeRatesSnapshot other) =>
        Enum.GetValues<CurrencyCode>().All(currency => Find(currency) == other.Find(currency));
}
