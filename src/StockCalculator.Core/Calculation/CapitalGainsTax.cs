namespace StockCalculator.Core.Calculation;

/// <summary>Polish capital gains tax ("podatek Belki") on profit from selling shares.</summary>
public static class CapitalGainsTax
{
    public const decimal RatePercent = 19m;

    private const decimal PercentDivisor = 100m;
    private const int TaxDecimalPlaces = 2;

    public static decimal Calculate(decimal grossAmount) =>
        grossAmount > 0m
            ? Math.Round(grossAmount * RatePercent / PercentDivisor, TaxDecimalPlaces, MidpointRounding.AwayFromZero)
            : 0m;
}
