# Regions and locations — Localization Profiles, the geo tree, addresses

`Crest.Regions` owns everything about **where** a party or a thing is: the Localization Profile
a party is assigned, the geographic tree every place resolves into, and how addresses are
entered per country. The `Address` part and the tenant's own Locations, which consume them,
are `Crest.Parties`' content types. What is not built yet is in
[regions.md](regions.md).

Nothing here assumes a UI language — that is [localization.md](localization.md)'s scope
entirely.

## Naming: "Localization Profile," never "Tenant"

OrchardCore's "Tenant" is a Shell — a hard isolation boundary with its own database,
content, users and permissions (`CrestTenant`, the Tenants admin page). A Localization
Profile is a content item living *inside* one Orchard Tenant. One Orchard Tenant can, and
typically will, have several profiles (US, MX, CA, EU) side by side. Use
`CrestLocalizationProfile` for the content type and keep "Tenant" language out of this
feature's code, permissions and content types.

The profile is a party's place, culture and currency, which is why it lives in Crest
rather than in a line-of-business module: every application has parties somewhere, and they
all have a country and a locale. What a *business* additionally reads off a profile is the
business's own — an accounting module attaches its currency tier facet, a tax module its
jurisdiction facet — and those attachments are made from the attaching module's migration,
never declared here.

## Localization Profiles

Content type `CrestLocalizationProfile`, following the same "Orchard content, not a parallel
JSON store" decision as [design-systems.md](design-systems.md)'s `CrestDesignSystem`.
A tenant publishes and can enable/disable each Localization Profile independently.

Built as `CrestLocalizationProfilePart`, `CrestLocalizationProfileReferencePart` (the
reference a party carries), `LocalizationProfileIndex`, permissions and
`ILocalizationProfileResolver` (party → context). It declares no facets of its own.

**A Localization Profile is a party-attached geo/localization profile.** It is assigned to a
party and bundles everything that varies with where that party is: default
country, language, measurement system, time zone, address standards, and whatever facets
downstream modules contribute. Any future field tied to a party's geography belongs here.

What the context itself declares:

- Default country (a level-1 node of the geo tree below)
- Measurement system, time zone defaults
- Address standards, postal code validation, phone validation — resolved through the
  per-country addressing map below, not stored inline
- Validation policies

What it does **not** declare, because downstream modules attach it:

- An accounting module's facets — default transaction currency, allowed currencies, accounting base
  currency, pricing strategy, exchange-rate policy.
- A tax facet.
- Warehouses and shipping providers — these are Locations (below) and a shipping module's
  concern respectively.

### The context is a composition point

`CrestLocalizationProfile` must not gain a field of any downstream module's kind, because that
would point this layer at a downstream module and the dependency only runs downstream.
Instead each module attaches its own part to the context in its own migration, declaring the
owner as a Manifest dependency ([architecture.md](architecture.md) › The rules that keep it
composable). Every geo-tied facet follows the same rule: **the context is a composition
point, and modules add to it — it never reaches out for them.** A tenant without a tax
feature simply has contexts with no tax facet.

Facets vary independently of each other and of the context's language: a German and an
Austrian customer fall under the same VAT regime while differing in language, so no facet
may be duplicated per language variant of a context.

## The geo tree

**One generic tree of ordered levels, labelled per country.** Every place any module
cares about — a country, a US state, a UK nation, a French département, a city, a postal
code, a transit-tax district, a sales territory — is a node in one tree. This is OFBiz's
`Geo`/`GeoAssoc` shape, which is how one `PostalAddress` serves every country there: nothing
in the *schema* is per-country, only the *data* is. France ships 16 régions → 110
départements; the UK ships its nations and 92 counties with no state level at all; both fit
the same table.

Where this departs from OFBiz, deliberately: OFBiz names its levels in US terms
(`stateProvinceGeoId`, `countyGeoId` — a département is filed as `COUNTY`). Crest uses
**numbered levels** and lets each country say what its levels are called.

### Levels

- **Level 1 is always the country.** Everything below is that country's own hierarchy.
- **No maximum depth.** How many levels a country has is that country's addressing map to
  dictate, not the schema's. The US may stop at 4; a country with boroughs or wards inside
  cities goes deeper, and nothing has to change to allow it.
