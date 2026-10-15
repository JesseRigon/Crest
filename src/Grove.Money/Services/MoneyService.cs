// Adapted from OrchardCore.Commerce's MoneyService
// (https://github.com/OrchardCMS/OrchardCore.Commerce, MIT License,
// Copyright (c) 2018 Grove).
using Grove.Money;
using Grove.Money.Abstractions;
using Grove.Money.Settings;
using Microsoft.Extensions.Options;
using Denominations_ = Grove.Money.Denominations;

namespace Grove.Money.Services;

/// <summary>
/// Exposes the data from all enabled currency providers under a simpler, unified interface.
/// </summary>
public class MoneyService : IMoneyService
{
    private readonly IEnumerable<ICurrencyProvider> _currencyProviders;
    private readonly CurrencySettings? _options;
    private readonly IEnumerable<ICurrencySelector> _currencySelectors;
    private readonly IEnumerable<IDenominationProvider> _denominationProviders;
    private readonly IEnumerable<IExchangeRateProvider> _exchangeRateProviders;

    // One entry per code: the first provider that knows a code answers for it, the same
    // order GetCurrency resolves in.
    public IEnumerable<ICurrency> Currencies =>
        _currencyProviders
            .SelectMany(provider => provider.Currencies)
            .DistinctBy(currency => currency.CurrencyIsoCode, StringComparer.Ordinal)
            .OrderBy(currency => currency.CurrencyIsoCode);

    public ICurrency DefaultCurrency
    {
        get
        {
            var defaultIsoCode = _options?.DefaultCurrency;
            return string.IsNullOrEmpty(defaultIsoCode)
                ? Currency.UsDollar
                : GetCurrency(defaultIsoCode) ?? Currency.UsDollar;
        }
    }

    // Unlike Commerce, selectors are optional here: with none registered the
    // default currency is the display currency.
    public ICurrency CurrentDisplayCurrency =>
        _currencySelectors.FirstOrDefault()?.CurrentDisplayCurrency ?? DefaultCurrency;

    public MoneyService(
        IEnumerable<ICurrencyProvider> currencyProviders,
        IOptions<CurrencySettings> options,
        IEnumerable<ICurrencySelector> currencySelectors,
        IEnumerable<IDenominationProvider>? denominationProviders = null,
        IEnumerable<IExchangeRateProvider>? exchangeRateProviders = null)
    {
        _currencyProviders = currencyProviders ?? [];
        _options = options?.Value;
        _currencySelectors = currencySelectors ?? [];
        _denominationProviders = denominationProviders ?? [];
        _exchangeRateProviders = exchangeRateProviders ?? [];
    }

    // Currencies first (a provider cannot shadow a currency code with another kind: the
    // identity includes the kind), then each provider's, first one per identity.
    public IEnumerable<IDenomination> Denominations =>
        Currencies.Select(Denominations_.Of)
            .Concat(_denominationProviders.SelectMany(provider => provider.Denominations))
            .DistinctBy(denomination => (denomination.Kind, denomination.Code.ToUpperInvariant()));

    public IDenomination? GetDenomination(string kind, string code)
    {
        if (string.Equals(kind, DenominationKinds.Currency, StringComparison.Ordinal))
        {
            var currency = Currency.FromIsoCode(code, _currencyProviders);
            return currency is null || currency.Equals(Currency.UnspecifiedCurrency) ? null : Denominations_.Of(currency);
        }

        return _denominationProviders
            .SelectMany(provider => provider.Denominations)
            .FirstOrDefault(denomination => string.Equals(denomination.Kind, kind, StringComparison.Ordinal)
                && string.Equals(denomination.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Amount?> ConvertAsync(Amount amount, IDenomination to, DateTimeOffset? at = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(to);

        if (amount.Denomination.Equals(to))
        {
            return amount;
        }

        var date = DateOnly.FromDateTime((at ?? DateTimeOffset.UtcNow).UtcDateTime);
        foreach (var provider in _exchangeRateProviders.OrderByDescending(provider => provider.Priority))
        {
            if (await provider.GetRateAsync(amount.Denomination, to, date, cancellationToken) is { } rate)
            {
                return rate.Convert(amount, to);
            }
        }

        return null;
    }

    public Amount Create(decimal value, string? currencyIsoCode = null) =>
        new(value, currencyIsoCode == null ? DefaultCurrency : GetCurrency(currencyIsoCode));

    public Amount EnsureCurrency(Amount amount) =>
        new(amount.Value, GetCurrency(amount.Currency.CurrencyIsoCode));

    public ICurrency GetCurrency(string currencyIsoCode) =>
        Currency.FromIsoCode(currencyIsoCode, _currencyProviders);
}
