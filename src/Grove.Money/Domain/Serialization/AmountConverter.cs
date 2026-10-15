// Adapted from OrchardCore.Commerce's MoneyDataType library
// (https://github.com/OrchardCMS/OrchardCore.Commerce, MIT License,
// Copyright (c) 2018 Grove).
using Grove.Money.Abstractions;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Grove.Money.Currency;

namespace Grove.Money.Serialization;

public sealed class AmountConverter : JsonConverter<Amount>
{
    public const string ValueName = "value";
    public const string CurrencyName = "currency";
    public const string Name = "name";
    public const string NativeName = "nativename";
    public const string EnglishName = "englishname";
    public const string Symbol = "symbol";
    public const string Iso = "iso";
    public const string DecimalDigits = "dec";

    // A denomination other than a currency is written whole (kind, code, precision and its
    // display rule), so an amount reads back without a provider lookup.
    public const string Kind = "kind";
    public const string Code = "code";
    public const string Precision = "precision";
    public const string Unit = "unit";
    public const string Format = "format";

    public override Amount Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        decimal value = default;
        ICurrency currency = null;
        string nativeName = null;
        string englishName = null;
        string symbol = null;
        string iso = null;
        int? decimalDigits = null;
        string kind = null;
        string code = null;
        int? precision = null;
        string unit = null;
        string format = null;

        if (reader.TokenType == JsonTokenType.String)
        {
            return ReadString(reader.GetString());
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var propertyName = reader.GetString();
            if (!reader.Read()) continue;

            switch (propertyName)
            {
                case ValueName:
                    value = reader.GetDecimal();
                    break;
                case CurrencyName:
                    currency = FromIsoCode(reader.GetString());
                    break;
                case Name: // Kept for backwards compatibility
                case NativeName:
                    nativeName = reader.GetString();
                    break;
                case EnglishName:
                    englishName = reader.GetString();
                    break;
                case Symbol:
                    symbol = reader.GetString();
                    break;
                case Iso:
                    iso = reader.GetString();
                    break;
                case DecimalDigits when reader.TryGetInt32(out var i):
                    decimalDigits = i;
                    break;
                case Kind:
                    kind = reader.GetString();
                    break;
                case Code:
                    code = reader.GetString();
                    break;
                case Precision when reader.TryGetInt32(out var p):
                    precision = p;
                    break;
                case Unit:
                    unit = reader.GetString();
                    break;
                case Format:
                    format = reader.GetString();
                    break;
                default:
                    throw new InvalidOperationException($"Unknown property name \"{propertyName}\".");
            }
        }

        if (kind is not null && kind != DenominationKinds.Currency)
        {
            return new(value, new Denomination(kind, code ?? throw new InvalidOperationException("Invalid amount format. A denomination must include a code."), precision.GetValueOrDefault(DefaultDecimalDigits), symbol, englishName ?? nativeName, unit, format));
        }

        currency = HandleUnknownCurrency(currency, nativeName, englishName, symbol, iso, decimalDigits);

        return new(value, currency);
    }

    public override void Write(Utf8JsonWriter writer, Amount value, JsonSerializerOptions options)
    {
        if (value.Denomination is null)
        {
            throw new InvalidOperationException("Amount must have a denomination applied to allow serialization.");
        }

        writer.WriteStartObject();
        writer.WriteNumber(ValueName, value.Value);

        if (!value.Denomination.IsCurrency())
        {
            var denomination = value.Denomination;
            writer.WriteString(Kind, denomination.Kind);
            writer.WriteString(Code, denomination.Code);
            writer.WriteNumber(Precision, denomination.Precision);
            writer.WriteString(Symbol, denomination.Symbol);
            writer.WriteString(EnglishName, denomination.Name);
            if (denomination.Unit is not null) writer.WriteString(Unit, denomination.Unit);
            if (denomination is Denomination { FormatPattern: var pattern } && pattern != Denomination.DefaultFormat) writer.WriteString(Format, pattern);
            writer.WriteEndObject();
            return;
        }

        if (IsKnownCurrency(value.Currency.CurrencyIsoCode))
        {
            writer.WriteString(CurrencyName, value.Currency.CurrencyIsoCode);
        }
        else
        {
            writer.WriteString(NativeName, value.Currency.NativeName);
            writer.WriteString(EnglishName, value.Currency.EnglishName);
            writer.WriteString(Symbol, value.Currency.Symbol);
            writer.WriteString(Iso, value.Currency.CurrencyIsoCode);

            if (value.Currency.DecimalPlaces != DefaultDecimalDigits)
            {
                writer.WriteNumber(DecimalDigits, value.Currency.DecimalPlaces);
            }
        }

        writer.WriteEndObject();
    }

    private static ICurrency HandleUnknownCurrency(
        ICurrency currency,
        string nativeName,
        string englishName,
        string symbol,
        string iso,
        int? decimalDigits)
    {
        if (!IsKnownCurrency(currency?.CurrencyIsoCode ?? string.Empty))
        {
            return new Currency(
                nativeName,
                englishName,
                symbol,
                iso,
                decimalDigits.GetValueOrDefault(DefaultDecimalDigits));
        }

        if (currency != null) return currency;

        throw new InvalidOperationException($"Invalid amount format. Must include a {nameof(currency)}.");
    }

    public static Amount ReadString(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        var parts = text.Split();
        if (parts.Length < 2)
        {
            throw new InvalidOperationException($"Unable to parse string amount \"{text}\".");
        }

        var currency = FromIsoCode(parts[0]);
        var value = decimal.Parse(string.Join(string.Empty, parts.Skip(1)), CultureInfo.InvariantCulture);

        return new(value, currency);
    }
}
