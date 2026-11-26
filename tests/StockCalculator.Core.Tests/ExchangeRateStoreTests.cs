using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StockCalculator.Core.ExchangeRates;

namespace StockCalculator.Core.Tests;

public class ExchangeRateStoreTests
{
    private static readonly DateOnly LastPublicationDate = new(2026, 11, 2);

    private readonly ExchangeRateStore _store = new(NullLogger<ExchangeRateStore>.Instance);

    [Fact]
    public void BeforeLoading_HasNoRates()
    {
        _store.RatesBySymbol.Should().BeEmpty();
        _store.Rates.EffectiveDatesBySymbol.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadLatestRatesAsync_WhenAllRatesArePublished_StoresThemBySymbol()
    {
        await _store.LoadLatestRatesAsync(AllRatesProvider());

        _store.RatesBySymbol.Should().BeEquivalentTo(new Dictionary<string, decimal>
        {
            ["USD"] = 3.9389m,
            ["EUR"] = 4.4185m,
        });
    }

    [Fact]
    public async Task LoadLatestRatesAsync_StoresEffectiveDateOfEachRate()
    {
        var olderPublicationDate = LastPublicationDate.AddDays(-3);

        await _store.LoadLatestRatesAsync(new StubProvider(currency =>
            new ExchangeRate(currency, RateOf(currency), currency == CurrencyCode.USD ? olderPublicationDate : LastPublicationDate)));

        _store.Rates.EffectiveDatesBySymbol.Should().BeEquivalentTo(new Dictionary<string, DateOnly>
        {
            ["USD"] = olderPublicationDate,
            ["EUR"] = LastPublicationDate,
        });
        _store.Rates.Find(CurrencyCode.USD).Should().Be(new ExchangeRate(CurrencyCode.USD, 3.9389m, olderPublicationDate));
    }

    [Fact]
    public async Task LoadLatestRatesAsync_WhenNoRateIsReturnedOnFirstLoad_ReportsMissingRate()
    {
        await _store.LoadLatestRatesAsync(new StubProvider(currency =>
            currency == CurrencyCode.USD ? null : PublishedRate(currency)));

        _store.RatesBySymbol.Should().ContainKey("EUR").And.NotContainKey("USD");
        _store.Rates.Find(CurrencyCode.USD).Should().BeNull();
    }

    [Fact]
    public async Task LoadLatestRatesAsync_WhenFirstLoadFails_HasNoRates()
    {
        await _store.LoadLatestRatesAsync(FailingProvider());

        _store.RatesBySymbol.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadLatestRatesAsync_WhenCancelled_PropagatesCancellation()
    {
        var act = () => _store.LoadLatestRatesAsync(new StubProvider(_ => throw new OperationCanceledException()));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadLatestRatesAsync_WhenRatesAreFirstLoaded_RaisesRatesChanged()
    {
        var changeCount = CountRatesChanged();

        await _store.LoadLatestRatesAsync(AllRatesProvider());

        changeCount().Should().Be(1);
    }

    [Fact]
    public async Task LoadLatestRatesAsync_WhenRatesAreUnchanged_DoesNotRaiseRatesChanged()
    {
        await _store.LoadLatestRatesAsync(AllRatesProvider());
        var changeCount = CountRatesChanged();

        await _store.LoadLatestRatesAsync(AllRatesProvider());

        changeCount().Should().Be(0);
    }

    [Fact]
    public async Task LoadLatestRatesAsync_WhenNoRatesWereOrAreAvailable_DoesNotRaiseRatesChanged()
    {
        var changeCount = CountRatesChanged();

        await _store.LoadLatestRatesAsync(new StubProvider(_ => null));

        changeCount().Should().Be(0);
    }

    [Fact]
    public async Task LoadLatestRatesAsync_WhenRateValueChanges_ReplacesRateAndRaisesRatesChanged()
    {
        await _store.LoadLatestRatesAsync(AllRatesProvider());
        var changeCount = CountRatesChanged();

        await _store.LoadLatestRatesAsync(new StubProvider(currency => new ExchangeRate(currency, 5m, LastPublicationDate)));

        changeCount().Should().Be(1);
        _store.RatesBySymbol["USD"].Should().Be(5m);
    }

    [Fact]
    public async Task LoadLatestRatesAsync_WhenNewerRateIsPublished_ReplacesRateAndDateAndRaisesRatesChanged()
    {
        await _store.LoadLatestRatesAsync(AllRatesProvider());
        var changeCount = CountRatesChanged();
        var nextPublicationDate = LastPublicationDate.AddDays(1);

        await _store.LoadLatestRatesAsync(new StubProvider(currency => new ExchangeRate(currency, RateOf(currency), nextPublicationDate)));

        changeCount().Should().Be(1);
        _store.Rates.EffectiveDatesBySymbol["USD"].Should().Be(nextPublicationDate);
    }

    [Fact]
    public async Task LoadLatestRatesAsync_WhenNoRateIsReturnedLater_KeepsLastKnownRate()
    {
        await _store.LoadLatestRatesAsync(AllRatesProvider());
        var changeCount = CountRatesChanged();

        await _store.LoadLatestRatesAsync(new StubProvider(_ => null));

        changeCount().Should().Be(0);
        _store.Rates.Find(CurrencyCode.USD).Should().Be(PublishedRate(CurrencyCode.USD));
    }

    [Fact]
    public async Task LoadLatestRatesAsync_WhenRefreshFails_KeepsLastKnownRates()
    {
        await _store.LoadLatestRatesAsync(AllRatesProvider());
        var changeCount = CountRatesChanged();

        await _store.LoadLatestRatesAsync(FailingProvider());

        changeCount().Should().Be(0);
        _store.Rates.Find(CurrencyCode.USD).Should().Be(PublishedRate(CurrencyCode.USD));
        _store.Rates.Find(CurrencyCode.EUR).Should().Be(PublishedRate(CurrencyCode.EUR));
    }

    [Fact]
    public async Task LoadLatestRatesAsync_WhenRefreshFailsForOneCurrency_KeepsItsRateAndUpdatesOther()
    {
        await _store.LoadLatestRatesAsync(AllRatesProvider());

        await _store.LoadLatestRatesAsync(new StubProvider(currency =>
            currency == CurrencyCode.USD
                ? throw new ExchangeRateUnavailableException(currency)
                : new ExchangeRate(currency, 5m, LastPublicationDate)));

        _store.RatesBySymbol["USD"].Should().Be(3.9389m);
        _store.RatesBySymbol["EUR"].Should().Be(5m);
    }

    [Fact]
    public async Task LoadLatestRatesAsync_WhenCancelledDuringRefresh_KeepsStoredRates()
    {
        await _store.LoadLatestRatesAsync(AllRatesProvider());

        var act = () => _store.LoadLatestRatesAsync(new StubProvider(_ => throw new OperationCanceledException()));

        await act.Should().ThrowAsync<OperationCanceledException>();
        _store.RatesBySymbol.Should().HaveCount(2);
    }

    private Func<int> CountRatesChanged()
    {
        var count = 0;
        _store.RatesChanged += () => count++;
        return () => count;
    }

    private static StubProvider AllRatesProvider() => new(PublishedRate);

    private static StubProvider FailingProvider() =>
        new(currency => throw new ExchangeRateUnavailableException(currency));

    private static ExchangeRate PublishedRate(CurrencyCode currency) =>
        new(currency, RateOf(currency), LastPublicationDate);

    private static decimal RateOf(CurrencyCode currency) => currency == CurrencyCode.USD ? 3.9389m : 4.4185m;

    private sealed class StubProvider(Func<CurrencyCode, ExchangeRate?> getRate) : IExchangeRateProvider
    {
        public Task<ExchangeRate?> GetLatestAverageRateAsync(
            CurrencyCode currency,
            CancellationToken cancellationToken = default) => Task.FromResult(getRate(currency));
    }
}
