using System.Globalization;

namespace StockCalculator.Web.Formatting;

public static class NumberFormatter
{
    private static readonly CultureInfo PolishCulture = CultureInfo.GetCultureInfo("pl-PL");

    public static string Money(decimal value) => value.ToString("N2", PolishCulture);
}
