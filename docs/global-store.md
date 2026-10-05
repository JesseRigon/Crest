# The global store — tenant-less reference data

Reference data that is the same for every tenant is stored **once**, in a YesSql store that
belongs to no tenant (`Crest.Global`). The Default tenant is its only editor. Tenants read it
and may layer their own additions and presentation over it; they never modify it. What is not
built yet is in [global-store.md](global-store.md).

## Why

Every `global.*` list used to be seeded **per tenant** from compiled C# arrays
(`GlobalReferenceData.cs`, `GlobalContentPartListsMigrations.cs`, both since deleted) into
Content Part Lists — content items in each tenant's own store. That was fine at hundreds of rows and
ten lists. It fails on what comes next: the geo tree is ~60k nodes for the US alone before
boundaries; postal codes ~40k; authority catalogues; classification lists. Copying that into
every tenant is waste, and correcting it means N migrations instead of one.

It also got the ownership wrong. A tenant could *edit* the country list. Nothing
about "Illinois" or "ISO 4217 JPY = 0 decimals" is a tenant's to edit; only labels,
ordering, visibility and additions are.

## The mechanism — Orchard's own precedent

Orchard can store tenant settings in a database rather than `tenants.json`
(`DatabaseShellsSettingsSources`). That data belongs to no tenant, so Orchard opens a YesSql
session against a **store of its own** — its own `DatabaseProvider`, `ConnectionString`,
`TablePrefix` and `Schema`, configured under `OrchardCore_Shells_Database`.

Crest does the same: a **`CrestGlobalStore`** — one host-level service that owns a
tenant-less YesSql store configured under its own section (`Crest_Global`,
same four fields). Full YesSql: documents, map indexes, SQLite, Postgres, SQL Server or
MySQL. Shell services resolve host services, so tenant code queries it like any other
service.

Rather than building a synthetic `ShellSettings` and asking `IShellContextFactory` for a
context, as Orchard's `internal` helper does, the store builds its YesSql `Configuration`
directly from those options, the way OrchardCore.Data builds a tenant's store, so no shell is
involved. Modules contribute their global documents as `ICrestGlobalSchema` types, discovered
by scanning the loaded module assemblies, so the host does not name each one.

**Topology is configuration, as it is for Orchard.** Orchard places tenants in one database
by table prefix, or in separate databases by connection string, per configuration. The global
store is the same: with no section configured it is a SQLite file (`crest-global.db`) in a
`CrestGlobal` folder beside the tenant folders, with the table prefix `CrestGlobal`; an
operator who wants it elsewhere (Postgres, SQL Server, MySQL) sets the provider and connection
string, exactly as they would for `OrchardCore_Shells_Database`.

**No cross-store joins**, and none needed. Tenant indexes hold global ids (stable strings,
see below); lookups query the global store with an `IN`. Two indexed queries, not one join.

### The host registers it

`ICrestGlobalStore` is registered by an `OrchardCoreBuilder` extension
(`AddCrestGlobalStore()`), not by the feature, because the store is one database shared by
every tenant and so belongs to the host's service collection: a host calls
`AddOrchardCms().AddCrestGlobalStore()` in `Program.cs`. The `Crest.Global` feature itself
only adds the `ManageCrestGlobalReferenceData` permission.

## Who can write

