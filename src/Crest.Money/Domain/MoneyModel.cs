#nullable enable
using Crest.Money.Abstractions;

namespace Crest.Money;

/// <summary>
/// The wire shape of an <see cref="Amount"/> in an API or a client model: the value and the
/// currency's ISO code. Null stands for "no amount" (an unpriced line, an unset field), never
/// for zero.
/// </summary>
public sealed record MoneyModel(decimal Value, string Currency)
{
    /// <summary>The wire shape of an amount; null when the amount is null or has no currency.</summary>
    public static MoneyModel? From(Amount? amount) =>
        amount is not { } value || value.Currency is null || value.Currency.Equals(Money.Currency.UnspecifiedCurrency)
            ? null
            : new MoneyModel(value.Value, value.Currency.CurrencyIsoCode);

    /// <summary>
    /// The amount a wire value stands for, in the currency <paramref name="money"/> knows by
    /// that code, its default currency when the code is unknown; <see cref="Amount.Unspecified"/>
    /// for null.
    /// </summary>
    public static Amount ToAmount(MoneyModel? model, IMoneyService money) =>
        model is null
            ? Amount.Unspecified
            : new Amount(model.Value, money.Currencies.FirstOrDefault(currency => string.Equals(currency.CurrencyIsoCode, model.Currency, StringComparison.OrdinalIgnoreCase)) ?? money.DefaultCurrency);

    public override string ToString() => $"{Value} {Currency}";
}
