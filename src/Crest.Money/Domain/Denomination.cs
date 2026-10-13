#nullable enable
using System.Diagnostics;
using System.Globalization;
using Crest.Money.Abstractions;

namespace Crest.Money;

/// <summary>
/// A denomination that is not a currency (a metal, a crypto asset, points, store credit),
/// carrying its own precision and display rule. Currencies are <see cref="CurrencyDenomination"/>.
/// </summary>
[DebuggerDisplay("{Kind,nq}:{Code,nq}")]
public sealed class Denomination : IDenomination
{
    /// <summary>
    /// The most decimal places a denomination may keep. Eighteen (an ether amount in wei) fits
    /// <see cref="decimal"/>: 96 bits of mantissa at scale 18 hold amounts up to about 7.9e10
    /// whole units, far above any single holding; beyond that scale the integer part shrinks
    /// below what a ledger needs.
    /// </summary>
    public const int MaxPrecision = 18;

    /// <summary>The default display rule: the symbol, then the number ("ETH 0.500000000000000000").</summary>
    public const string DefaultFormat = "{1} {0}";

    public Denomination(string kind, string code, int precision, string? symbol = null, string? name = null, string? unit = null, string? format = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentOutOfRangeException.ThrowIfNegative(precision);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(precision, MaxPrecision);

        Kind = kind;
        Code = code;
        Precision = precision;
        Symbol = string.IsNullOrEmpty(symbol) ? code : symbol;
        Name = string.IsNullOrEmpty(name) ? code : name;
        Unit = string.IsNullOrEmpty(unit) ? null : unit;
        FormatPattern = string.IsNullOrEmpty(format) ? DefaultFormat : format;
    }

    /// <summary>The "no denomination" value an unset <see cref="Amount"/> carries: the unspecified currency.</summary>
    public static IDenomination Unspecified { get; } = new CurrencyDenomination(Money.Currency.UnspecifiedCurrency);

    public string Kind { get; }

    public string Code { get; }

    public int Precision { get; }

    public string Symbol { get; }

    public string Name { get; }

    public string? Unit { get; }

    /// <summary>
    /// The display rule: a composite format string over the number ({0}, formatted to
    /// <see cref="Precision"/> places in the current culture), the symbol ({1}) and the unit ({2}).
    /// The default is "{1} {0}" ("ETH 0.5").
    /// </summary>
    public string FormatPattern { get; }

    public ICurrency? Currency => null;

    public string Format(decimal amount) =>
        string.Format(CultureInfo.CurrentCulture, FormatPattern, amount.ToString("N" + Precision.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture), Symbol, Unit ?? string.Empty).Trim();

    public bool Equals(IDenomination? other) => Denominations.SameIdentity(this, other);

    public override bool Equals(object? obj) => obj is IDenomination other && Equals(other);

    public override int GetHashCode() => Denominations.IdentityHash(this);

    public override string ToString() => $"{Kind}:{Code}";
}

/// <summary>A currency as a denomination: identity, precision and formatting are the currency's.</summary>
[DebuggerDisplay("currency:{Code,nq}")]
public sealed class CurrencyDenomination : IDenomination
{
    public CurrencyDenomination(ICurrency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        Currency = currency;
    }

    public ICurrency Currency { get; }

    public string Kind => DenominationKinds.Currency;

    public string Code => Currency.CurrencyIsoCode;

    public int Precision => Currency.DecimalPlaces;

    public string Symbol => Currency.Symbol;

    public string Name => Currency.EnglishName;

    public string? Unit => null;

    public string Format(decimal amount) => Currency.ToString(amount);

    public bool Equals(IDenomination? other) => Denominations.SameIdentity(this, other);

    public override bool Equals(object? obj) => obj is IDenomination other && Equals(other);

    public override int GetHashCode() => Denominations.IdentityHash(this);

    public override string ToString() => Code;
}

/// <summary>Helpers over <see cref="IDenomination"/>.</summary>
public static class Denominations
{
    /// <summary>The denomination for a currency.</summary>
    public static IDenomination Of(ICurrency currency) => new CurrencyDenomination(currency);

    /// <summary>Whether two denominations have the same identity: the kind (ordinal) and the code (ignoring case, as currency codes compare).</summary>
    public static bool SameIdentity(IDenomination? first, IDenomination? second) =>
        ReferenceEquals(first, second)
        || (first is not null && second is not null
            && string.Equals(first.Kind, second.Kind, StringComparison.Ordinal)
            && string.Equals(first.Code, second.Code, StringComparison.OrdinalIgnoreCase));

    internal static int IdentityHash(IDenomination denomination) =>
        HashCode.Combine(StringComparer.Ordinal.GetHashCode(denomination.Kind), StringComparer.OrdinalIgnoreCase.GetHashCode(denomination.Code));

    /// <summary>Whether a denomination is the currency kind.</summary>
    public static bool IsCurrency(this IDenomination denomination) =>
        string.Equals(denomination.Kind, DenominationKinds.Currency, StringComparison.Ordinal);
}
