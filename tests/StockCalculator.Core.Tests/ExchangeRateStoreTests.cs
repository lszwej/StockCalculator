using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StockCalculator.Core.ExchangeRates;

namespace StockCalculator.Core.Tests;

public class ExchangeRateStoreTests
{
    private static readonly DateOnly Today = new(2026, 11, 2);

    private readonly StubTimeProvider _clock = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly ExchangeRateStore _store;

    public ExchangeRateStoreTests() => _store = new ExchangeRateStore(NullLogger<ExchangeRateStore>.Instance, _clock);

    [Fact]
    public void BeforeLoading_HasNoRates()
    {
        _store.RatesBySymbol.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenAllRatesArePublished_StoresThemBySymbol()
    {
        await _store.LoadTodayRatesAsync(new StubProvider(currency => new ExchangeRate(currency, RateOf(currency), Today)));

        _store.RatesBySymbol.Should().BeEquivalentTo(new Dictionary<string, decimal>
        {
            ["USD"] = 3.9389m,
            ["EUR"] = 4.4185m,
        });
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenRateIsNotPublished_ReportsMissingRates()
    {
        await _store.LoadTodayRatesAsync(new StubProvider(currency =>
            currency == CurrencyCode.USD ? null : new ExchangeRate(currency, RateOf(currency), Today)));

        _store.RatesBySymbol.Should().ContainKey("EUR").And.NotContainKey("USD");
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenRetrievalFails_ReportsMissingRates()
    {
        await _store.LoadTodayRatesAsync(new StubProvider(currency => throw new ExchangeRateUnavailableException(currency)));

        _store.RatesBySymbol.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenCancelled_PropagatesCancellation()
    {
        var act = () => _store.LoadTodayRatesAsync(new StubProvider(_ => throw new OperationCanceledException()));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenRatesAreFirstLoaded_RaisesRatesChanged()
    {
        var changeCount = CountRatesChanged();

        await _store.LoadTodayRatesAsync(AllRatesProvider());

        changeCount().Should().Be(1);
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenRatesAreUnchanged_DoesNotRaiseRatesChanged()
    {
        await _store.LoadTodayRatesAsync(AllRatesProvider());
        var changeCount = CountRatesChanged();

        await _store.LoadTodayRatesAsync(AllRatesProvider());

        changeCount().Should().Be(0);
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenNoRatesWereOrAreAvailable_DoesNotRaiseRatesChanged()
    {
        var changeCount = CountRatesChanged();

        await _store.LoadTodayRatesAsync(new StubProvider(_ => null));

        changeCount().Should().Be(0);
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenRateValueChanges_ReplacesRateAndRaisesRatesChanged()
    {
        await _store.LoadTodayRatesAsync(AllRatesProvider());
        var changeCount = CountRatesChanged();

        await _store.LoadTodayRatesAsync(new StubProvider(currency => new ExchangeRate(currency, 5m, Today)));

        changeCount().Should().Be(1);
        _store.RatesBySymbol["USD"].Should().Be(5m);
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenRateIsNoLongerPublished_RemovesItAndRaisesRatesChanged()
    {
        await _store.LoadTodayRatesAsync(AllRatesProvider());
        var changeCount = CountRatesChanged();

        await _store.LoadTodayRatesAsync(new StubProvider(_ => null));

        changeCount().Should().Be(1);
        _store.RatesBySymbol.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenRefreshFailsOnSameDay_KeepsTodayRates()
    {
        await _store.LoadTodayRatesAsync(AllRatesProvider());
        var changeCount = CountRatesChanged();

        await _store.LoadTodayRatesAsync(FailingProvider());

        changeCount().Should().Be(0);
        _store.RatesBySymbol["USD"].Should().Be(3.9389m);
        _store.RatesBySymbol["EUR"].Should().Be(4.4185m);
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenRefreshFailsForOneCurrency_KeepsItsRateAndUpdatesOther()
    {
        await _store.LoadTodayRatesAsync(AllRatesProvider());

        await _store.LoadTodayRatesAsync(new StubProvider(currency =>
            currency == CurrencyCode.USD
                ? throw new ExchangeRateUnavailableException(currency)
                : new ExchangeRate(currency, 5m, Today)));

        _store.RatesBySymbol["USD"].Should().Be(3.9389m);
        _store.RatesBySymbol["EUR"].Should().Be(5m);
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenRefreshFailsOnNextDay_RemovesYesterdayRates()
    {
        await _store.LoadTodayRatesAsync(AllRatesProvider());
        _clock.Advance(TimeSpan.FromDays(1));
        var changeCount = CountRatesChanged();

        await _store.LoadTodayRatesAsync(FailingProvider());

        changeCount().Should().Be(1);
        _store.RatesBySymbol.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenFirstLoadFails_HasNoRates()
    {
        await _store.LoadTodayRatesAsync(FailingProvider());

        _store.RatesBySymbol.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveExpiredRates_OnSameDay_KeepsRatesWithoutRaisingRatesChanged()
    {
        await _store.LoadTodayRatesAsync(AllRatesProvider());
        _clock.Advance(TimeSpan.FromHours(11.99));
        var changeCount = CountRatesChanged();

        _store.RemoveExpiredRates();

        changeCount().Should().Be(0);
        _store.RatesBySymbol.Should().HaveCount(2);
    }

    [Fact]
    public async Task RemoveExpiredRates_AfterMidnight_RemovesRatesAndRaisesRatesChanged()
    {
        await _store.LoadTodayRatesAsync(AllRatesProvider());
        _clock.Advance(TimeSpan.FromHours(12));
        var changeCount = CountRatesChanged();

        _store.RemoveExpiredRates();

        changeCount().Should().Be(1);
        _store.RatesBySymbol.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadTodayRatesAsync_WhenCancelledDuringRefresh_KeepsStoredRates()
    {
        await _store.LoadTodayRatesAsync(AllRatesProvider());

        var act = () => _store.LoadTodayRatesAsync(new StubProvider(_ => throw new OperationCanceledException()));

        await act.Should().ThrowAsync<OperationCanceledException>();
        _store.RatesBySymbol.Should().HaveCount(2);
    }

    private Func<int> CountRatesChanged()
    {
        var count = 0;
        _store.RatesChanged += () => count++;
        return () => count;
    }

    private static StubProvider AllRatesProvider() =>
        new(currency => new ExchangeRate(currency, RateOf(currency), Today));

    private static StubProvider FailingProvider() =>
        new(currency => throw new ExchangeRateUnavailableException(currency));

    private static decimal RateOf(CurrencyCode currency) => currency == CurrencyCode.USD ? 3.9389m : 4.4185m;

    private sealed class StubTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class StubProvider(Func<CurrencyCode, ExchangeRate?> getRate) : IExchangeRateProvider
    {
        public Task<ExchangeRate?> GetTodayAverageRateAsync(
            CurrencyCode currency,
            CancellationToken cancellationToken = default) => Task.FromResult(getRate(currency));
    }
}
