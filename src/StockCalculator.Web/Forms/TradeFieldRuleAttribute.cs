using System.ComponentModel.DataAnnotations;
using StockCalculator.Core.Validation;

namespace StockCalculator.Web.Forms;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class TradeFieldRuleAttribute(TradeField field) : ValidationAttribute
{
    public TradeField Field { get; } = field;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not decimal number)
        {
            return ValidationResult.Success;
        }

        var error = TradeInputValidator.ValidateField(Field, number);
        if (error is null)
        {
            return ValidationResult.Success;
        }

        string[]? memberNames = validationContext.MemberName is { } memberName ? [memberName] : null;
        return new ValidationResult(error.Message, memberNames);
    }
}
