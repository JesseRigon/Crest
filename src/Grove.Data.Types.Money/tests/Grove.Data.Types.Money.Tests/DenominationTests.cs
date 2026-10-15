using System.Text.Json;
using Grove.Data.Types.Money.Abstractions;
using Grove.Data.Types.Money.Services;
using Grove.Data.Types.Money.Settings;
using Microsoft.Extensions.Options;
using Xunit;

namespace Grove.Data.Types.Money.Tests;

// Money is value + denomination (docs/types.md › Currency and money are separate concepts):
// a currency is one kind of denomination, metals, crypto assets and points are others.
public sealed class DenominationTests
{
    private static readonly IDenomination Ether = new Denomination(DenominationKinds.Crypto, "ETH", 18, "Ξ", "Ether");
    private static readonly IDenomination Gold = new Denomination(DenominationKinds.Metal, "XAU", 4, "Au", "Gold", unit: "ozt", format: "{0} {2} {1}");

    [Fact]
    public void ACurrencyAmountKeepsItsCurrency()
    {
        var amount = new Amount(12.5m, Currency.Euro);

        Assert.True(amount.Denomination.IsCurrency());
        Assert.Equal("EUR", amount.Currency.CurrencyIsoCode);
        Assert.Equal("EUR", amount.Denomination.Code);
    }

    [Fact]
    public void ANonCurrencyAmountHasNoCurrency()
    {
        var amount = new Amount(0.5m, Ether);

        Assert.Equal(Currency.UnspecifiedCurrency, amount.Currency);
        Assert.True(amount.IsValid);
    }

    [Fact]
    public void IdentityIsKindAndCode()
    {
        Assert.Equal(Ether, new Denomination(DenominationKinds.Crypto, "eth", 6));
        Assert.NotEqual(Ether, new Denomination(DenominationKinds.Points, "ETH", 18));
    }

    [Fact]
    public void EighteenPlacesRoundTrip()
    {
        var wei = new Amount(1.000000000000000001m, Ether);

        Assert.Equal(1.000000000000000001m, wei.GetRounded().Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Denomination(DenominationKinds.Crypto, "X", Denomination.MaxPrecision + 1));
    }

    [Fact]
    public void AmountsOfDifferentDenominationsDoNotAdd()
    {
        Assert.Throws<InvalidOperationException>(() => new Amount(1, Ether) + new Amount(1, Gold));
    }

    [Fact]
    public void ANonCurrencyAmountSerializesWhole()
    {
        var json = JsonSerializer.Serialize(new Amount(2.25m, Gold));
        var read = JsonSerializer.Deserialize<Amount>(json);

        Assert.Equal(Gold, read.Denomination);
        Assert.Equal(2.25m, read.Value);
        Assert.Equal("ozt", read.Denomination.Unit);
        Assert.Equal(Gold.Format(2.25m), read.ToString());
    }

    [Fact]
    public void ACurrencyAmountSerializesAsBefore()
    {
        var json = JsonSerializer.Serialize(new Amount(3m, Currency.Euro));

        Assert.Contains("\"currency\":\"EUR\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"kind\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConversionUsesTheFirstProviderWithARate()
    {
        var service = new MoneyService([new CurrencyProvider()], Options.Create(new CurrencySettings()), [], [new FixedDenominations(Gold)], [new NoRates(), new FixedRate(2000m)]);

        var converted = await service.ConvertAsync(new Amount(1.5m, Gold), service.GetDenomination(DenominationKinds.Currency, "USD")!, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3000m, converted!.Value.Value);
        Assert.Equal("USD", converted.Value.Currency.CurrencyIsoCode);
        Assert.Same(Gold, service.GetDenomination(DenominationKinds.Metal, "xau"));
    }

    [Fact]
    public async Task ConversionAsksTheHighestPriorityProviderFirst()
    {
        var service = new MoneyService([new CurrencyProvider()], Options.Create(new CurrencySettings()), [], [new FixedDenominations(Gold)], [new FixedRate(1000m), new FixedRate(2000m, priority: 5)]);

        var converted = await service.ConvertAsync(new Amount(1m, Gold), service.GetDenomination(DenominationKinds.Currency, "USD")!, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2000m, converted!.Value.Value);
    }

    [Fact]
    public void TheManualTableTakesTheLatestRateOnOrBeforeTheDateAndInvertsAStatedReverseRate()
    {
        ManualExchangeRate[] rates =
        [
            new() { From = "USD", To = "EUR", Rate = 0.90m, EffectiveFrom = new(2025, 1, 1) },
            new() { From = "USD", To = "EUR", Rate = 0.95m, EffectiveFrom = new(2025, 6, 1) },
        ];

        Assert.Equal(0.90m, ManualExchangeRateProvider.Find(rates, "USD", "EUR", new(2025, 5, 31), "Manual")!.Rate);
        Assert.Equal(0.95m, ManualExchangeRateProvider.Find(rates, "usd", "eur", new(2025, 7, 1), "Manual")!.Rate);
        Assert.Equal(1m / 0.95m, ManualExchangeRateProvider.Find(rates, "EUR", "USD", new(2025, 7, 1), "Manual")!.Rate);
        Assert.Null(ManualExchangeRateProvider.Find(rates, "USD", "EUR", new(2024, 12, 31), "Manual"));
    }

    [Fact]
    public void TheWireShapeRoundTripsAnAmountAndHasNoneForAnUnsetOne()
    {
        var service = new MoneyService([new CurrencyProvider()], Options.Create(new CurrencySettings()), []);

        var wire = MoneyModel.From(new Amount(12.5m, Currency.Euro));

        Assert.Equal(new MoneyModel(12.5m, "EUR"), wire);
        Assert.Equal(new Amount(12.5m, Currency.Euro), MoneyModel.ToAmount(wire, service));
        Assert.Null(MoneyModel.From(null));
        Assert.Null(MoneyModel.From(Amount.Unspecified));
        Assert.Equal(Amount.Unspecified, MoneyModel.ToAmount(null, service));
    }

    private sealed class FixedDenominations(params IDenomination[] denominations) : IDenominationProvider
    {
        public IEnumerable<IDenomination> Denominations => denominations;
    }

    private sealed class NoRates : IExchangeRateProvider
    {
        public string Name => "None";

        public int Priority => 10;

        public Task<ExchangeRate?> GetRateAsync(IDenomination from, IDenomination to, DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult<ExchangeRate?>(null);
    }

    private sealed class FixedRate(decimal rate, int priority = 0) : IExchangeRateProvider
    {
        public string Name => $"Fixed {rate}";

        public int Priority => priority;

        public Task<ExchangeRate?> GetRateAsync(IDenomination from, IDenomination to, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult<ExchangeRate?>(new ExchangeRate(from.Code, to.Code, rate, date, Name));
    }
}
