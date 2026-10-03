# Crest.Money.Domain

Money/currency value types adapted from OrchardCore.Commerce's
`OrchardCore.Commerce.MoneyDataType` library (MIT License, Copyright (c) 2018
OrchardCMS). Namespaces rewritten `OrchardCore.Commerce.MoneyDataType.*` ->
`Crest.Money.*`; behaviour unchanged.

Server-free on purpose: the same types round and serialize amounts identically in
the server and in the Blazor WASM client, so neither re-implements the rules.

- `Amount` - a decimal quantity bound to an `ICurrency`, with arithmetic and
  comparison operators and System.Text.Json serialization (`AmountConverter`).
- `Currency` / `KnownCurrencyTable` - the ISO currency catalog, culture-aware
  formatting.
- `ICurrencyProvider` / `CurrencyProvider` - enumeration + lookup. The module's
  server side adds a provider over the global store, so a shared correction to
  ISO 4217 minor units wins over the culture-derived table.
- `IMoneyService` - the seam consumers use (ensure/resolve currency, create
  amounts).
