# The global store — tenant-less reference data

Reference data that is the same for every tenant is stored **once**, in a YesSql store that
belongs to no tenant (`Grove.Data.Global`). The Default tenant is its only editor. Tenants read it
and may layer their own additions and presentation over it; they never modify it. `Grove.Data.Global` is the home of the standardized global data set
imports and loading and of the global data registry, so no module reimplements either
([blazor-display.md](blazor-display.md) › Module homes). What is not built yet is under
[Still to build](#still-to-build).

## Why

Reference data that is the same for every tenant is not copied into every tenant. The geo tree
is ~60k nodes for the US alone before boundaries; postal codes ~40k; authority catalogues;
classification lists. A copy per tenant is waste, and correcting it would mean N migrations
instead of one.

Ownership decides it too. Nothing about "Illinois" or "ISO 4217 JPY = 0 decimals" is a tenant's
to edit; only labels, ordering, visibility and additions are.

## The mechanism — Orchard's own precedent

Orchard can store tenant settings in a database rather than `tenants.json`
(`DatabaseShellsSettingsSources`). That data belongs to no tenant, so Orchard opens a YesSql
session against a **store of its own** — its own `DatabaseProvider`, `ConnectionString`,
`TablePrefix` and `Schema`, configured under `Grove_Shells_Database`.

Grove does the same: a **`GlobalStore`** — one host-level service that owns a
tenant-less YesSql store configured under its own section (`Grove_Global`,
same four fields). Full YesSql: documents, map indexes, SQLite, Postgres, SQL Server or
MySQL. Shell services resolve host services, so tenant code queries it like any other
service.

Rather than building a synthetic `ShellSettings` and asking `IShellContextFactory` for a
context, as Orchard's `internal` helper does, the store builds its YesSql `Configuration`
directly from those options, the way Grove.Data builds a tenant's store, so no shell is
involved. Modules contribute their global documents as `GlobalDataMigration` types (the
platform's `DataMigration` convention: `CreateAsync`, then `UpdateFromNAsync`), discovered by
scanning the loaded module assemblies, so the host does not name each one.

**Topology is configuration, as it is for Orchard.** Orchard places tenants in one database
by table prefix, or in separate databases by connection string, per configuration. The global
store is the same: with no section configured it is a SQLite file (`grove-global.db`) in a
`GroveGlobal` folder beside the tenant folders, with the table prefix `GroveGlobal`; an
operator who wants it elsewhere (Postgres, SQL Server, MySQL) sets the provider and connection
string, exactly as they would for `Grove_Shells_Database`.

**No cross-store joins**, and none needed. Tenant indexes hold global ids (stable strings,
see below); lookups query the global store with an `IN`. Two indexed queries, not one join.

### The feature registers it

The `Grove.Data.Global` feature registers `IGlobalStore` and `IGlobalCache` in each
tenant that enables it, and every tenant of a host gets the same instance (`GlobalHost`,
keyed by the host), built from host-level services only and released when the application
stops. A host makes no call of its own. The feature also adds the
`ManageGlobalReferenceData` permission.

## Who can write

**Only the Default tenant** (`ShellSettings.IsDefaultShell()`, which Grove's
`TenantsController` already special-cases) — the super-tenant: the host operator's own
tenant, never a customer's. Ordinary tenants hold **no permission of any kind** against
global rows — they reference them.

**No personal data in the global store, ever.** Institutions, codes, places and rates are
global; a named person — a contact at an authority, anyone — is not, however often
tenants end up recording the same one. That is *earned* data: each tenant collected it in
its own dealings under its own privacy obligations, and it stays in that tenant.

Two write paths, and they are different:

- **The loader** — data files shipped with the module (versioned per release) applied on
  version bump. This is how standards data gets in: countries, subdivisions, geo nodes,
  postal codes, currencies, classification lists. Idempotent, additive, corrections ride the
  next data version.
- **Super-tenant editing** — for what has no upstream file or cannot wait for one: a new
  authority, a correction, a grouping every tenant should see. Audited, and the loader must
  never clobber it — the same never-overwrite rule `SeedAsync` has, now at the global
  layer.

Super-tenant writes go through the `ManageGlobalReferenceData` permission and
Default-shell-only controllers (`api/grove/global/lists`, geo import). The permission is
granted to no stereotype, and the controllers additionally require the Default shell, so an
ordinary tenant has no route to them, not a hidden one.

## Migrations: fresh-install repeatable, always

Pre-release means no compatibility shims and no lifting of old data — but it does **not**
mean disposable migrations. Every migration must produce the same end state
from a **fresh install** as from a dev tenant that has been re-migrated, every time:

- The global store's schema and its loader run from an empty database to the full standard
  layer with no manual step. A new host starts, and the data is there.
- Obsolete migration *steps* are deleted rather than accumulated, so the migration that
  ships is the direct declarative statement of the desired state, not a history of how we
  got there. `Create` describes the target; there is no `UpdateFrom3` telling a story.
- The loader is idempotent on re-run against a populated store and a no-op when the data
  version has not changed — so a fresh install and a hundredth restart converge on the same
  rows.
- The overlay tables are created empty by the tenant migration; nothing seeds them.

This is the existing convention (`SeedAsync` is written to be re-run) applied to the
global layer, and it is what makes "wipe the dev tenant and start again" a safe operation
rather than a risky one.

## What tenants keep

Tenants relabel, hide, re-sort and add to `global.*` lists. The resolution is the same
**standard + overlay** split as for the geo tree ([regions.md](regions.md)):

| Layer | Where | Who writes | Holds |
| --- | --- | --- | --- |
| Standard | global store | loader, super-tenant | the rows themselves: keys, machine values, default labels |
| Tenant overlay | tenant store | the tenant | per-key label override, hidden flag, sort order, **additions** (tenant-only rows) |

**This overlay is a hard requirement, not a convenience.** The reason a per-tenant copy was
attractive in the first place was that tenants could modify it; the global store is only
acceptable *because* an override document can be maintained against any global table. If
some global table ever cannot be overlaid, that is a defect in the store, not a reason to
copy the table into tenants.

A tenant's view of a list is the standard rows with its overlay applied plus its own
additions — which is exactly what a Content Part List *is*, so
`IContentPartListService` keeps its contract and sources the standard rows from the
global store with the tenant overlay (`Overrides` on `GroveContentPartListPart`,
`OptionOverride`) applied. Consumers reading option keys are unaffected.

Tenant additions can never collide with standard keys (namespaced), and a standard row
arriving later with the same meaning as a tenant addition is a super-tenant merge decision,
not an automatic one.

## What lives where

**In the global store** — identical for every tenant, owned by a standard:

- The `global.*` lists, declared in `Grove.ContentPartLists/Data/global-lists.json`:
  `country-codes`, `subdivisions`, `postal-formats`, `phone-country-codes`, `us-area-codes`,
  `languages`, `uom`, `uom-rec20`, `honorifics`, `name-suffixes`, `naics` (all levels,
  arranged by each option's `Parent`), plus `items.base-classification`
- The geo tree, postal-code records, `GeoBoundary` MultiPolygons, per-country addressing
  maps ([regions.md](regions.md))
- Currency metadata, ISO 4217 minor units ([money.md](money.md))
- Whatever reference data a downstream module declares through its own `GlobalDataMigration`
  (authority catalogues, classification lists), seeded from that module's data file on
  fresh install and always present on the host

**Per tenant** — tenant vocabulary, even where seeded with defaults:

- Module option sets: address, contact-point and legal-entity-name kinds, association types,
  tenant *additions* over global lists (an industry NAICS does not name), and downstream
  modules' own option sets
- Standard data whose licence forbids redistribution: each tenant imports its own. The only
  case where licence, not ownership, decides the store.
- Every overlay above
- Anything a tenant authored

The test: *would two tenants ever legitimately disagree about a row?* Countries, no.
Sales categories, yes. Then: *may we redistribute it?* If not, it stays per tenant however
standard it is.

**Features are per tenant; global data is not.** Orchard enables and disables features per
tenant, and that is the only per-tenant switch there is. The global store's data is loaded
once by the host and is there whether zero or a hundred tenants have the feature that reads
it turned on. Enabling a feature on a tenant turns on *code* — services, migrations for the
tenant's own overlay and content types, admin pages — and never copies global rows into the
tenant. Plugin code (a tax provider, a geocoder) is likewise one registration shared by all
tenants; only its enable state and credentials are tenant data.

## Ids

Global rows carry **stable string ids** that survive data versions — ISO codes where a
standard supplies them (`US`, `US-IL`, `JPY`), hierarchical composites where it does not
(`US-IL-031`). Tenant stores index these strings. Integer surrogate ids never leave the
global store.

## The store and its cache

- `GlobalStore`: one per host, registered by the `Grove.Data.Global` feature
  (`GlobalHost`), configuration section `Grove_Global`, shell-less
  `ReadAsync`/`WriteAsync`, migrations recorded in the store's own `DataMigrationRecord`,
  released on application stop. Two tenants reading one row set is exercised by every list
  read, not by a dedicated test.
- Host-level cache with version-keyed invalidation: `IGlobalCache`, one per
  host process; every `WriteAsync` bumps `GroveGlobalSchemaState.DataVersion`, the
  cache re-checks it at most every `CacheVersionCheckSeconds` (5) and drops every entry
  on change, so a write from another host lands within that window. Geo nodes (a
  country per entry), addressing maps, postal codes and global lists read through it, as
  does downstream modules' global data.

## Still to build

### Super-tenant editing UI

Editing UIs for the global store are generated for the functions a super-tenant needs and
are visible **only** in the Default tenant — an ordinary tenant has no route to them, not a
hidden one. The permission and the Default-only controllers exist (above); nothing edits global
rows from a page yet, only the API and import endpoints.

- [ ] **Build the first generated editing UI**, Default tenant only.

### Global data registry

- [ ] **The global data registry beyond the store**: one registry modules declare their global
  data sets in, so imports and loading are standardized.

### Open questions

- [ ] **Super-tenant UI scope.** Which global tables get a generated editor first — geo
  groupings and authority catalogues are the ones with no upstream file. Whether the editor
  is generic over any global list or per-table is a Grove UI decision.
- [ ] **Does anything need a real join?** None identified. Record here if one turns up rather
  than reaching for cross-schema SQL.
