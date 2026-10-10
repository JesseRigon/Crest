// Adapted from OrchardCore.Commerce's MoneyDataType library
// (https://github.com/OrchardCMS/OrchardCore.Commerce, MIT License,
// Copyright (c) 2018 Crest).
using System.Collections.Generic;

namespace Crest.Money.Abstractions;

/// <summary>
/// A service that provides a set of currencies.
/// </summary>
public interface ICurrencyProvider
{
    /// <summary>
    /// Gets the list of currencies.
    /// </summary>
    IEnumerable<ICurrency> Currencies { get; }

    /// <summary>
    /// Finds the currency object for the provided three letter currency code, like USD and EUR.
    /// </summary>
    /// <param name="isoCode">The three-letter ISO 4217 code for the currency.</param>
    ICurrency GetCurrency(string isoCode);

    /// <summary>
    /// Returns <see langword="true"/> if the <see cref="Currency.CurrencyIsoCode"/> defined by <paramref
    /// name="isoCode"/> is registered in this provider instance, otherwise <see langword="false"/>.
    /// </summary>
    bool IsKnownCurrency(string isoCode);
}
