using System.ComponentModel.DataAnnotations;
using StockCalculator.Core.Models;
using StockCalculator.Core.Validation;

namespace StockCalculator.Web.Forms;

public sealed class TradeFormModel
{
    [Required(ErrorMessage = "Podaj ilość akcji.")]
    [TradeFieldRule(TradeField.Quantity)]
    public decimal? Quantity { get; set; }

    [Required(ErrorMessage = "Podaj cenę za akcję.")]
    [TradeFieldRule(TradeField.PricePerShare)]
    public decimal? PricePerShare { get; set; }

    [Required(ErrorMessage = "Podaj prowizję.")]
    [TradeFieldRule(TradeField.CommissionPercent)]
    public decimal? CommissionPercent { get; set; }

    public TradeInput ToTradeInput() => new(
        Quantity ?? throw MissingValue(nameof(Quantity)),
        PricePerShare ?? throw MissingValue(nameof(PricePerShare)),
        CommissionPercent ?? throw MissingValue(nameof(CommissionPercent)));

    private static InvalidOperationException MissingValue(string propertyName) =>
        new($"{propertyName} must be provided before conversion to {nameof(TradeInput)}.");
}
