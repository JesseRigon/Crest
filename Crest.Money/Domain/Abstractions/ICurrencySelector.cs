// Adapted from OrchardCore.Commerce's Abstractions library
// (https://github.com/OrchardCMS/OrchardCore.Commerce, MIT License,
// Copyright (c) 2018 OrchardCMS).
namespace Crest.Money.Abstractions;

/// <summary>
/// Implementations of this interface can alter the currency used for showing prices to the customer.
/// </summary>
public interface ICurrencySelector
{
    /// <summary>
    /// Gets the current currency used for displaying prices to the customer.
    /// </summary>
    public ICurrency CurrentDisplayCurrency { get; }
}
