namespace StockCalculator.Core.ExchangeRates;

public enum CurrencyCode
{
    USD,
    EUR,
}

public static class TradeCurrencies
{
    public const string Pln = "PLN";

    public static readonly IReadOnlyList<string> All = [Pln, nameof(CurrencyCode.EUR), nameof(CurrencyCode.USD)];
}

public sealed record ExchangeRate(CurrencyCode Currency, decimal AverageRateInPln, DateOnly EffectiveDate);
