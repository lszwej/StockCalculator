using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StockCalculator.Core.Calculation;
using StockCalculator.Core.Models;
using StockCalculator.Core.Validation;

namespace StockCalculator.Core.Tests;

public class StockTradeCalculatorTests
{
    private readonly StockTradeCalculator _sut = new(NullLogger<StockTradeCalculator>.Instance);

    [Fact]
    public void CalculateTrade_ForBuy_AddsCommissionToStocksValue()
    {
        var result = _sut.CalculateTrade(TradeSide.Buy, new TradeInput(10m, 100m, 1m));

        result.Should().Be(new TradeCalculation(TradeSide.Buy, 1000m, 10m, 1010m));
    }

    [Fact]
    public void CalculateTrade_ForSell_AddsCommissionToStocksValue()
    {
        var result = _sut.CalculateTrade(TradeSide.Sell, new TradeInput(10m, 100m, 1m));

        result.Should().Be(new TradeCalculation(TradeSide.Sell, 1000m, 10m, 1010m));
    }

    [Fact]
    public void CalculateTrade_WithFractionalValues_KeepsDecimalPrecision()
    {
        var result = _sut.CalculateTrade(TradeSide.Buy, new TradeInput(2.5m, 10.4m, 0.39m));

        result.StocksValue.Should().Be(26m);
        result.Commission.Should().Be(0.1014m);
        result.Total.Should().Be(26.1014m);
    }

    [Fact]
    public void CalculateTrade_WithMaximumCommission_DoublesSellTotal()
    {
        var result = _sut.CalculateTrade(TradeSide.Sell, new TradeInput(10m, 100m, 100m));

        result.Total.Should().Be(2000m);
    }

    [Fact]
    public void CalculateProfitLoss_WhenSellTotalExceedsBuyTotal_ReturnsProfit()
    {
        var result = _sut.CalculateProfitLoss(new TradeInput(10m, 100m, 1m), new TradeInput(10m, 120m, 1m));

        result.Kind.Should().Be(ProfitLossKind.Profit);
        result.GrossAmount.Should().Be(202m);
    }

    [Fact]
    public void CalculateProfitLoss_WhenSellTotalIsBelowBuyTotal_ReturnsLoss()
    {
        var result = _sut.CalculateProfitLoss(new TradeInput(10m, 100m, 1m), new TradeInput(10m, 90m, 1m));

        result.Kind.Should().Be(ProfitLossKind.Loss);
        result.GrossAmount.Should().Be(-101m);
    }

    [Fact]
    public void CalculateProfitLoss_WhenValuesAreEqual_ReturnsBreakEven()
    {
        var result = _sut.CalculateProfitLoss(new TradeInput(10m, 100m, 0m), new TradeInput(10m, 100m, 0m));

        result.Kind.Should().Be(ProfitLossKind.BreakEven);
        result.GrossAmount.Should().Be(0m);
    }

    [Theory]
    [InlineData("0.001", "0.004")]
    [InlineData("0.004", "0.001")]
    public void CalculateProfitLoss_WhenDifferenceRoundsToZero_ReturnsBreakEven(string buyPrice, string sellPrice)
    {
        var result = _sut.CalculateProfitLoss(
            new TradeInput(1m, decimal.Parse(buyPrice, System.Globalization.CultureInfo.InvariantCulture), 0m),
            new TradeInput(1m, decimal.Parse(sellPrice, System.Globalization.CultureInfo.InvariantCulture), 0m));

        result.Kind.Should().Be(ProfitLossKind.BreakEven);
        result.GrossAmount.Should().Be(0m);
    }

    [Fact]
    public void CalculateProfitLoss_RoundsAmountToTwoDecimalPlaces()
    {
        var result = _sut.CalculateProfitLoss(new TradeInput(1m, 10m, 0m), new TradeInput(1m, 10.125m, 0m));

        result.GrossAmount.Should().Be(0.13m);
    }

    public static TheoryData<TradeInput, TradeField> InvalidInputs => new()
    {
        { new TradeInput(-1m, 10m, 1m), TradeField.Quantity },
        { new TradeInput(TradeLimits.MaxQuantity + 1m, 10m, 1m), TradeField.Quantity },
        { new TradeInput(1m, -0.01m, 1m), TradeField.PricePerShare },
        { new TradeInput(0m, 10m, 1m), TradeField.Quantity },
        { new TradeInput(1m, 0m, 1m), TradeField.PricePerShare },
        { new TradeInput(1m, TradeLimits.MaxPricePerShare + 1m, 1m), TradeField.PricePerShare },
        { new TradeInput(1m, 10m, -0.01m), TradeField.CommissionPercent },
        { new TradeInput(1m, 10m, 100.01m), TradeField.CommissionPercent },
    };

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public void CalculateTrade_WithInvalidInput_ThrowsValidationException(TradeInput input, TradeField expectedField)
    {
        var act = () => _sut.CalculateTrade(TradeSide.Buy, input);

        act.Should().Throw<TradeValidationException>()
            .Which.Errors.Should().ContainSingle(error => error.Field == expectedField);
    }

    [Fact]
    public void CalculateProfitLoss_WithInvalidSellInput_ThrowsValidationExceptionForSellSide()
    {
        var act = () => _sut.CalculateProfitLoss(new TradeInput(1m, 10m, 1m), new TradeInput(-1m, 10m, 1m));

        act.Should().Throw<TradeValidationException>()
            .Which.Side.Should().Be(TradeSide.Sell);
    }

    [Fact]
    public void CalculateProfitLoss_WhenSellQuantityExceedsBuyQuantity_ThrowsValidationExceptionForSellQuantity()
    {
        var act = () => _sut.CalculateProfitLoss(new TradeInput(10m, 100m, 1m), new TradeInput(11m, 100m, 1m));

        var exception = act.Should().Throw<TradeValidationException>().Which;
        exception.Side.Should().Be(TradeSide.Sell);
        exception.Errors.Should().ContainSingle(error => error.Field == TradeField.Quantity);
    }

    [Fact]
    public void CalculateProfitLoss_WhenSellingPartOfBoughtShares_ReturnsResult()
    {
        var result = _sut.CalculateProfitLoss(new TradeInput(10m, 100m, 0m), new TradeInput(5m, 300m, 0m));

        result.GrossAmount.Should().Be(500m);
    }

    [Fact]
    public void CalculateTrade_WithNullInput_ThrowsArgumentNullException()
    {
        var act = () => _sut.CalculateTrade(TradeSide.Buy, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
