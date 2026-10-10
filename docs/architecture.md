# Crest architecture

Crest is a platform and the application layer built into it. This document is its shape: what it holds, the
modules that make it up, who signs in to it, and the rules that keep downstream modules plugging
into it rather than it reaching for them. The global store, the geographic tree and money have
their own documents: [global-store.md](global-store.md), [regions.md](regions.md),
[money.md](money.md).

## What Crest is

Crest has two halves in one repository. **The platform** (the projects listed in
`src/Crest.Build/Platform.Projects.props`, and `test/`) is a hard fork of OrchardCore: tenants, users, permissions, content, features, settings and the module
system. Crest develops it as its own code. **The application layer** is everything a business-facing
application needs that is not a line of business. Its foundation is built — the Blazor admin and
site shells, the content-item API, icons, localization, Content Part Lists, the global
store and the geographic tree. Three modules complete it:

| Module | What it is |
| --- | --- |
| `Crest.Parties` | Who an application deals with: Person and Organization content types, contact points, addresses, positions, a role registry (`IPartyTypeProvider`) that downstream modules register their party roles with (customer, vendor, employee, lead, …), and the parties UI. |
| `Crest.Members` | The other side of the business: member accounts (ordinary Orchard users with a class marker), organization bindings with per-organization roles, a hierarchy store, the member portal (login, register, external login, pages), and impersonation for staff support. Commercial memberships (tiers, seats, groups, perks, entitlements) are a downstream module's, which hooks member lifecycle events through `IMemberLifecycleHandler`, a seam Members declares. |
| `Crest.Workflows` | A workflow service for Orchard built on a vendored fork of Elsa 3 (MIT): a registry of activities, triggers, hook slots and flows that modules contribute; units of work (one request, one session, one transaction, with inline hooks and durable background calls); connectors with API-key, bearer, basic and OAuth2 authentication and OpenAPI import; approvals; field dependencies; ownership tiers and per-flow access; the stock-Orchard bridge; the designer bridge. |
| `Crest.Money` | The money type and the `PriceField` a tenant puts on any content type: `Amount` bound to an `ICurrency`, ISO 4217 metadata from the global store so minor units are never a per-tenant guess, and a default currency. Adapted from OrchardCore.Commerce (MIT). See [money.md](money.md). |

The thesis: **all an application needs is parties, workflows and content items.** A
party is who, a workflow is what happens, a content item is everything else. Line-of-
business objects (accounts, inventory, transactions, invoices) are content items with a
registry and a discipline, and they belong to the modules that own that business.
Members is Crest's because every business with a public face has members, customers or
users on the other side of it, and the member portal is how a product reaches them.

## Direction: Blazor or headless, nothing else

Crest hard-forked OrchardCore (ruling 2026-10-09); its projects sit beside Crest's own under
`src/`. When the platform lacks
something, the platform is changed: no shims, no stock-id module forks, no workarounds
kept to stay mergeable. **The platform is renamed from `OrchardCore` to `Crest`, fully** (ruling 2026-10-09):
namespaces, assemblies, project and folder names, package ids, feature and module ids,
recipes, configuration sections and static asset paths. Dev tenants are reset. Identifiers
that carry the project name become neutral, per the naming rule (`PlatformBuilder` →
`PlatformBuilder`, `AddPlatform()` → `AddPlatform()`, `IPlatformHelper` →
`IPlatformHelper`, JS globals likewise), so a later rename touches module names only. Dotted
module, namespace and package segments become `Crest`; prose "Orchard Core" becomes "Crest".
External URLs and the `OrchardCore.Translations` packages are left as they are. The stock
`Crest.Workflows.Platform` module is merged into `Crest.Workflows` as part of the rename, and
the four sample site themes (TheTheme, TheBlogTheme, TheAgencyTheme, TheComingSoonTheme) are
pruned first. Neither the platform nor Crest.Server is named just `Crest`: the
platform's core library becomes `Crest.Core` and Crest.Server's assembly and package become
`Crest.Server`. Where a renamed platform module or type meets existing Crest code, each clash
is resolved case by case: delete the side Crest has already replaced, or merge Crest code
that was written to sit over the platform into the platform module. Before the rename, only
platform modules **already rewritten in Crest code** are pruned; modules Crest simply has
not reached yet stay. As each Blazor replacement lands, the Liquid templates and the stock Razor/Vue
UIs it replaces are removed, until every page is a Blazor build. Headless (the content-item
API, GraphQL, Queries) is first class alongside Blazor. Every capability a Blazor page has
is reachable through the API, and no feature exists only in the UI. Stock front-end assets
and their build pipeline shrink with the UIs they serve.