- **A level is a record, not a column.** Nothing anywhere has `Level1..LevelN` fields. A
  node carries its own level number, its country, its **parents** and its code (ISO 3166-2,
  FIPS, INSEE — whatever that country's standard is); depth is a property of the data.
- **Many-to-many containment — of nodes, never of addresses.** A node may have several
  parents: Kansas City spans four counties; a postal area straddles a border; a French
  commune sits in two cantons. The tree is therefore a **directed acyclic graph**, not a
  strict tree. County stays a generic level (a US-shaped word, but just a level-3 node); the
  graph, not a special case, is what makes it accurate.
- **An address has exactly one geo stack.** A point is in one county and one city, however
  many counties the city spans. So an address resolves to **one node per level**, and
  resolution never "walks every parent path": it walks the address's own stack. Where a
  node on the stack has several parents, the address **must carry the disambiguating node
  explicitly** — entered from the map's list field, or set by geocoding — because the
  child alone cannot say which parent applies. A Kansas City address with no county node
  cannot be resolved at level 3, and that is an `Unavailable` for anything that needs it,
  not a guess.
- **No cycles, enforced.** Inserting or re-parenting a node is refused if the new parent is
  the node itself or any of its descendants — checked on every write to node membership,
  standard or overlay, and re-verified by the loader over a whole data version before
  commit. A cycle would make ancestor-walking non-terminating and tax stacking wrong.
- Levels **need not be equivalent across countries.** Level 2 is a state in the US, a
  nation in the UK, a région in France. The number is an ordering within one country, never
  a claim that "level 2" means the same thing anywhere else.
- **Postal codes are not a level.** They are a separate, fairly uniform record — (country,
  code, the node or *nodes* it maps to, a point). Uniform because every country has them in
  roughly the same shape; separate because they do not nest cleanly — a US ZIP can straddle
  counties, so it maps to a set, not a parent.
- **Groupings** sit outside the strict hierarchy: the EU, a sales territory, a
  special-purpose tax district that straddles counties. A node may belong to any number of
  groupings. OFBiz's `GROUP_MEMBER` association. Groupings are also how tax scopes wider
  than one node are expressed — there is no separate "tax region".

In code: `GeoNode` (level, country, parents, groups, code), `GeoPostalCode` and
`GeoBoundary` (MultiPolygon WKT) documents and indexes in the global store; the tenant overlay
(`GeoTenantNode`, `GeoNodeOverride`); the cycle check on tenant writes; and `GeoService`'s
merge, ancestors and `ResolveStackAsync`.

### The per-country addressing map

**Every country has an addressing map** that establishes the pattern for both the UI
and data validation. It says, for that country:

| The map declares | Example — US | Example — UK | Example — FR |
| --- | --- | --- | --- |
| which levels an address uses, and their labels | L1 Country, L2 State, L3 County, L4 City | L1 Country, L2 Nation, L3 County, L4 Post town | L1 Pays, L2 Région, L3 Département, L4 Commune |
| which of those appear on the form, and in what order | Line 1, Line 2, City, State, ZIP | Line 1, Line 2, Post town, County (optional), Postcode | Ligne 1, Ligne 2, Code postal, Commune |
| which are required | City, State, ZIP | Post town, Postcode | Code postal, Commune |
| whether a level is a fixed list or free text | State = list; City = text | County = list, optional; Post town = text | Département = list (derivable from code postal); Commune = text |
| the postal-code pattern and label | `\d{5}(-\d{4})?`, "ZIP" | UK postcode grammar, "Postcode" | `\d{5}`, "Code postal" |

The map is **global-store reference data keyed by country** ([global-store.md](global-store.md)),
loaded from data files and edited only by the Default tenant; a tenant may overlay labels.
`addressing-maps.json` ships maps for US, GB, FR and CA plus a generic map, and postal
patterns for 107 countries; `AddressingMap`/`AddressInput`/`AddressRules` live in
`Crest.Regions.Domain`, shared by client and server. `global.country-codes` and
`global.subdivisions` are levels 1 and 2 of the tree (`geo-nodes.json`: 249 countries, 69
subdivisions) and `global.postal-formats` one column of the map. Per the validation decision
below, both the WASM client and the server resolve the map at runtime from the same source.

**The country on the address selects the map**, not the party's Localization Profile. The
context supplies the *default* country for a new address; a German customer can still have
a UK delivery address, and that address is entered and validated as a UK address.

### Storage: the global store plus a tenant overlay

**The standard tree lives once, in the tenant-less global store** — see
[global-store.md](global-store.md) for the mechanism, who may write it (the Default tenant
only) and how tenant overrides work. Nothing is copied per tenant; a 60k-node tree exists
exactly once whatever the tenant count. Content items are the wrong primitive for it and are
not used.

**Tenant overlay — additive and override, in the tenant's store.** Tenants add nodes and
grouping memberships (a sales territory, a custom district, a municipality the data lacks)
parented onto standard nodes, and override presentation (label, hidden) of standard ones.
They never edit or delete a standard node. Standard-data version bumps never touch the
overlay; overlay nodes whose parent vanished are flagged, never dropped.

**Node ids are stable strings**, hierarchical where the source standard is (`US`, `US-IL`,
`US-IL-031`), so tenant indexes can hold them across data versions. An address's nodes go in
a many-row tenant index (`AddressGeoNodeIndex`: address id, node id, level), one row per
node — the `PartyPositionIndex` pattern — which is what "no level columns" means concretely
and what gives unbounded depth for free. Resolution queries the global store by id set; no
cross-store join is needed.

**Boundaries are their own document type, lazy-loaded.** Geometry is large and only
point-in-polygon needs it, so it never rides on the node. A `GeoBoundary` document in the
global store, keyed by node id, holds a **MultiPolygon** — required, not optional, because
geographies are non-contiguous: Hawaii, Michigan's two peninsulas, any archipelago, most
transit districts. WKT or GeoJSON; loaded per node when a point is tested; never
enumerated. Point-in-polygon is a **provider seam** like icons and tax: `IGeoBoundaryResolver` in Crest, with a built-in NetTopologySuite implementation first (BSD, in-process, no network) and external providers registrable later.

Geocoding (address → `Point`) is a provider seam of the same kind: `IGeocoder` and
`IGeoLocator`, composed by `CompositeGeoLocator`.

The same store holds downstream modules' place-keyed reference data, such as an authority
catalogue keyed to geo nodes, with the tenant's own records as its overlay.

### Populating the tree

The tree is a container; the data is a sourcing job of its own, and OFBiz is a warning —
it ships 63 US states but **2 counties and 1 postal code**.

`CensusGazetteerParser` streams a Census gazetteer file into level-3 nodes; the 2024 county
file ships embedded (3,222 counties, `US-{state}-{county FIPS}`), plus `geo-nodes-us.json`
for places and groupings (Chicago under Cook and DuPage; the RTA grouping over its six
counties). `POST api/crest/geo/import/gazetteer?level=` takes further files on the Default
tenant.

## Addresses

**One `Address` part for every country.** Not a part per country — tax resolution,
`PartyContactsService` and any index need uniform columns to query, and a part per country
turns "every address in this postal code" into a query across N types. Per-country
variation is handled entirely by the addressing map driving the form.

The part carries two kinds of data side by side, as OFBiz's `PostalAddress` does:

- **Text for the envelope** — `Line1`, `Line2`, `Locality` (city / post town / commune),
  `PostalCode`, attention/recipient names. Free text, exactly as written, in the order and
  with the labels the country's map says.
- **References for the machine** — the address's geo stack (`Geo`, a `GeoStackField`):
  node ids, **exactly one per level**, any depth; an optional postal-code record reference;
  and a `Point` (lat/long). Stored as the many-row `AddressGeoNodeIndex`, never as level
  columns. These are what tax, territories, localization and geocoding read. They are
  populated from the form's list-typed fields where the map makes a level a list, and
  back-filled by geocoding where it does not.
- **Boundaries** — extra node memberships beyond the hierarchy, for districts that do not
  follow it (transit, school, special-purpose tax districts). OFBiz's
  `PostalAddressBoundary`. Set by geocoding against boundary polygons or by import; never
  hand-typed.

`Crest.Parties` owns the `Address` part and its write surface (`PartyContactsService`);
it consumes the tree and the map, which are global reference data. The form and
`ResolveAddressAsync` are driven by the map.

The `GeoPostalCode` row for the address's code fills in any level the form left blank when
the code maps to exactly one node at that level (a straddling code fills nothing). A stored
point: supplied on the write model, else geocoded through `IGeoLocator`, else the postal
centroid; boundary memberships follow from the point. The US/UK/FR entry-and-validate test is
`Crest.Regions.Tests`; tree resolution is exercised by Playwright's US path.

**What consumes the machine half** — and why the split matters:

| Consumer | Reads |
| --- | --- |
| Tax (a downstream tax module) | the address's geo stack — one node per level — plus boundary memberships — one address reaches state, county, city and district authorities at once |
| Localization / Localization Profile defaults | the level-1 node |
| Sales territories, reporting | groupings |
| Shipping, distance, geocoded search | `Point` |

None of them care what a country calls a level or whether it uses it. A UK address has no
taxing level 3 and that is an empty reference, not a missing field.

## Locations

A Location is a place the **tenant** operates from — warehouse, store, office, receiving
dock. One Location is marked the tenant's **primary** — its "home", which a tax module reads
as the tenant's tax home. Built as the `Location` and `LocationPoint` content types,
`LocationIndex` and `ILocationService` in `Crest.Parties`.

### A Location can straddle a boundary; an address cannot

A point is in one geo stack. A **premises** is an area, and areas cross lines: State Line
BBQ in Kansas City sits on the Kansas–Missouri border and both states tax it. The one-stack
rule is not violated — it is the Location, not any address, that spans two stacks.

**What Locations hold is physical fact**, declared here because it is true regardless of
which features are enabled:

- **Several points.** A Location holds one or more `Point`s, each with its own geo stack
  and a label — a register, a dock, an entrance, a storey. A transaction that knows its
  point (this till, this receiving door) has a single stack and nothing to apportion.
- **Floor area and storeys**, per point where known, and for the premises as a whole. These
  are the facts any apportionment is computed *from*.

**What Locations do not hold is any apportionment.** How a straddling premises splits its
tax base between the stacks is a tax ruling, and a tax module attaches it to Location from
its own migration — Location never declares a tax field. A tenant without a tax feature has
Locations with points and floor areas and no apportionment, which is correct.

## Validation

Business-Context/document-driven validation (address, postal code, currency, precision,
measurement, business policy), never UI-language-driven.

**Decision: validation rules are pulled from Orchard at runtime, not compiled into a shared
static library.** Rule *definitions* — the addressing maps above (global store),
allowed-currency lists a downstream module attaches per Localization Profile (tenant store),
required-field sets per document type — are Orchard-managed data, never compiled in. Both the
Blazor WASM client and the Orchard Core server resolve the same definitions from that same
source at runtime, so there is one place a rule is edited and both sides pick up the change
without a redeploy. A shared compiled *engine* that interprets rules is still needed; the rule
*data* is Orchard content, matching this codebase's preference for Orchard-as-system-of-record
over parallel config.

**Boundary with user localization:** the *message text* shown on a validation failure is a
translation concern ([localization.md](localization.md)). The *rule that fired* is this
document's concern. Keep them independent.

**Client/server parity has a real limit.** Rules requiring a live third-party call (address
verification, geocoding, real-time tax lookups, live exchange rates) cannot execute inside a
WASM sandbox. The client does structural validation (well-formed postcode per the map,
currency in the allowed list) from the same rule source; authoritative checks round-trip to
the server. "Same validation everywhere" does not mean every check literally runs twice
with no network.

## Still to build

Built: see [docs/regions.md](regions.md) — Localization Profiles, the geo tree and its
overlay, addressing maps, the `Address` part over the geo stack, Locations, the first county
import, and the boundary and geocoding seams. This section is the checklist of what remains.

### Addressing maps as runtime rules

`global.country-codes`, `global.subdivisions` and `global.postal-formats` are levels 1 and
2 of the tree plus one column of the map, and are replaced by them.

- [ ] **Make maps runtime rules.** Maps as **Orchard-backed runtime rules** editable by the
  super-tenant — today they are data-file only.

  Candidate open dataset for the maps: Google's libaddressinput address metadata (it
  encodes exactly this — field order, required fields, postal pattern, subdivision lists,
  per country). **Licence unverified**; check before bundling, the same standard as any
  classification source.

