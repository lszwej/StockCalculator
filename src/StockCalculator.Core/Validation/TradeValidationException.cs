using StockCalculator.Core.Models;

namespace StockCalculator.Core.Validation;

public sealed class TradeValidationException(TradeSide side, IReadOnlyList<TradeValidationError> errors)
    : ArgumentException($"Invalid {side} trade input: {string.Join(" ", errors.Select(error => error.Message))}")
{
    public TradeSide Side { get; } = side;

    public IReadOnlyList<TradeValidationError> Errors { get; } = errors;
}
