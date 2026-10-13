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
record it) and `GlobalCurrencyProvider` reads `Crest.Data.Global`, which is Crest's already.

The currency reference data (`Data/currencies.json`, `CurrencyGlobalSchema`, stored under
the schema name `Crest.Money.Currencies`) is `Crest.Money`'s, since ISO 4217 minor units are
reference data every application needs and the global store is Crest's
([globals.md](globals.md)).

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

- [x] **Introduce the denomination.** `Amount` becomes value + denomination. A denomination
  has an identity (kind + code, unique per kind), a precision, a display rule, and
  optionally a unit (troy ounce, gram). A currency denomination points at the localization
  `Currency` and borrows its symbol and formatting; other kinds carry their own.
  Denominations come from providers, so a module can supply metals, crypto assets or points.
  Conversion between any two denominations goes through exchange rates (rate policy stays
  downstream). Precision up to 18 places (ETH) sits at the edge of `decimal`; check before
  committing to it. Touches the JSON converters, `PriceField` and about 11 `ICurrency`
  references across Crest and downstream modules.
  *What landed (2026-10-13):* `IDenomination` (kind, code, precision, symbol, name, unit,
  `Format`, and `Currency` when the kind is one) with `DenominationKinds` (currency, metal,
  crypto, points, credit; a module may use its own kind); `CurrencyDenomination` wraps an
  `ICurrency` and borrows its precision and formatting; `Denomination` carries its own,
  its display rule a composite format over number, symbol and unit. Identity is kind (ordinal)
  plus code (ignoring case, as currency codes compare). `Amount` is value + `Denomination`;
  `Amount.Currency` stays as the denomination's currency (the unspecified currency for any
  other kind), so the downstream `ICurrency` references compile unchanged, and
  `new Amount(value, ICurrency)` wraps the currency. Arithmetic and comparison refuse mixed
  denominations; `GetRounded` rounds to the denomination's precision; `ConvertTo(to, rate)`
  converts at a given rate. Providers: `IDenominationProvider` supplies non-currency
  denominations (currencies still come from `ICurrencyProvider`); `IExchangeRateProvider`
  answers a rate for a pair on a date, and `IMoneyService.ConvertAsync` asks them by
  priority (amended the same day, below: the exchange-rate seam). `IMoneyService.Denominations` and
  `GetDenomination(kind, code)` list and find them. JSON: a currency amount serializes
  exactly as before; any other kind writes itself whole (`kind`, `code`, `precision`,
  `symbol`, `englishname`, `unit`, `format`) so it reads back with no provider. Precision is
  capped at `Denomination.MaxPrecision` = 18: at scale 18 `decimal` still holds about 7.9e10
  whole units, enough for an ether amount in wei; higher scales would shrink the integer
  part below what a ledger needs. Tests in `Crest.Money.Tests/DenominationTests.cs`. Not
  done: `PriceField` editing a non-currency denomination (there is no Money editor yet, see
  the value type registration) and a currency setting in a Regional Profile.
  *Exchange rates, the wire shape and the currency picker (2026-10-13, moved down from a
  host):* an `ExchangeRate` is from, to, rate, date and source (the provider's name,
  `Identity` for a pair with itself) and converts an amount. `IExchangeRateProvider` has a
  `Name` and a `Priority` (higher first) and answers an `ExchangeRate` or declines with null.
  `IExchangeRateRegistry` is what callers inject: it walks the providers by priority with a
  per-scope cache, logs a provider that throws and moves on, and when nobody answers, the
  tenant's `ExchangeRateSettings.FailureBehavior` (site settings) decides: `Continue` answers
  null, `Block` throws `ExchangeRateUnavailableException`. `ManualExchangeRateProvider` is
  always registered at the lowest priority over the tenant's own table
  (`ExchangeRateSettings.Rates`: effective-dated, latest on or before the date, the inverse of
  a stated rate is a stated rate), so a multi-currency application converts with no
  line-of-business module; a feed is one more provider. `MoneyModel` (value, currency code)
  is the wire shape of an `Amount` in APIs and client models, null for no amount, with
  `MoneyModel.From(amount)` and `MoneyModel.ToAmount(model, money)`. The `currency` option
  source (`CurrencyOptionSourceProvider`) gives option pickers the currency system (ISO code,
  name, native name, symbol, decimal places; the ISO code is both row id and key), so
  currencies are never a seeded list. No admin page edits the rate table yet.
- [x] **Fix the currency lookup order.** `Currency.FromIsoCode` checks the culture table
  before the registered providers, so a provider cannot override a code the culture table
  already knows, contrary to `GlobalCurrencyProvider`'s comment.
  *What landed (2026-10-13):* `Currency.FromIsoCode` (and `FromRegion`) ask the providers
  in registration order first, the culture table behind them; `MoneyService.Currencies` lists
  each code once, from the first provider that knows it. Tests in `Crest.Money.Tests`.
