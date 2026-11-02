using Microsoft.Extensions.Logging;

namespace StockCalculator.Core.ExchangeRates;

public sealed class ExchangeRateStore(ILogger<ExchangeRateStore> logger, TimeProvider timeProvider)
{
    private volatile Dictionary<string, decimal> _ratesBySymbol = [];
    private DateOnly _ratesDate;

    public event Action? RatesChanged;

    public IReadOnlyDictionary<string, decimal> RatesBySymbol => _ratesBySymbol;

    private DateOnly Today => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    public async Task LoadTodayRatesAsync(IExchangeRateProvider provider, CancellationToken cancellationToken = default)
    {
        var today = Today;
        RemoveExpiredRates();

        var rates = await Task.WhenAll(
            Enum.GetValues<CurrencyCode>().Select(currency => ResolveTodayRateAsync(provider, currency, cancellationToken)));

        _ratesDate = today;
        ReplaceRates(rates.OfType<KeyValuePair<string, decimal>>().ToDictionary());
    }

    public void RemoveExpiredRates()
    {
        if (_ratesDate != Today)
        {
            ReplaceRates([]);
        }
    }

    private async Task<KeyValuePair<string, decimal>?> ResolveTodayRateAsync(
        IExchangeRateProvider provider,
        CurrencyCode currency,
        CancellationToken cancellationToken)
    {
        var symbol = currency.ToString();

        try
        {
            var rate = await provider.GetTodayAverageRateAsync(currency, cancellationToken);
            if (rate is null)
            {
                logger.LogDebug("NBP has not published a {Currency} rate for today", currency);
                return null;
            }

            return new(symbol, rate.AverageRateInPln);
        }
        catch (ExchangeRateUnavailableException)
        {
            return _ratesBySymbol.TryGetValue(symbol, out var storedTodayRate) ? new(symbol, storedTodayRate) : null;
        }
    }

    private void ReplaceRates(Dictionary<string, decimal> rates)
    {
        if (HasSameRatesAsStored(rates))
        {
            return;
        }

        _ratesBySymbol = rates;
        RatesChanged?.Invoke();
    }

    private bool HasSameRatesAsStored(Dictionary<string, decimal> rates) =>
        rates.Count == _ratesBySymbol.Count
        && rates.All(rate => _ratesBySymbol.GetValueOrDefault(rate.Key) == rate.Value);
}
