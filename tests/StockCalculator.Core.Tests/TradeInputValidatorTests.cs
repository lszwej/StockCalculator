using FluentAssertions;
using StockCalculator.Core.Models;
using StockCalculator.Core.Validation;

namespace StockCalculator.Core.Tests;

public class TradeInputValidatorTests
{
    [Fact]
    public void Validate_WithBoundaryValues_ReturnsNoErrors()
    {
        var errors = TradeInputValidator.Validate(
            new TradeInput(TradeLimits.MaxQuantity, TradeLimits.MaxPricePerShare, TradeLimits.MaxCommissionPercent));

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithSmallestPositiveValuesAndZeroCommission_ReturnsNoErrors()
    {
        var errors = TradeInputValidator.Validate(new TradeInput(0.0001m, 0.0001m, 0m));

        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(TradeField.Quantity)]
    [InlineData(TradeField.PricePerShare)]
    public void ValidateField_WithZero_ReturnsMustBeGreaterThanZeroError(TradeField field)
    {
        var error = TradeInputValidator.ValidateField(field, 0m);

        error.Should().NotBeNull();
        error!.Field.Should().Be(field);
        error.Message.Should().EndWith("musi być większa od 0.");
    }

    [Fact]
    public void Validate_WithZeroQuantityAndPrice_ReturnsErrorForBoth()
    {
        var errors = TradeInputValidator.Validate(new TradeInput(0m, 0m, 1m));

        errors.Select(error => error.Field).Should().Equal(TradeField.Quantity, TradeField.PricePerShare);
    }

    [Fact]
    public void Validate_WithAllFieldsInvalid_ReturnsErrorPerField()
    {
        var errors = TradeInputValidator.Validate(new TradeInput(-1m, -1m, 101m));

        errors.Select(error => error.Field).Should().Equal(
            TradeField.Quantity, TradeField.PricePerShare, TradeField.CommissionPercent);
    }

    [Theory]
    [InlineData(TradeField.Quantity)]
    [InlineData(TradeField.PricePerShare)]
    [InlineData(TradeField.CommissionPercent)]
    public void ValidateField_WithNegativeValue_ReturnsError(TradeField field)
    {
        var error = TradeInputValidator.ValidateField(field, -0.0001m);

        error.Should().NotBeNull();
        error!.Field.Should().Be(field);
    }

    [Fact]
    public void ValidateField_WithCommissionAboveLimit_ReturnsError()
    {
        var error = TradeInputValidator.ValidateField(TradeField.CommissionPercent, 100.0001m);

        error.Should().NotBeNull();
    }

    [Theory]
    [InlineData("10", "10")]
    [InlineData("10", "9.9999")]
    public void ValidateSellQuantity_WhenSellDoesNotExceedBuy_ReturnsNull(string buyQuantity, string sellQuantity)
    {
        TradeInputValidator.ValidateSellQuantity(ParseInvariant(buyQuantity), ParseInvariant(sellQuantity))
            .Should().BeNull();
    }

    [Fact]
    public void ValidateSellQuantity_WhenSellExceedsBuy_ReturnsQuantityError()
    {
        var error = TradeInputValidator.ValidateSellQuantity(10m, 10.0001m);

        error.Should().NotBeNull();
        error!.Field.Should().Be(TradeField.Quantity);
    }

    [Fact]
    public void ValidateField_WithUnknownField_Throws()
    {
        var act = () => TradeInputValidator.ValidateField((TradeField)999, 1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static decimal ParseInvariant(string value) =>
        decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
