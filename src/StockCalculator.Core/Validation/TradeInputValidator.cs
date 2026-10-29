using StockCalculator.Core.Models;

namespace StockCalculator.Core.Validation;

public enum TradeField
{
    Quantity,
    PricePerShare,
    CommissionPercent,
}

public sealed record TradeValidationError(TradeField Field, string Message);

public static class TradeLimits
{
    public const decimal MaxQuantity = 1_000_000_000_000m;
    public const decimal MaxPricePerShare = 1_000_000_000_000m;
    public const decimal MaxCommissionPercent = 100m;
}

public static class TradeInputValidator
{
    public static IReadOnlyList<TradeValidationError> Validate(TradeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new[]
            {
                ValidateField(TradeField.Quantity, input.Quantity),
                ValidateField(TradeField.PricePerShare, input.PricePerShare),
                ValidateField(TradeField.CommissionPercent, input.CommissionPercent),
            }
            .OfType<TradeValidationError>()
            .ToArray();
    }

    public static TradeValidationError? ValidateField(TradeField field, decimal value) => field switch
    {
        TradeField.Quantity => ValidatePositive(field, "Ilość akcji", value, TradeLimits.MaxQuantity),
        TradeField.PricePerShare => ValidatePositive(field, "Cena za akcję", value, TradeLimits.MaxPricePerShare),
        TradeField.CommissionPercent => ValidateNonNegative(field, "Prowizja", value, TradeLimits.MaxCommissionPercent),
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unsupported trade field."),
    };

    public static TradeValidationError? ValidateSellQuantity(decimal buyQuantity, decimal sellQuantity) =>
        sellQuantity > buyQuantity
            ? new TradeValidationError(TradeField.Quantity, "Ilość akcji przy sprzedaży nie może przekraczać ilości przy kupnie.")
            : null;

    private static TradeValidationError? ValidatePositive(TradeField field, string displayName, decimal value, decimal maxValue) =>
        value > 0m
            ? ValidateMaximum(field, displayName, value, maxValue)
            : new TradeValidationError(field, $"{displayName} musi być większa od 0.");

    private static TradeValidationError? ValidateNonNegative(TradeField field, string displayName, decimal value, decimal maxValue) =>
        value >= 0m
            ? ValidateMaximum(field, displayName, value, maxValue)
            : new TradeValidationError(field, $"{displayName} nie może być ujemna.");

    private static TradeValidationError? ValidateMaximum(TradeField field, string displayName, decimal value, decimal maxValue) =>
        value > maxValue
            ? new TradeValidationError(field, $"{displayName} nie może przekraczać {maxValue:N0}.")
            : null;
}
