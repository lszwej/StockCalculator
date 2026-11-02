using StockCalculator.Core.ExchangeRates;

namespace StockCalculator.Core.Models;

public enum TradeSide
{
    Buy,
    Sell,
}

public enum ProfitLossKind
{
    Profit,
    Loss,
    BreakEven,
}

public sealed record TradeInput(
    decimal Quantity,
    decimal PricePerShare,
    decimal CommissionPercent,
    string Currency = TradeCurrencies.Pln);

public sealed record TradeCalculation(
    TradeSide Side,
    decimal StocksValue,
    decimal Commission,
    decimal Total,
    string Currency = TradeCurrencies.Pln);

public sealed record ProfitLossResult(
    TradeCalculation Buy,
    TradeCalculation Sell,
    decimal GrossAmount,
    decimal CapitalGainsTax,
    decimal NetAmount,
    ProfitLossKind Kind)
{
    public string Currency => Buy.Currency;
}
