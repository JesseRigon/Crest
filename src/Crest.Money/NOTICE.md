# Third-party notices

The money value types and the price content field in this module are adapted from
[OrchardCore.Commerce](https://github.com/OrchardCMS/OrchardCore.Commerce) — its
`OrchardCore.Commerce.MoneyDataType` library, `MoneyService`, and the `PriceField` /
`PriceFieldSettings` pair from its ContentFields module. Namespaces were rewritten
`OrchardCore.Commerce.MoneyDataType.*` → `Crest.Money.*`; the behaviour is unchanged.
Each adapted file carries the attribution in its own header, and `Domain/README.md`
records what was taken.

Adapted: `Domain/Amount.cs`, `Domain/Currency.cs`, `Domain/Currency.extra.cs`,
`Domain/CurrencyProvider.cs`, `Domain/KnownCurrencyTable.cs`,
`Domain/Abstractions/*.cs`, `Domain/Extensions/*.cs`, `Domain/Serialization/*.cs`,
`Fields/PriceField.cs`, `Fields/PriceFieldSettings.cs`, `Services/MoneyService.cs`.

Crest-owned (no third-party origin): `Startup.cs`, `Manifest.cs`,
`MoneyConstants.cs`, `Settings/CurrencySettings.cs`, `Global/CurrencyGlobalSchema.cs`
and `Services/GlobalCurrencyProvider.cs`, which put the ISO 4217 table in Crest's
global store.

`Data/currencies.json` is ISO 4217 reference data (codes, names, symbols and minor
units), not a third-party work.

OrchardCore.Commerce is Copyright (c) 2018 Crest and licensed under the MIT
License:

> Permission is hereby granted, free of charge, to any person obtaining a copy of this
> software and associated documentation files (the "Software"), to deal in the Software
> without restriction, including without limitation the rights to use, copy, modify,
> merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
> permit persons to whom the Software is furnished to do so, subject to the following
> conditions: The above copyright notice and this permission notice shall be included in
> all copies or substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS",
> WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
> WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
> IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR
> OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
> OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
> SOFTWARE.
