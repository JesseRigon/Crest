// Adapted from OrchardCore.Commerce's MoneyDataType library
// (https://github.com/OrchardCMS/OrchardCore.Commerce, MIT License,
// Copyright (c) 2018 Crest).
using System;
using System.Collections.Generic;

namespace Crest.Money.Abstractions;

/// <summary>
/// Contains a set of utilities to more easily work with currency providers.
/// </summary>
public interface IMoneyService
{
    /// <summary>
    /// Gets all currencies from all currency providers.
    /// </summary>
    IEnumerable<ICurrency> Currencies { get; }

    /// <summary>
    /// Gets the currency to use or assume when none is provided.
    /// </summary>
    ICurrency DefaultCurrency { get; }

    /// <summary>
    /// Gets the current currency used for displaying prices to the customer.
    /// </summary>
    ICurrency CurrentDisplayCurrency { get; }

    /// <summary>
    /// Finds a currency from its ISO code.
    /// </summary>
    /// <param name="currencyIsoCode">The ISO code of the currency to look up.</param>
    /// <returns>The currency if found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Throws if the currency code is not found from any active provider.
    /// </exception>
    ICurrency GetCurrency(string currencyIsoCode);

    /// <summary>
    /// Creates an amount object from a value and a currency symbol.
    /// </summary>
    /// <param name="value">The decimal value of the amount.</param>
    /// <param name="currencyIsoCode">
    /// The ISO code of the currency. If <see langword="null"/> then the <see cref="DefaultCurrency"/> is used.
    /// </param>
    /// <returns>The amount.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Throws if the currency code is not found from any active provider.
    /// </exception>
    Amount Create(decimal value, string? currencyIsoCode = null);

    /// <summary>
    /// Gets every denomination: each currency (as <see cref="Money.CurrencyDenomination"/>), then
    /// what the registered <see cref="IDenominationProvider"/>s supply, one per identity.
    /// </summary>
    IEnumerable<IDenomination> Denominations { get; }

    /// <summary>Finds a denomination by kind and code; null when no provider knows it.</summary>
    IDenomination? GetDenomination(string kind, string code);

    /// <summary>
    /// Converts an amount into another denomination at the rate the highest-priority
    /// <see cref="IExchangeRateProvider"/> that knows the pair gives on the date of
    /// <paramref name="at"/> (today when null); the same denomination returns the amount
    /// unchanged. Null when no provider has a rate. For the tenant's failure behaviour and a
    /// rate's source, use <see cref="IExchangeRateRegistry"/>.
    /// </summary>
    System.Threading.Tasks.Task<Amount?> ConvertAsync(Amount amount, IDenomination to, DateTimeOffset? at = null, System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a new <see cref="Amount"/> that has been verified using <see cref="Currency.FromIsoCode"/> to ensure its
    /// currency is a known ISO 4217 code.
    /// </summary>
    Amount EnsureCurrency(Amount amount);
}
