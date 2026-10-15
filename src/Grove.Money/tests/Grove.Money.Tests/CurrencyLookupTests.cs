using Grove.Money.Abstractions;
using Grove.Money.Services;
using Grove.Money.Settings;
using Microsoft.Extensions.Options;
using Xunit;

namespace Grove.Money.Tests;

// Currency lookups (docs/types.md › Fix the currency lookup order): the registered providers
// answer before the culture-derived table, and each code is listed once.
public sealed class CurrencyLookupTests
{
    // A global-store correction: three minor units for a code the culture table also knows.
    private static readonly ICurrency CorrectedDollar = new Currency("US Dollar", "US Dollar", "$", "USD", 3);

    [Fact]
    public void AProviderOverridesTheCultureTable()
    {
        var currency = Currency.FromIsoCode("USD", [new FixedProvider(CorrectedDollar)]);

        Assert.Equal(3, currency.DecimalPlaces);
    }

    [Fact]
    public void TheCultureTableAnswersWhatNoProviderKnows()
    {
        var currency = Currency.FromIsoCode("JPY", [new FixedProvider(CorrectedDollar)]);

        Assert.Equal("JPY", currency.CurrencyIsoCode);
    }

    [Fact]
    public void MoneyServiceListsEachCodeOnceFromTheFirstProvider()
    {
        var service = new MoneyService(
            [new FixedProvider(CorrectedDollar), new CurrencyProvider()],
            Options.Create(new CurrencySettings()),
            []);

        var dollars = service.Currencies.Where(currency => currency.CurrencyIsoCode == "USD").ToList();

        Assert.Single(dollars);
        Assert.Equal(3, dollars[0].DecimalPlaces);
        Assert.Equal(3, service.GetCurrency("USD").DecimalPlaces);
    }

    [Fact]
    public void MoneyServiceUsesTheConfiguredDefault()
    {
        var service = new MoneyService(
            [new CurrencyProvider()],
            Options.Create(new CurrencySettings { DefaultCurrency = "CAD" }),
            []);

        Assert.Equal("CAD", service.DefaultCurrency.CurrencyIsoCode);
    }

    private sealed class FixedProvider(params ICurrency[] currencies) : ICurrencyProvider
    {
        public IEnumerable<ICurrency> Currencies => currencies;

        public ICurrency GetCurrency(string isoCode) =>
            currencies.FirstOrDefault(currency => currency.CurrencyIsoCode == isoCode)!;

        public bool IsKnownCurrency(string isoCode) =>
            currencies.Any(currency => currency.CurrencyIsoCode == isoCode);
    }
}
