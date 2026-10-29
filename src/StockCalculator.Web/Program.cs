using System.Globalization;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using StockCalculator.Core.Calculation;
using StockCalculator.Web.Components;
using StockCalculator.Web.Logging;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var polishCulture = CultureInfo.GetCultureInfo("pl-PL");
    CultureInfo.DefaultThreadCurrentCulture = polishCulture;
    CultureInfo.DefaultThreadCurrentUICulture = polishCulture;

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default,
    });
    builder.Host.UseWindowsService(options => options.ServiceName = "StockCalculator");
    builder.Configuration.ResolveRelativeLogPathsAgainst(AppContext.BaseDirectory);

    builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddSingleton<IStockTradeCalculator, StockTradeCalculator>();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    await app.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "StockCalculator terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
