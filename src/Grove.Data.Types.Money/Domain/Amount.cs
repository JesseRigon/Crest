// Adapted from OrchardCore.Commerce's MoneyDataType library
// (https://github.com/OrchardCMS/OrchardCore.Commerce, MIT License,
// Copyright (c) 2018 Grove).
#nullable enable

using Grove.Data.Types.Money.Abstractions;
using Grove.Data.Types.Money.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Grove.Data.Types.Money;

/// <summary>
/// A money amount: a decimal value and the <see cref="IDenomination"/> it counts (a currency,
/// a metal by weight, a crypto asset, points). <see cref="Currency"/> is the denomination's
/// currency when it is one, so currency-only callers keep working.
/// </summary>
[JsonConverter(typeof(AmountConverter))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct Amount : IEquatable<Amount>, IComparable<Amount>
{
    public static Amount Unspecified { get; } = new(0, Money.Currency.UnspecifiedCurrency);

    private readonly IDenomination? _denomination;

    private string DebuggerDisplay => ToString();

    /// <summary>
    /// Gets the decimal value.
    /// </summary>
    public decimal Value { get; }

    /// <summary>
    /// Gets what the amount counts.
    /// </summary>
    public IDenomination Denomination => _denomination ?? Money.Denomination.Unspecified;

    /// <summary>
    /// Gets the currency when the denomination is one; the unspecified currency for any other kind.
    /// </summary>
    public ICurrency Currency => Denomination.Currency ?? Money.Currency.UnspecifiedCurrency;

    /// <summary>
    /// Gets a value indicating whether this <see cref="Amount"/> has usable values.
    /// </summary>
    public bool IsValid =>
        Value >= 0 &&
        !Denomination.Equals(Money.Denomination.Unspecified);

    public bool IsValidAndNonZero => Value > 0 && IsValid;

    [Obsolete($"Use {nameof(Unspecified)} instead.")]
    public Amount()
        : this(0, Money.Currency.UnspecifiedCurrency)
    {
    }

    public Amount(decimal value, RegionInfo region)
        : this(value, Money.Currency.FromRegion(region))
    {
    }

    public Amount(decimal value, CultureInfo? culture)
        : this(value, Money.Currency.FromCulture(culture))
    {
    }

    public Amount(decimal value, ICurrency? currency)
        : this(value, Money.Denominations.Of(currency ?? Money.Currency.UnspecifiedCurrency))
    {
    }

    public Amount(decimal value, IDenomination? denomination)
    {
        _denomination = denomination ?? Money.Denomination.Unspecified;
        Value = value;
    }

    public bool Equals(Amount other) =>
        Value == other.Value &&
        Denomination.Equals(other.Denomination);

    public override bool Equals(object? obj) => obj is Amount other && Equals(other);

    public override int GetHashCode() => (Value, Denomination).GetHashCode();

    public override string ToString() => Denomination.Format(Value);

    public int CompareTo(Amount other)
    {
        ThrowIfDenominationDoesntMatch(other);
        return Value.CompareTo(other.Value);
    }

    public Amount GetRounded() =>
        new(Math.Round(Value, Denomination.Precision), Denomination);

    /// <summary>This amount in another denomination at <paramref name="rate"/> units of it per unit of this one, rounded to its precision.</summary>
    public Amount ConvertTo(IDenomination denomination, decimal rate) =>
        new(Math.Round(Value * rate, denomination.Precision), denomination);

    /// <summary>
    /// Converts the <see cref="Amount"/> to a fixed-point fractional value by keeping some digits based on the <see
    /// cref="ICurrency.CurrencyIsoCode"/>.
    /// </summary>
    /// <param name="roundingByCurrencyCode">
    /// Provides exceptional rounding rules for currencies that aren't converted according to the default. The key is
    /// the <see cref="Currency"/>'s ISO code, the value pairs follow the same logic as the matching default parameters.
    /// </param>
    /// <param name="defaultKeepDigits">Indicates how many digits should be kept after the decimal point.</param>
    /// <param name="defaultRoundTens">
    /// If positive, the <see cref="Amount"/> is rounded to this many digits before converted to a fixed-point
    /// fractional. Ignored otherwise.
    /// </param>
    public long GetFixedPointAmount(
        IDictionary<string, (int KeepDigits, int RoundTens)> roundingByCurrencyCode,
        int defaultKeepDigits = 2,
        int defaultRoundTens = 0)
    {
        static int Tens(int zeroes) => (int)Math.Pow(10, zeroes);

        var (keepDigits, roundTens) = roundingByCurrencyCode.TryGetValue(Denomination.Code, out var pair)
            ? pair
            : (defaultKeepDigits, defaultRoundTens);

        return roundTens > 0
            ? (long)Math.Round(Value / Tens(roundTens)) * Tens(roundTens + keepDigits)
            : (long)Math.Round(Value * Tens(keepDigits));
    }

    private void ThrowIfDenominationDoesntMatch(Amount other, string operation = "compare")
    {
        if (Denomination.Equals(other.Denomination)) return;
        throw new InvalidOperationException(
            $"Can't {operation} amounts of different denominations ({Denomination} and " +
            $"{other.Denomination}).");
    }

    #region Operators

    public static explicit operator decimal(Amount amount) => amount.Value;

    public static Amount operator +(Amount first, Amount second)
    {
        first.ThrowIfDenominationDoesntMatch(second, operation: "add");
        return new(first.Value + second.Value, first.Denomination);
    }

    public static Amount operator +(Amount first, decimal second) => new(first.Value + second, first.Denomination);

    public static Amount operator -(Amount first, Amount second)
    {
        first.ThrowIfDenominationDoesntMatch(second, operation: "subtract");
        return new(first.Value - second.Value, first.Denomination);
    }

    public static Amount operator -(Amount first, decimal second) => new(first.Value - second, first.Denomination);

    public static Amount operator -(Amount amount) =>
        new(-amount.Value, amount.Denomination);

    public static Amount operator *(int quantity, Amount amount) =>
        new(quantity * amount.Value, amount.Denomination);

    public static Amount operator *(decimal quantity, Amount amount) =>
        new(quantity * amount.Value, amount.Denomination);

    public static Amount operator *(float quantity, Amount amount) =>
        new((decimal)quantity * amount.Value, amount.Denomination);

    public static Amount operator *(double quantity, Amount amount) =>
        new((decimal)quantity * amount.Value, amount.Denomination);

    public static Amount operator *(Amount amount, int quantity) =>
        new(quantity * amount.Value, amount.Denomination);

    public static Amount operator *(Amount amount, decimal quantity) =>
        new(quantity * amount.Value, amount.Denomination);

    public static Amount operator *(Amount amount, float quantity) =>
        new((decimal)quantity * amount.Value, amount.Denomination);

    public static Amount operator *(Amount amount, double quantity) =>
        new((decimal)quantity * amount.Value, amount.Denomination);

    public static Amount operator /(Amount amount, int quantity) =>
        new(amount.Value / quantity, amount.Denomination);

    public static Amount operator /(Amount amount, decimal quantity) =>
        new(amount.Value / quantity, amount.Denomination);

    public static Amount operator /(Amount amount, float quantity) =>
        amount / (decimal)quantity;

    public static Amount operator /(Amount amount, double quantity) =>
        amount / (decimal)quantity;

    public static bool operator ==(Amount first, Amount second) => first.Equals(second);

    public static bool operator !=(Amount first, Amount second) => !first.Equals(second);

    public static bool operator <(Amount first, Amount second)
    {
        first.ThrowIfDenominationDoesntMatch(second);
        return first.Value < second.Value;
    }

    public static bool operator >(Amount first, Amount second)
    {
        first.ThrowIfDenominationDoesntMatch(second);
        return first.Value > second.Value;
    }

    public static bool operator <=(Amount first, Amount second)
    {
        first.ThrowIfDenominationDoesntMatch(second);
        return first.Value <= second.Value;
    }

    public static bool operator >=(Amount first, Amount second)
    {
        first.ThrowIfDenominationDoesntMatch(second);
        return first.Value >= second.Value;
    }

    #endregion
}
