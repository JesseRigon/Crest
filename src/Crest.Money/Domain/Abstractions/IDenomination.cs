#nullable enable

namespace Crest.Money.Abstractions;

/// <summary>
/// What an <see cref="Amount"/> is an amount of: a currency, or gold or silver by weight, a
/// crypto asset, points or store credit. Money is transactional, currency is localization
/// (docs/types.md › Currency and money are separate concepts): a currency denomination
/// points at its localization <see cref="ICurrency"/> and borrows its symbol and formatting;
/// other kinds carry their own.
/// </summary>
/// <remarks>
/// Identity is <see cref="Kind"/> plus <see cref="Code"/>, the code unique per kind. Two
/// denominations are equal when their identities are, whatever their display rule.
/// </remarks>
public interface IDenomination : IEquatable<IDenomination>
{
    /// <summary>The kind, one of <see cref="DenominationKinds"/> or a module's own.</summary>
    string Kind { get; }

    /// <summary>The code, unique within <see cref="Kind"/> (an ISO 4217 code for a currency, "XAU", "ETH", "POINTS").</summary>
    string Code { get; }

    /// <summary>
    /// The number of decimal places an amount is kept to, up to
    /// <see cref="Denomination.MaxPrecision"/>.
    /// </summary>
    int Precision { get; }

    /// <summary>The symbol shown beside an amount; the code when there is none.</summary>
    string Symbol { get; }

    /// <summary>The display name.</summary>
    string Name { get; }

    /// <summary>The unit an amount counts, for a denomination by weight or measure (troy ounce, gram); null otherwise.</summary>
    string? Unit { get; }

    /// <summary>The localization currency when this denomination is one; null for every other kind.</summary>
    ICurrency? Currency { get; }

    /// <summary>Formats an amount of this denomination (the display rule).</summary>
    string Format(decimal amount);
}

/// <summary>The kinds Crest names. A module may use its own kind string; the identity is kind + code.</summary>
public static class DenominationKinds
{
    public const string Currency = "currency";
    public const string Metal = "metal";
    public const string Crypto = "crypto";
    public const string Points = "points";
    public const string Credit = "credit";
}

/// <summary>
/// Supplies denominations of kinds other than currency (currencies come from
/// <see cref="ICurrencyProvider"/>): a module registers one for metals, crypto assets or points.
/// </summary>
public interface IDenominationProvider
{
    IEnumerable<IDenomination> Denominations { get; }
}

/// <summary>
/// A source of exchange rates between denominations: the tenant's own table
/// (<c>ManualExchangeRateProvider</c>, always registered, lowest priority), a bank feed, a
/// paid service. <see cref="IExchangeRateRegistry"/> asks the providers by
/// <see cref="Priority"/>, highest first; the first answer wins.
/// </summary>
/// <remarks>
/// Declining (null) and failing (throwing) are different: a decline moves on to the next
/// provider quietly, a failure is logged and, when the tenant's
/// <c>ExchangeRateSettings.FailureBehavior</c> is Block, refuses the conversion.
/// </remarks>
public interface IExchangeRateProvider
{
    /// <summary>The name a rate from this provider records as its <see cref="ExchangeRate.Source"/>.</summary>
    string Name { get; }

    /// <summary>Higher is consulted first.</summary>
    int Priority => 0;

    /// <summary>
    /// How many units of <paramref name="to"/> one unit of <paramref name="from"/> is worth on
    /// <paramref name="date"/>; null when this provider has no rate for the pair.
    /// </summary>
    Task<ExchangeRate?> GetRateAsync(IDenomination from, IDenomination to, DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>
/// What callers inject to convert: walks the <see cref="IExchangeRateProvider"/>s by priority
/// with a per-scope cache, answers identity when both sides are the same denomination.
/// </summary>
public interface IExchangeRateRegistry
{
    /// <summary>
    /// The rate for the pair on the date. Null when no provider answered and the tenant's
    /// failure behaviour is Continue; throws <see cref="ExchangeRateUnavailableException"/>
    /// when it is Block.
    /// </summary>
    Task<ExchangeRate?> GetRateAsync(IDenomination from, IDenomination to, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>The same, between two currencies by ISO code. Null when either code is unknown.</summary>
    Task<ExchangeRate?> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>Thrown by <see cref="IExchangeRateRegistry"/> when no provider answered and the tenant's failure behaviour is Block.</summary>
public sealed class ExchangeRateUnavailableException(string detail) : InvalidOperationException($"Exchange rate: {detail}");
