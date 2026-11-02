namespace StockCalculator.Core.ExchangeRates;

public sealed class ExchangeRateUnavailableException(CurrencyCode currency, Exception? innerException = null)
    : Exception($"Could not retrieve the {currency} exchange rate.", innerException)
{
    public CurrencyCode Currency { get; } = currency;
}
