using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StockCalculator.Core.Calculation;
using StockCalculator.Core.Models;

namespace StockCalculator.Core.Tests;

public class CapitalGainsTaxTests
{
    private readonly StockTradeCalculator _calculator = new(NullLogger<StockTradeCalculator>.Instance);

    [Theory]
    [InlineData("100", "19")]
    [InlineData("0.13", "0.02")]
    [InlineData("0.5", "0.10")]
    [InlineData("202", "38.38")]
    public void Calculate_ForProfit_ReturnsNineteenPercentRoundedToGrosze(string grossAmount, string expectedTax)
    {
        CapitalGainsTax.Calculate(ParseInvariant(grossAmount)).Should().Be(ParseInvariant(expectedTax));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-101")]
    public void Calculate_ForLossOrBreakEven_ReturnsZero(string grossAmount)
    {
        CapitalGainsTax.Calculate(ParseInvariant(grossAmount)).Should().Be(0m);
    }

    [Fact]
    public void CalculateProfitLoss_ForProfit_DeductsTaxFromNetAmount()
    {
        var result = _calculator.CalculateProfitLoss(new TradeInput(10m, 100m, 1m), new TradeInput(10m, 120m, 1m));

        result.GrossAmount.Should().Be(202m);
        result.CapitalGainsTax.Should().Be(38.38m);
        result.NetAmount.Should().Be(163.62m);
    }

    [Fact]
    public void CalculateProfitLoss_ForLoss_KeepsNetEqualToGross()
    {
        var result = _calculator.CalculateProfitLoss(new TradeInput(10m, 100m, 1m), new TradeInput(10m, 90m, 1m));

        result.CapitalGainsTax.Should().Be(0m);
        result.NetAmount.Should().Be(result.GrossAmount);
    }

    private static decimal ParseInvariant(string value) =>
        decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
