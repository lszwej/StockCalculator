using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace StockCalculator.Core.ExchangeRates;

public interface IExchangeRateProvider
{
    Task<ExchangeRate?> GetTodayAverageRateAsync(CurrencyCode currency, CancellationToken cancellationToken = default);
}

public sealed class NbpExchangeRateProvider(HttpClient httpClient, ILogger<NbpExchangeRateProvider> logger)
    : IExchangeRateProvider
{
    public static readonly Uri ApiBaseAddress = new("https://api.nbp.pl/api/exchangerates/rates/a/");
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public async Task<ExchangeRate?> GetTodayAverageRateAsync(
        CurrencyCode currency,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(TodayRatePath(currency), cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<NbpRatesPayload>(cancellationToken);
            return ToExchangeRate(currency, payload);
        }
        catch (Exception exception) when (IsRetrievalFailure(exception, cancellationToken))
        {
            logger.LogError(exception, "NBP exchange rate retrieval failed for {Currency}", currency);
            throw new ExchangeRateUnavailableException(currency, exception);
        }
    }

    private static string TodayRatePath(CurrencyCode currency) =>
        $"{currency.ToString().ToLowerInvariant()}/today/?format=json";

    private static ExchangeRate ToExchangeRate(CurrencyCode currency, NbpRatesPayload? payload) =>
        payload is { Rates: [{ Mid: > 0 } rate] }
        && string.Equals(payload.Code, currency.ToString(), StringComparison.OrdinalIgnoreCase)
            ? new ExchangeRate(currency, rate.Mid, rate.EffectiveDate)
            : throw new InvalidDataException($"Unexpected NBP response for {currency}.");

    private static bool IsRetrievalFailure(Exception exception, CancellationToken cancellationToken) => exception switch
    {
        HttpRequestException or JsonException or InvalidDataException => true,
        OperationCanceledException => !cancellationToken.IsCancellationRequested,
        _ => false,
    };

    private sealed record NbpRatesPayload(string? Code, IReadOnlyList<NbpRate>? Rates);

    private sealed record NbpRate(DateOnly EffectiveDate, decimal Mid);
}