**Only the Default tenant** (`ShellSettings.IsDefaultShell()`, which Crest's
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

Super-tenant writes go through the `ManageCrestGlobalReferenceData` permission and
Default-shell-only controllers (`api/crest/global/lists`, geo import). The permission is
granted to no stereotype, and the controllers additionally require the Default shell, so an
ordinary tenant has no route to them, not a hidden one.

## Migrations: fresh-install repeatable, always

Pre-release means no compatibility shims and no lifting of old data — but it does **not**
mean disposable migrations. Every migration must produce the same end state
from a **fresh install** as from a dev tenant that has been re-migrated, every time:

- The global store's schema and its loader run from an empty database to the full standard
  layer with no manual step. A new host starts, and the data is there.
- Superseded migration *steps* are deleted rather than accumulated, so the migration that
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
`ICrestContentPartListService` keeps its contract and sources the standard rows from the
global store with the tenant overlay (`Overrides` on `CrestContentPartListPart`,
`CrestOptionOverride`) applied. Consumers reading option keys are unaffected.

Tenant additions can never collide with standard keys (namespaced), and a standard row
arriving later with the same meaning as a tenant addition is a super-tenant merge decision,
not an automatic one.

## What lives where

**In the global store** — identical for every tenant, owned by a standard:

- The `global.*` lists, declared in `Crest.ContentPartLists/Data/global-lists.json`:
  `country-codes`, `subdivisions`, `postal-formats`, `phone-country-codes`, `us-area-codes`,
  `languages`, `uom`, `uom-rec20`, `honorifics`, `name-suffixes`, plus
  `items.base-classification`
- The geo tree, postal-code records, `GeoBoundary` MultiPolygons, per-country addressing
  maps ([regions.md](regions.md))
- Currency metadata, ISO 4217 minor units ([money.md](money.md))
- Whatever reference data a downstream module declares through its own `ICrestGlobalSchema`
  (authority catalogues, classification lists), seeded from that module's data file on
  fresh install and always present on the host

**Per tenant** — tenant vocabulary, even where seeded with defaults:

- Module option sets: `parties.industry` *additions*, address and contact-point kinds, and
  downstream modules' own option sets
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

- `CrestGlobalStore`: host-level service (`Crest.Global`, `AddCrestGlobalStore()` in
  `Program.cs`), configuration section `Crest_Global`, shell-less
  `ReadAsync`/`WriteAsync`, versioned `CrestGlobalSchemaState` per `ICrestGlobalSchema`,
  hosted-service lifetime. Two tenants reading one row set is exercised by every list
  read, not by a dedicated test.
- Host-level cache with version-keyed invalidation: `ICrestGlobalCache`, one per
  host process; every `WriteAsync` bumps `CrestGlobalSchemaState.DataVersion`, the
  cache re-checks it at most every `CacheVersionCheckSeconds` (5) and drops every entry
  on change, so a write from another host lands within that window. Geo nodes (a
  country per entry), addressing maps, postal codes and global lists read through it, as
  does downstream modules' global data.

## Still to build

Built: see [docs/global-store.md](global-store.md) — the tenant-less store, the
overlay, the loaders, the super-tenant permission and API, and the version-keyed cache.
This section is the checklist of what remains.

### Super-tenant editing UI

Editing UIs for the global store are generated for the functions a super-tenant needs and
are visible **only** in the Default tenant — an ordinary tenant has no route to them, not a
hidden one.

- [x] **Permission and Default-only controllers.** `ManageCrestGlobalReferenceData`
      permission; Default-shell-only controllers (`api/crest/global/lists`, geo import).
      Documented above.
- [ ] **Build the first generated editing UI.** The first generated editing UI, Default
      tenant only. Nothing edits global rows from a page yet; only the API and import
      endpoints exist.

### Known rough edge: the global store needs a host call

- [ ] **Make the global store need no host call.** `Crest.Global`'s `ICrestGlobalStore` is
  registered by an `OrchardCoreBuilder` extension (`AddCrestGlobalStore()`), not by the
  feature, because the store is one database shared by every tenant and so belongs to the
  host's service collection. A host that enables `Crest.Global`, `Crest.Regions` or
  `Crest.Money` without making that call gets no warning: the features enable, and setup
  then fails with "Unable to resolve service for type ICrestGlobalStore". Enabling a
  feature should not depend on an invisible second step — either the features should fail
  enablement with a clear message, or the registration should happen where the feature is.
  Recorded rather than fixed; it bit the standalone host first and will bite the next one.

### Decisions needed

- [ ] **Super-tenant UI scope.** Which global tables get a generated editor first — geo
  groupings and authority catalogues are the ones with no upstream file. Whether the editor
  is generic over any global list or per-table is a Crest UI decision.
- [ ] **Does anything need a real join?** None identified. Record here if one turns up rather
  than reaching for cross-schema SQL.
