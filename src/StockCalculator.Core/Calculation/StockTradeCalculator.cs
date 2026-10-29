using Microsoft.Extensions.Logging;
using StockCalculator.Core.Models;
using StockCalculator.Core.Validation;

namespace StockCalculator.Core.Calculation;

public interface IStockTradeCalculator
{
    TradeCalculation CalculateTrade(TradeSide side, TradeInput input);

    ProfitLossResult CalculateProfitLoss(TradeInput buy, TradeInput sell);
}

public sealed class StockTradeCalculator(ILogger<StockTradeCalculator> logger) : IStockTradeCalculator
{
    private const decimal PercentDivisor = 100m;
    private const int ResultDecimalPlaces = 2;

    public TradeCalculation CalculateTrade(TradeSide side, TradeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = TradeInputValidator.Validate(input);
        if (errors.Count > 0)
        {
            throw new TradeValidationException(side, errors);
        }

        var stocksValue = input.Quantity * input.PricePerShare;
        var commission = stocksValue * input.CommissionPercent / PercentDivisor;
        var total = stocksValue + commission;

        return new TradeCalculation(side, stocksValue, commission, total);
    }

    public ProfitLossResult CalculateProfitLoss(TradeInput buy, TradeInput sell)
    {
        try
        {
            var buyCalculation = CalculateTrade(TradeSide.Buy, buy);
            var sellCalculation = CalculateTrade(TradeSide.Sell, sell);
            if (TradeInputValidator.ValidateSellQuantity(buy.Quantity, sell.Quantity) is { } sellQuantityError)
            {
                throw new TradeValidationException(TradeSide.Sell, [sellQuantityError]);
            }

            var roundedAmount = Math.Round(
                sellCalculation.Total - buyCalculation.Total,
                ResultDecimalPlaces,
                MidpointRounding.AwayFromZero);
            var kind = Classify(roundedAmount);
            var grossAmount = kind == ProfitLossKind.BreakEven ? 0m : roundedAmount;
            var tax = CapitalGainsTax.Calculate(grossAmount);
            var netAmount = grossAmount - tax;

            logger.LogInformation("Profit/loss calculation performed");

            return new ProfitLossResult(buyCalculation, sellCalculation, grossAmount, tax, netAmount, kind);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            logger.LogError(
                exception,
                "Profit/loss calculation failed. Buy {@BuyInput}. Sell {@SellInput}",
                buy, sell);
            throw;
        }
    }

    private static ProfitLossKind Classify(decimal amount) => Math.Sign(amount) switch
    {
        > 0 => ProfitLossKind.Profit,
        < 0 => ProfitLossKind.Loss,
        _ => ProfitLossKind.BreakEven,
    };
}