Everything is streamlined to Blazor: no work goes into other UI systems. Templates, layouts,
pages and content items become data rendered into Blazor components through the platform's own
display system, with its HTML binding replaced by a component binding
([blazor-display.md](blazor-display.md)). There is **one
expression engine** for the whole application: query parameters, route and title patterns,
workflow expressions, notification and email templates. Liquid as a view engine goes with
the stock UIs.

- **For now, keep Liquid (Fluid)** for expressions, for ease of migration.
- **The workflow engine's expression system is the one engine** (ruling 2026-10-09, plan in
  [blazor-display.md](blazor-display.md) § 12): its expression model, descriptors and input
  metadata are the contract for workflows and for display; the platform's Liquid becomes its
  Liquid implementation; the platform-side evaluators and **`Crest.Scripting` are removed**,
  replaced by workflows. A workflow is written as a script or edited as a flow diagram; both are
  front ends for the same objects.
  - Every read and write is an **operation** in one registry (the engine's descriptors plus a
    query kind), behind one access machinery, with the API surfaces generated from it
    ([operations.md](operations.md), ruling 2026-10-10).
  - Liquid is the default language.
  - Admins can grant C#, JavaScript and Python scripting access. Granting is only the first
    step toward sandboxing: those providers run code on the server and must be hardened
    before any tenant uses them. Neither C# nor Python exists in the vendored engine yet.

## Three shells

Crest serves three audiences, each with its own shell: the **admin** shell for staff, the
**site** shell for the public, and the **member** shell — the application a member or
customer actually uses, optional and present when Members is enabled. Modules contribute
pages to a shell rather than shipping their own. Themes are not interchangeable: a theme
declares which shell it serves and whether it hosts a Crest shell document, and a module
declares the contract its pages need. See [shells-and-themes.md](shells-and-themes.md).

## The user vocabulary matches the shells

Crest knows exactly three kinds of visitor, one per shell:

- **users** — tenant users, who sign in to the admin shell;
- **members** — organization-bound users, who sign in to the member shell;
- **anonymous** — the site shell's visitors.

Every one of them is an ordinary OrchardCore user in the tenant's own store (anonymous
aside); the class is a marker that decides which login surface accepts the account, and
Crest owns it along with the org bindings, the hierarchy and the surface separation.

**Employee is not one of them.** An employee is a person the business employs — an
employment record, a party role, payroll — so it belongs to a downstream module. Some
employees are tenant users and some never sign in at all; Crest neither knows nor cares.
Products keep the three words and label them for their domain without renaming anything.

**Super-tenancy splits.** The Default tenant, cross-tenant scope
(`IShellHost.GetScopeAsync`) and the platform-user class are OrchardCore concepts, so the
mechanism and its seams are Crest's. A SaaS product built on them — tenant provisioning
and lifecycle UI, plans and billing, operator dashboards, the hosting infrastructure — is
not.

**Crest.Members keeps its own permissions and hierarchy.** Member roles, the org-admin
ceiling and the per-organization permission scope are Crest's, parallel to but separate
from the tenant-user side: a product on Crest alone needs members with differentiated
access and org admins who manage their own people, with no line-of-business module present.
A downstream module's entitlements are a layer above those permissions, never a replacement
for them.

## Parties is the model, not the roles

