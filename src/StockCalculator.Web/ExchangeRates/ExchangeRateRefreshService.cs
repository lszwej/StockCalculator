using Microsoft.Extensions.Options;
using StockCalculator.Core.ExchangeRates;

namespace StockCalculator.Web.ExchangeRates;

public sealed class ExchangeRateRefreshService(
    ExchangeRateStore store,
    IServiceScopeFactory scopeFactory,
    IOptions<ExchangeRateRefreshOptions> options,
    TimeProvider timeProvider,
    ILogger<ExchangeRateRefreshService> logger) : BackgroundService
{
    private static readonly TimeSpan ScheduleCheckInterval = TimeSpan.FromSeconds(30);

    private readonly DailyRefreshSchedule _schedule = new(options.Value.DailyRefreshTime);

    private DateTime LocalNow => timeProvider.GetLocalNow().DateTime;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await RunGuardedAsync(() => LoadTodayRatesAsync(cancellationToken));
        _schedule.TryClaimRefresh(LocalNow);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ScheduleCheckInterval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunGuardedAsync(() => ApplyScheduleAsync(stoppingToken));
        }
    }

    private async Task ApplyScheduleAsync(CancellationToken cancellationToken)
    {
        store.RemoveExpiredRates();

        if (_schedule.TryClaimRefresh(LocalNow))
        {
            await LoadTodayRatesAsync(cancellationToken);
        }
    }

    private async Task LoadTodayRatesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IExchangeRateProvider>();

        await store.LoadTodayRatesAsync(provider, cancellationToken);
    }

    private async Task RunGuardedAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Exchange rate refresh failed");
        }
    }
}