### The shared validation engine

The validation engine is a genuinely shared piece of code referenced by both client and
server — worth fixing regardless, since `CrestTenant` today is hand-duplicated between
`Crest.Admin/wasm/Api/Api.cs` and
`Crest.Server/ViewModels/TenantsViewModels.cs` with nothing keeping the two
in sync. That duplication is the concrete proof this problem is real.

- [ ] **Fix the `CrestTenant` duplication.** The `CrestTenant` duplication fix. Not touched.

### Populating the tree

Candidate open sources, none verified: Census TIGER/FIPS (counties, places, ZCTAs,
boundaries; US Government work, no copyright), INSEE for France, ONS for the UK.
Postal-code → place data is the hard part and is commercial in many countries.

- [ ] **Load postal codes and boundaries.** Postal-code records and boundaries loaded from
      data — schema only, no rows ship.

### Geocoding

- [ ] **Add a geocoder.** Any geocoder implementation. `IGeocoder` / `IGeoLocator` and
      `CompositeGeoLocator` exist; no provider does.

### Decisions needed

- [ ] **Data sourcing and licence** for the tree below level 2 and for the addressing maps —
  see Populating the tree. Nothing is verified.
- [ ] **Geocoding provider.** Geocoding (address → `Point`) is a provider seam like boundary
  resolution; no built-in exists for it, so the first external provider is undecided.