Crest has the party **model** — there is no engine to speak of: content types (Person,
Organization, Address, ContactPoint, Position, Location, the `parties.*` vocabularies on
`Crest.ContentPartLists`, the address's geo stack (`GeoStackField`) on `Crest.Regions`), the contacts /
positions / locations services and API, the Person ↔ portal-user link, three indexes, the
`IPartyTypeProvider` registry with the menu and generic role pages generated from its
catalog, and two workflow activities. It is Crest's for two reasons, neither of them
cleverness: Members depends on it (a member is a Person with a portal user; an org binding
points at an Organization), and every product needs people and organizations before it needs
anything else. It ships no party role of its own.

Parties defines **no field types of its own**: the party picker on a role is a stock
`ContentPickerField` constrained to Person/Organization, the portal-user link a stock
`UserPickerField`, an address's country and the levels below it a `GeoStackField` from
`Crest.Regions`, and honorific, address kind, phone country, organization type and
industry are `OptionPickerField`s from `Crest.ContentPartLists`. The only Parties-specific
helper is `PartyRoleDefinitions` ("define a role type: party picker + number field +
profile"), which downstream modules and products call. The role content types are defined
and registered by the modules that own them.

## Every content field type lives in Crest

Field types are platform vocabulary: a tenant composes content from them in the type
editor, and an application on Crest alone must be able to model prices, places and option
sets without a line-of-business module. So **every custom `ContentField` belongs to Crest**
unless it is proprietary or depends on proprietary code. Three exist: `OptionPickerField`
(Crest.Server), `GeoStackField` (Crest.Regions) and `PriceField` (Crest.Money,
[money.md](money.md)).

## Naming

`Crest.Parties`, `Crest.Members`, `Crest.Workflows`, matching `Crest.Regions`,
`Crest.Global`, `Crest.ContentPartLists`. Namespaces, assembly names, feature ids,
manifests, permission names, option-set keys (`parties.*` — already unbranded), route
constants and test names follow.

**"Crest" appears only in module naming (ruling 2026-10-06).** Modules, assemblies and
packages carry the `Crest.` prefix; nothing inside them does — not permission names,
types, functions, settings keys or other internal names (`ManageConnections`, not
`ManageCrestWorkflowConnections`). The project must be renameable by renaming its modules
alone. The same rule holds for downstream projects. OrchardCore's own names are not ours to
change.

**No downstream name appears in Crest.** Crest is open source and knows nothing of the
products built on it — not in a namespace, an assembly name, a route, a comment, a test name
or a fixture. That includes the vendored engine subtree:
`engine/src/modules/Crest.Workflows.Core`, `.Engine`, `.Api` and the rest are
`Crest.Workflows.*`, with their namespaces and internal references renamed with them. The
engine API prefix is `crest-workflows/api` and activity type names are
`Crest.Workflows.*`.

The cost is accepted deliberately: the subtree is tracked against
`engine/UPSTREAM-COMMIT`, and renaming it makes future upstream diffs noisier. Pulling an
upstream Elsa change therefore means applying it against renamed files — a mechanical
rename of the incoming diff, documented at the top of the subtree
([UPSTREAM.md](../Crest.Workflows/UPSTREAM.md)) so whoever does it next knows.
`engine/LICENSE` (Elsa, MIT) and `UPSTREAM-COMMIT` stay exactly as they are; the fork's
provenance is recorded in the subtree README rather than in its type names.

## Rules that hold

- Crest never references a downstream module. Downstream modules register with the
  registries Crest declares (`IPartyTypeProvider`, workflow activity/trigger/hook/flow
  providers) and attach their own parts to Crest's content types from their own
  migrations. The consumer owns the interface; the host composes.
- No literal path strings; routes flow through the platform's own systems (see
  [agents.md](../agents.md)).
- Pre-release: no compatibility code; restructure outright. Until the first public release,
  migrations are edited in place and dev tenants are reset when shipped data changes
  (ruling 2026-10-06); from the first public release on, every change ships an upgrade step
  (`UpdateFrom`, backfill tasks). A plan that relies on a reset says so.
- Blazor pages and components that depend on a platform module being enabled are kept
  isolated, one place per module, so they move with that module when it is reworked or
  removed (ruling 2026-10-06).

## The rules that keep it composable

1. **Dependencies run one way.** Application layer (Crest: content, Parties, Members,
   Workflows, UI and API) ← downstream modules (a line-of-business layer's registries and
   its domain modules); products sit beside them and reference Crest. A downstream module
   injects upward through a seam the upstream module declares (interfaces, registries, page
   regions, part attachment from the downstream module's own migration). Upstream code never
   names a downstream type.
2. **The consumer owns the interface.** A module that consumes tax resolution owns its
   resolver interface; a tax module implements it. Parties consumes party types, so
   `IPartyTypeProvider` is Parties'; a module with a party role implements it.
   **Nothing declares its own version of a core object.** A module with a new kind of
   party or workflow registers it with the registry that owns that object; the registry is
   where the shape, the menu, the pages and the cross-module lookups come from, once.
3. **Content types are the model.** Parties, roles and the records downstream modules keep
   are content types built from parts and fields, editable at runtime in the type editor.
   New fields are configuration, not code.
4. **Global data is global; features are per tenant.** Reference data every tenant
   shares (geo tree, currency metadata, standard lists and classifications) lives once in
   the global store and is read-only in tenants; a tenant enables the *features* that
   read it and holds its own shims (registrations, mappings, preferences) against it.
5. **Ids for code, snapshots for history, display text for humans.** Code matches keys
   and content item ids; posted documents freeze the values they must show forever;
   people read display text. Nothing aggregates on a string.
6. **Permissive licences only.** Designs and code adapted from MIT/Apache/BSD/MPL
   sources at file boundaries, attribution kept; nothing copyleft consulted.

Not yet built: [architecture.md](architecture.md).

## Still to build

Built: see [docs/architecture.md](architecture.md) — Parties, Members, Workflows and
Money are here, and so is the member shell ([shells-and-themes.md](shells-and-themes.md)
phases 1–2). This section is the checklist of what remains, in build order.

The global store's host-call rough edge is in [global-store.md](global-store.md).

### Standing alone

- [ ] **3. Add a party role of Crest's own.** A party role of Crest's own (or a test fixture
  that registers one), so the generic role pages can be exercised without a host's modules.
- [ ] **4. Remove the host references below, and add the publishing gate check.** Crest must
  build, test and read without any host present.
  - [ ] `Crest.Tests.slnx` lists a host's projects. Root cause:
    `Crest.AdminTheme.Client.csproj` globs `..\..\..\**\blazor-wasm\*.csproj`, reaching out
    of the repository. Replace the glob with an item the host supplies.
  - [ ] Workflow object constants and a payload fallback key name a host's records; object
    names are free strings, and the subject key becomes a generic `ContentItemId`.
  - [ ] Test fixtures and code comments use a host's flow keys and vocabulary; reword them
    generically.
  - [ ] A global list for item classification ships in Crest although a downstream module
    seeds and owns it; it moves to that module's global schema.
  - [ ] Two code mentions name a product (the member home page, the members portal check).
  - [ ] **Publishing gate:** no host or product name anywhere in the repository, checked
    automatically in Crest's test run (the list of names comes from configuration outside
    the repository, so Crest never contains them).
  - [ ] **Before publishing code that moved in from a private host:** no secrets, tokens,
    connection strings or real credentials in the moved trees (`Data/`, recipes, test
    fixtures, `appsettings*`); test fixtures use invented people and organizations only;
    third-party attribution present (Elsa's MIT licence for the engine and the Studio fork,
    and the header of anything else adapted at file boundaries); the moved plans read
    without the host's repository; the README lists the modules.
- [ ] **5. Build and test a host that has no line-of-business modules** as a fresh install,
  to prove Crest stands alone.
- [ ] **6. Move the generic pieces below into Crest, one at a time.** Hosts built on Crest
  currently carry their own copies of things every Crest application needs. Each belongs in
  Crest once a host-neutral version exists; none references a host's types.
  - [ ] **A currency option source** over `ICurrencyProvider`, in `Crest.Money` (the content
    items doc already lists `currency` as a source a host supplies).
  - [ ] **A money wire DTO** (value and currency) for `Amount` over the API, in `Crest.Money`.
  - [ ] **An exchange-rate seam** (rate, policy, registry, manual rates) in `Crest.Money`, so a
    multi-currency application converts without a line-of-business module.
  - [ ] **Workflow browser checks** for connectors, ownership tiers, party triggers and engine
    tenant isolation, written against a Crest fixture (a content-published trigger, a
    shipped fixture flow, a fixture party role) instead of a host's documents. The tenant
    isolation check runs last.
  - [ ] **Workflow editor and viewer role stereotypes** granting only the Crest workflow
    permissions, so every host can run the ownership checks.
  - [ ] **Host defaults:** the data-root shell-options override and the no-cache static files
    fix, as a Crest server host extension (configuration key `Crest:DataRoot`).
  - [ ] **Kind-registry plumbing:** the catalog, menu, route gates, "All" page and per-kind pane
    that `Crest.Parties` implements, as a generic `KindCatalog<T>` and kind pane hosts reuse
    for their own registries.
  - [ ] **Gap-free record numbering** as a Crest server service.
  - [ ] **A content-part-list import** that upserts options by key and never overwrites a
    tenant's label.
  - [ ] **Shared dev tooling:** feed pack and check, stopping the local server, the throwaway
    test server, the solution-membership gate, the legacy-script filter, provisioning
    verification and the suite entry point, as `tools/dev-lib.sh` and a `runHostSuite()`
    harness helper. Hosts pass their solution, features, ports and credentials.

- [ ] **The query system and the connection system (Workflows depends on it)** — planned in
  [queries.md](queries.md).

### Recorded for later

- [ ] **Organization ownership of records.** Applications that serve many organizations inside
  one tenant need records that belong to an organization and are visible only to its members,
  fail-closed (no organization binding means no records). Members already rules the scope; the
  generic facility — an owner-organization part attachable to any content type, its index
  column, a query scope the member portal applies — is designed when the first application
  asks for it.

### Decisions needed

- [ ] **Translations of Crest's own UI.** Hosts currently ship them in their own catalogs, so
  a second host gets an untranslated admin. Ship them with Crest, or keep translations
  the host's?

