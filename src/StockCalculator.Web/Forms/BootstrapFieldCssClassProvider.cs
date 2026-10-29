using Microsoft.AspNetCore.Components.Forms;

namespace StockCalculator.Web.Forms;

public sealed class BootstrapFieldCssClassProvider : FieldCssClassProvider
{
    public static BootstrapFieldCssClassProvider Instance { get; } = new();

    public override string GetFieldCssClass(EditContext editContext, in FieldIdentifier fieldIdentifier) =>
        editContext.IsValid(fieldIdentifier) ? string.Empty : "is-invalid";
}
