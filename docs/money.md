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

## Still to build

### Currency and money are separate concepts (ruling 2026-10-06)

Today they are tied together: `Amount` is `decimal Value` + `ICurrency Currency`, and
`ICurrency` is a localization object (ISO code, symbol, names, decimal places, formatting
through a `CultureInfo`), built from a culture or region (`new Amount(value, RegionInfo)`,
`Currency.FromCulture`). So anything transacted has to pretend to be a currency.

- **Currency is localization.** A country or region's legal tender and how it is written:
  ISO 4217 codes, symbols, names, minor units, culture formatting. It belongs with Regions
  and localization (a Regional Profile's default currency). `ICurrency` and the global
  currency table go back to being only this.
- **Money is transactional.** An amount of something that can be held, transacted and
  converted. That something — working name **denomination** — is a currency, or gold or
  silver by weight, a crypto asset, points or store credit. None of the non-currency kinds
  is a currency; they can still be transacted and converted through the money types.

- [ ] **Introduce the denomination.** `Amount` becomes value + denomination. A denomination
  has an identity (kind + code, unique per kind), a precision, a display rule, and
  optionally a unit (troy ounce, gram). A currency denomination points at the localization
  `Currency` and borrows its symbol and formatting; other kinds carry their own.
  Denominations come from providers, so a module can supply metals, crypto assets or points.
  Conversion between any two denominations goes through exchange rates (rate policy stays
  downstream). Precision up to 18 places (ETH) sits at the edge of `decimal`; check before
  committing to it. Touches the JSON converters, `PriceField` and about 11 `ICurrency`
  references across Crest and downstream modules.
- [ ] **Fix the currency lookup order.** `Currency.FromIsoCode` checks the culture table
  before the registered providers, so a provider cannot override a code the culture table
  already knows, contrary to `GlobalCurrencyProvider`'s comment.
