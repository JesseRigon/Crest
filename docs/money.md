# Money

`Crest.Money` is the money type and the `PriceField` a tenant puts on any content type:
`Amount` bound to an `ICurrency`, ISO 4217 metadata from the global store so minor units are
never a per-tenant guess, and a default currency. It is Crest's because every content field
type is ([architecture.md](architecture.md) › Every content field type lives in Crest): an
application on Crest alone must be able to model prices without a line-of-business module.

## What it holds

| Project | Contents |
| --- | --- |
| `Crest.Money/Domain` (server-free, the shape `Crest.Regions.Domain` has) | `Amount`, `Currency`, `CurrencyProvider`, `KnownCurrencyTable`, the `ICurrency`/`ICurrencyProvider`/`ICurrencySelector`/`IMoneyService` abstractions, the JSON converters (`AmountConverter`, `CurrencyConverter`), the culture extensions |
| `Crest.Money` (server side) | `PriceField`, `PriceFieldSettings`, `MoneyService`, `CurrencySettings`, `GlobalCurrencyProvider`, the currency global schema and `currencies.json` |

Nothing in it is proprietary: the money types and `PriceField` are adapted from
OrchardCore.Commerce (MIT, attribution headers kept, `Domain/README.md` and `NOTICE.md`
record it) and `GlobalCurrencyProvider` reads `Crest.Global`, which is Crest's already.

The currency reference data (`Data/currencies.json`, `CurrencyGlobalSchema`, stored under
the schema name `Crest.Money.Currencies`) is `Crest.Money`'s, since ISO 4217 minor units are
reference data every application needs and the global store is Crest's
([global-store.md](global-store.md)).

The default currency is `CurrencySettings.DefaultCurrency`, falling back to US dollars when
unset. There is no tenant settings surface for it yet: the class is the seam a Crest settings
page writing site settings will fill.

What a downstream module keeps is the *use* of money, not the type: currency tiers, its own
context facets, pricing strategies and price books, exchange-rate policy, pricing maths, and
option sources over `ICurrencyProvider`.
