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

## Currency and money are separate concepts

- **Currency is localization.** A country or region's legal tender and how it is written:
  ISO 4217 codes, symbols, names, minor units, culture formatting. It belongs with Regions
  and localization (a Regional Profile's default currency). `ICurrency` and the global
  currency table are only this.
- **Money is transactional.** An amount of something that can be held, transacted and
  converted: a **denomination**, which is a currency, or gold or silver by weight, a crypto
  asset, points or store credit. None of the non-currency kinds is a currency; they are still
  transacted and converted through the money types.

### Denominations

`Amount` is value + `Denomination`. `IDenomination` has a kind, a code, a precision, a symbol,
a name, an optional unit (troy ounce, gram), `Format`, and `Currency` when the kind is one.
`DenominationKinds` lists currency, metal, crypto, points and credit; a module may use its own
kind. Identity is kind (ordinal) plus code (ignoring case, as currency codes compare).

- `CurrencyDenomination` wraps an `ICurrency` and borrows its precision and formatting;
  `Denomination` carries its own, its display rule a composite format over number, symbol and
  unit.
- `Amount.Currency` is the denomination's currency (the unspecified currency for any other
  kind), and `new Amount(value, ICurrency)` wraps the currency, so code written against
  `ICurrency` keeps working.
- Arithmetic and comparison refuse mixed denominations; `GetRounded` rounds to the
  denomination's precision; `ConvertTo(to, rate)` converts at a given rate.
- Precision is capped at `Denomination.MaxPrecision` = 18: at scale 18 `decimal` still holds
  about 7.9e10 whole units, enough for an ether amount in wei; higher scales would shrink the
  integer part below what a ledger needs.
- `IDenominationProvider` supplies non-currency denominations (currencies come from
  `ICurrencyProvider`); `IMoneyService.Denominations` and `GetDenomination(kind, code)` list and
  find them.
- JSON: a currency amount serializes as a plain currency amount; any other kind writes itself
  whole (`kind`, `code`, `precision`, `symbol`, `englishname`, `unit`, `format`) so it reads
  back with no provider.

Tests are in `Crest.Money.Tests/DenominationTests.cs`.

### Exchange rates, the wire shape and the currency picker

- An `ExchangeRate` is from, to, rate, date and source (the provider's name, `Identity` for a
  pair with itself) and converts an amount.
- `IExchangeRateProvider` has a `Name` and a `Priority` (higher first) and answers an
  `ExchangeRate` or declines with null.
- `IExchangeRateRegistry` is what callers inject (`IMoneyService.ConvertAsync` uses it): it
  walks the providers by priority with a per-scope cache, logs a provider that throws and moves
  on, and when nobody answers, the tenant's `ExchangeRateSettings.FailureBehavior` (site
  settings) decides: `Continue` answers null, `Block` throws `ExchangeRateUnavailableException`.
- `ManualExchangeRateProvider` is always registered at the lowest priority over the tenant's
  own table (`ExchangeRateSettings.Rates`: effective-dated, latest on or before the date, the
  inverse of a stated rate is a stated rate), so a multi-currency application converts with no
  line-of-business module; a feed is one more provider. Rate policy stays downstream.
- `MoneyModel` (value, currency code) is the wire shape of an `Amount` in APIs and client
  models, null for no amount, with `MoneyModel.From(amount)` and `MoneyModel.ToAmount(model,
  money)`.
- The `currency` option source (`CurrencyOptionSourceProvider`) gives option pickers the
  currency system (ISO code, name, native name, symbol, decimal places; the ISO code is both
  row id and key), so currencies are never a seeded list.
- `Currency.FromIsoCode` (and `FromRegion`) ask the registered providers in registration order
  first and the culture table behind them, so a provider can override a code the culture table
  knows; `MoneyService.Currencies` lists each code once, from the first provider that knows it.

## Still to build

- [ ] `PriceField` editing a non-currency denomination (there is no Money editor yet; see the
  value type registration).
- [ ] A currency setting in a Regional Profile.
- [ ] An admin page for the tenant's exchange-rate table.
- [ ] A tenant settings page for the default currency (`CurrencySettings.DefaultCurrency` is
  the seam).
