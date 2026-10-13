#nullable enable
using Crest.Money.Abstractions;

namespace Crest.Money;

/// <summary>
/// A rate between two denominations on a date: one unit of <see cref="From"/> is worth
/// <see cref="Rate"/> units of <see cref="To"/>. <see cref="Source"/> names the provider that
/// answered ("Manual", a feed's name, "Identity"), so a posted document can say where its
/// rate came from.
/// </summary>
public sealed record ExchangeRate(string From, string To, decimal Rate, DateOnly Date, string Source)
{
    /// <summary>The source of the rate between a denomination and itself.</summary>
    public const string IdentitySource = "Identity";

    /// <summary>Converts an amount of <see cref="From"/> into <paramref name="to"/>, rounded to its precision.</summary>
    public Amount Convert(Amount amount, IDenomination to) => amount.ConvertTo(to, Rate);

    /// <summary>Converts an amount of <see cref="From"/> into the currency <paramref name="to"/>, rounded to its minor units.</summary>
    public Amount Convert(Amount amount, ICurrency to) => new Amount(amount.Value * Rate, to).GetRounded();
}
