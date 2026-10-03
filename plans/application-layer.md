# Crest as an application layer: Parties, Members, Workflows and Money

## Status

**The three modules are here** (Parties, Members, Workflows), along with `Crest.Money`
— the money type and the `PriceField`, since every content field type belongs to the
application layer. The member shell
([shells-and-themes.md](shells-and-themes.md)) is the remaining large piece.

## What Crest is

Crest is an **application layer** on Orchard: everything a business-facing application
needs that is not a line of business. The platform half is built — the Blazor admin and
site shells, the content-item API, icons, localization, Content Part Lists, the global
store and the geographic tree. Three modules complete it:

| Module | What it is |
| --- | --- |
| `Crest.Parties` | Who an application deals with: Person and Organization content types, contact points, addresses, positions, a role registry (`IPartyTypeProvider`) that downstream modules register their party roles with (customer, vendor, employee, lead, …), and the parties UI. |
| `Crest.Members` | The other side of the business: member accounts (ordinary Orchard users with a class marker), organization bindings with per-organization roles, a hierarchy store, the member portal (login, register, external login, pages), impersonation for staff support, and memberships (tiers, seats, groups, perks, entitlements). Billing a membership is left to a downstream module through a seam Members declares. |
| `Crest.Workflows` | A workflow service for Orchard built on a vendored fork of Elsa 3 (MIT): a registry of activities, triggers, hook slots and flows that modules contribute; units of work (one request, one session, one transaction, with inline hooks and durable background calls); connectors with API-key, bearer, basic and OAuth2 authentication and OpenAPI import; approvals; field dependencies; ownership tiers and per-flow access; the stock-Orchard bridge; the designer bridge. |
| `Crest.Money` | The money type and the `PriceField` a tenant puts on any content type: `Amount` bound to an `ICurrency`, ISO 4217 metadata from the global store so minor units are never a per-tenant guess, and a tenant-chosen default currency. Adapted from OrchardCore.Commerce (MIT). |

The thesis: **all an application needs is parties, workflows and content items.** A
party is who, a workflow is what happens, a content item is everything else. Line-of-
business objects (accounts, inventory, transactions, invoices) are content items with a
registry and a discipline, and they belong to the modules that own that business.

## Three shells

Crest serves three audiences, each with its own shell: the **admin** shell for staff, the
**site** shell for the public, and the **member** shell — the application a member or
customer actually uses, optional and present when Members is enabled. Modules contribute
pages to a shell rather than shipping their own. Themes are not interchangeable: a theme
declares which shell it serves and whether it hosts a Crest shell document, and a module
declares the contract its pages need. See [shells-and-themes.md](shells-and-themes.md).

## Rules that hold

- Crest never references a downstream module. Downstream modules register with the
  registries Crest declares (`IPartyTypeProvider`, workflow activity/trigger/hook/flow
  providers) and attach their own parts to Crest's content types from their own
  migrations. The consumer owns the interface; the host composes.
- No literal path strings; routes flow through Orchard's own systems (see
  [agents.md](../agents.md)).
- Pre-release: no compatibility code; restructure outright.

## Recorded for later: organization ownership of records

Applications that serve many organizations inside one tenant need records that belong
to an organization and are visible only to its members, fail-closed (no organization
binding means no records). Members already rules the scope; the generic facility — an
owner-organization part attachable to any content type, its index column, a query scope
the member portal applies — is designed when the first application asks for it.

## Known rough edge: the global store needs a host call

`Crest.Global`'s `ICrestGlobalStore` is registered by an `OrchardCoreBuilder` extension
(`AddCrestGlobalStore()`), not by the feature, because the store is one database shared by
every tenant and so belongs to the host's service collection. A host that enables
`Crest.Global`, `Crest.Regions` or `Crest.Money` without making that call gets no warning:
the features enable, and setup then fails with "Unable to resolve service for type
ICrestGlobalStore". Enabling a feature should not depend on an invisible second step —
either the features should fail enablement with a clear message, or the registration
should happen where the feature is. Recorded rather than fixed; it bit the standalone host
first and will bit the next one.

## Phases

- [x] 0. This plan; the shell and theme model in [shells-and-themes.md](shells-and-themes.md).
- [x] 1. `Crest.Parties`, `Crest.Members`, `Crest.Workflows` and `Crest.Money` arrived
      with their tests (xUnit projects under each module's `tests/`, discovered by
      `tests/run-tests.sh`; browser checks in each module's
      `tests/playwright/checks`), the workflow docs (`docs/workflows.md`,
      `docs/workflows.mmd`) and plans (`plans/workflows.md`, `plans/members.md`,
      `plans/machine-actors.md`); READMEs updated.
- [ ] 2. The member shell, so a product's pages have somewhere to live that is not the
      admin bucket ([shells-and-themes.md](shells-and-themes.md) phases 1–2).
- [ ] 3. A party role of Crest's own (or a test fixture that registers one), so the
      generic role pages can be exercised without a host's modules.
