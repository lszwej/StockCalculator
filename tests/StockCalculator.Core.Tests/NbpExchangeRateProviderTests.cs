using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StockCalculator.Core.ExchangeRates;

namespace StockCalculator.Core.Tests;

public class NbpExchangeRateProviderTests
{
    private const string UsdPayload =
        """{"table":"A","currency":"dolar amerykański","code":"USD","rates":[{"no":"213/A/NBP/2026","effectiveDate":"2026-11-02","mid":3.9389}]}""";

    [Fact]
    public async Task GetTodayAverageRateAsync_ForPublishedRate_ReturnsMidRateAndDate()
    {
        var rate = await CreateProvider(_ => Json(UsdPayload)).GetTodayAverageRateAsync(CurrencyCode.USD);

        rate.Should().Be(new ExchangeRate(CurrencyCode.USD, 3.9389m, new DateOnly(2026, 11, 2)));
    }

    [Theory]
    [InlineData(CurrencyCode.USD, "https://api.nbp.pl/api/exchangerates/rates/a/usd/today/?format=json")]
    [InlineData(CurrencyCode.EUR, "https://api.nbp.pl/api/exchangerates/rates/a/eur/today/?format=json")]
    public async Task GetTodayAverageRateAsync_RequestsTodayRateFromTableA(CurrencyCode currency, string expectedUrl)
    {
        Uri? requestedUrl = null;
        var provider = CreateProvider(request =>
        {
            requestedUrl = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await provider.GetTodayAverageRateAsync(currency);

        requestedUrl.Should().Be(new Uri(expectedUrl));
    }

    [Fact]
    public async Task GetTodayAverageRateAsync_WhenRateIsNotPublishedToday_ReturnsNull()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("404 NotFound - Not Found - Brak danych"),
        });

        var rate = await provider.GetTodayAverageRateAsync(CurrencyCode.USD);

        rate.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetTodayAverageRateAsync_ForErrorStatus_ThrowsUnavailable(HttpStatusCode statusCode)
    {
        var provider = CreateProvider(_ => new HttpResponseMessage(statusCode));

        await AssertUnavailable(provider);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"code":"USD","rates":[]}""")]
    [InlineData("""{"code":"USD"}""")]
    [InlineData("""{"code":"EUR","rates":[{"effectiveDate":"2026-11-02","mid":4.4185}]}""")]
    [InlineData("""{"code":"USD","rates":[{"effectiveDate":"2026-11-02","mid":0}]}""")]
    public async Task GetTodayAverageRateAsync_ForUnexpectedPayload_ThrowsUnavailable(string payload)
    {
        await AssertUnavailable(CreateProvider(_ => Json(payload)));
    }

    [Fact]
    public async Task GetTodayAverageRateAsync_ForPlainTextBody_ThrowsUnavailable()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Service temporarily unavailable"),
        });

        await AssertUnavailable(provider);
    }

    [Fact]
    public async Task GetTodayAverageRateAsync_ForNetworkFailure_ThrowsUnavailable()
    {
        await AssertUnavailable(CreateProvider(_ => throw new HttpRequestException("No route to host")));
    }

    [Fact]
    public async Task GetTodayAverageRateAsync_ForTimeout_ThrowsUnavailable()
    {
        await AssertUnavailable(CreateProvider(_ => throw new TaskCanceledException("Timed out")));
    }

    [Fact]
    public async Task GetTodayAverageRateAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var provider = CreateProvider(_ => Json(UsdPayload));

        var act = () => provider.GetTodayAverageRateAsync(CurrencyCode.USD, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async Task AssertUnavailable(NbpExchangeRateProvider provider)
    {
        var act = () => provider.GetTodayAverageRateAsync(CurrencyCode.USD);

        (await act.Should().ThrowAsync<ExchangeRateUnavailableException>())
            .Which.Currency.Should().Be(CurrencyCode.USD);
    }

    private static HttpResponseMessage Json(string payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json"),
    };

    private static NbpExchangeRateProvider CreateProvider(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(
            new HttpClient(new StubHttpMessageHandler(respond)) { BaseAddress = NbpExchangeRateProvider.ApiBaseAddress },
            NullLogger<NbpExchangeRateProvider>.Instance);

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
}
