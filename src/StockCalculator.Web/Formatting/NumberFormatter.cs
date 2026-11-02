using System.Globalization;
using StockCalculator.Core.ExchangeRates;

namespace StockCalculator.Web.Formatting;

public static class NumberFormatter
{
    private static readonly CultureInfo PolishCulture = CultureInfo.GetCultureInfo("pl-PL");

    private const char NonBreakingSpace = ' ';

    public static string Money(decimal value, string currency) =>
        currency == TradeCurrencies.Pln
            ? value.ToString("C2", PolishCulture)
            : $"{value.ToString("N2", PolishCulture)}{NonBreakingSpace}{currency}";

    public static string ExchangeRate(decimal value) => value.ToString("N4", PolishCulture);
}
