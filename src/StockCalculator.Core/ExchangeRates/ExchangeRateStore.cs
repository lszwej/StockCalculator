using Microsoft.Extensions.Logging;

namespace StockCalculator.Core.ExchangeRates;

public sealed class ExchangeRateStore(ILogger<ExchangeRateStore> logger)
{
    private volatile ExchangeRatesSnapshot _rates = ExchangeRatesSnapshot.Empty;

    public event Action? RatesChanged;

    public ExchangeRatesSnapshot Rates => _rates;

    public IReadOnlyDictionary<string, decimal> RatesBySymbol => _rates.RatesBySymbol;

    public async Task LoadLatestRatesAsync(IExchangeRateProvider provider, CancellationToken cancellationToken = default)
    {
        var rates = await Task.WhenAll(
            Enum.GetValues<CurrencyCode>().Select(currency => ResolveLatestRateAsync(provider, currency, cancellationToken)));

        ReplaceRates(ExchangeRatesSnapshot.From(rates.OfType<ExchangeRate>()));
    }

    private async Task<ExchangeRate?> ResolveLatestRateAsync(
        IExchangeRateProvider provider,
        CurrencyCode currency,
        CancellationToken cancellationToken)
    {
        try
        {
            var rate = await provider.GetLatestAverageRateAsync(currency, cancellationToken);
            if (rate is null)
            {
                logger.LogDebug("NBP returned no published {Currency} rate; keeping the stored one", currency);
            }

            return rate ?? _rates.Find(currency);
        }
        catch (ExchangeRateUnavailableException)
        {
            return _rates.Find(currency);
        }
    }

    private void ReplaceRates(ExchangeRatesSnapshot rates)
    {
        if (rates.HasSameRatesAs(_rates))
        {
            return;
        }

        _rates = rates;
        RatesChanged?.Invoke();
    }
}
