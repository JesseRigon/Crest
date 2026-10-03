// Adapted from OrchardCore.Commerce's MoneyDataType library
// (https://github.com/OrchardCMS/OrchardCore.Commerce, MIT License,
// Copyright (c) 2018 OrchardCMS).
using Crest.Money.Abstractions;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Crest.Money.Serialization;

public sealed class CurrencyConverter : JsonConverter<ICurrency>
{
    public override ICurrency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Currency.FromIsoCode(reader.GetString());

    public override void Write(Utf8JsonWriter writer, ICurrency value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.CurrencyIsoCode);
}
