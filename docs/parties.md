# Parties — Person, Organization and the roles composed on them

`Crest.Parties` is the platform-wide party model: the `Person` and `Organization` records,
their contact points, addresses and org positions, the link from a person to a user
account, and the party-type registry that lets other modules compose roles on a party and
get its admin pages without Parties knowing them. Work not yet built is in
[parties.md](parties.md).

## Core party records

The base is two records, `Person` and `Organization`, and they are not interchangeable
(a person never becomes an organization). Every other party type is **composed on** one
of them: a separate role content item that points at its party through a `Party` picker
and carries the role's own data. A party can hold many roles at once — the same
organization is a customer, a vendor and a partner, each role with its own numbers,
terms and history. This is composition, never inheritance; the reasoning is in
[Why not inheritance?](#why-not-inheritance) below.

- A `Person` represents a human contact.
- An `Organization` represents a company, nonprofit, agency, household, or other legal/social organization.
- A person is not an Orchard user account.
- A person can optionally be associated with a portal/user account later.
- Creating a portal/user account for a person is explicit unless the user account is created first.
- A `Person` cannot become an `Organization`; the content type is fixed.
- Person / Organization parties (content types, runtime-extensible).

Parties owns the **party-type registry** (`IPartyTypeProvider`). A module registers a
descriptor for each role it composes and gets, without further code, its Parties menu
item, its `/Parties/{key}` page and its pane on All Parties. Parties never references
the modules that compose on it; they reference Parties.

## Why not inheritance?

Orchard content types are composition-first. They are best treated as named schemas made from parts and fields, not as CLR-style base classes.

Avoid creating separate inherited-style types such as:

```text
PersonCustomer
OrganizationCustomer
PersonVendor
OrganizationVendor
```

That duplicates business logic and makes querying, reporting, and cross-role relationships harder.

Also avoid attaching `CustomerPart` directly to both `Person` and `Organization` as the primary model. Orchard parts are attached at the content type definition level, while these roles need to be per-party and repeatable. A party may have no customer account, one customer account, or potentially multiple account records in the future.

## Vocabularies and pickers

- **Party vocabularies**: `parties.contact-point-kind`,
  `parties.address-kind`, `parties.organization-type`, `parties.industry` —
  module-owned Content Part Lists seeded DEFERRED (Parties migrates before Contents'
  index tables exist at first-time setup, so inline seeding aborts provisioning —
  every early module defers), none locked (the address-kind seed gains a DataLock
  when posting starts snapshotting by kind). Organization carries OrganizationType +
  Industry pickers. Seed keys in `PartiesOptionSets`/`ContactPointKinds`/`AddressKinds`.
- **Content Part List adoption.** `Crest.Parties` declares
  `Crest.ContentPartLists` in its manifest and its migration attaches pickers
  DEFERRED (global lists seed in a deferred task; an inline attach at
  first-time setup runs before the seeds and aborts provisioning): Person gets
  Honorific, Suffix and PreferredLanguage from Crest's global lists; the
  `ContactPoint` element gets PhoneCountry, and both bag elements get their Kind picker.

## Contact points and addresses

- **Contact points + addresses**: two named BagParts on Person and Organization,
  `ContactPoints` (element type `ContactPoint`: Kind picker, Value, Label, Preferred,
  PhoneCountry picker) and `Addresses` (element type `Address`: Kind picker, Line1/2,
  Locality, `Geo` — a `GeoStackField` from `Crest.Regions` whose level 1 is the
  country — PostalCode, Preferred). Person / Organization carry no scalar
  Email/Phone/Website fields. Nothing edits bag contents generically (Crest's editor round-trips them but
  is field-based), so Parties owns the write surface:
  `api/crest/parties/{partyId}/contacts|contact-points|addresses` (authorized with
  Orchard's View/EditContent on the party item; kinds and phone countries travel as
  option KEYS, translated through the scoped `PartyOptionKeys`; an address's country
  selects its addressing map, which validates it server-side; "preferred" is exclusive
  per kind). `PartyModel.Email/Phone` are DERIVED (`PartyContactRules.Primary`,
  pure Domain, unit-tested) so `PartyMapper` is a scoped service and its consumers
  await it. Live check: `party-contacts-api.js`.
- **Addresses are per-country.** Address shape varies per
  country — a US and a UK address in the same tenant, each in its own form — and the
  per-country definitions belong to the localization layer, not to Parties. Parties keeps
  owning the `Address` part and its write surface; it consumes the geo tree and the
  per-country addressing map: one `Address` part, generic numbered
  levels labelled per country, a map per country driving form and validation. Design in
  [regions.md](regions.md). Gates county-level tax resolution.

## Party ↔ user link

- **Party ↔ user link**: `Person.PortalUser` is a stock
  `UserPickerField`, indexed
  by `OrchardCore.ContentFields.Indexing.SQL.UserPicker` so person-for-user is a
  query. `IPartyUserLinkService` (Parties) owns the writes and the lookup;
  `Crest.Members.CreateMemberAsync` calls it when a `PersonId` is supplied
  (unknown person → error), so the link is recorded on both sides by the module
  that creates the account.

## Org structure

- **Org structure**: a `Positions` bag on Person (element type
  `OrgPosition`: Organization ContentPickerField restricted to Organization, Title,
  Department, Primary — one primary per person). Bag-contained fields are outside
  stock field indexes, so `PartyPositionIndex` (Parties-owned YesSql map index, one
  row per position) makes the organization side a query:
  `api/crest/parties/{personId}/positions` (CRUD) and
  `api/crest/parties/{orgId}/people`. Live check: `party-positions-api.js`.

## Party roles as data

- **Party roles as data**: Customer, Vendor, Lead, Contractor, Guest, Employee... —
  a party can hold many. Distinct from Orchard SECURITY roles (Admin, Supervisor...);
  the outline's UserTypes list mixes the two and this module is where the split lives.
  The model is composition, never inheritance (above): Person and
  Organization are the base party records, and every other party type is a separate
  role content item pointing at one of them through its `Party` picker (the shape
  `CreatePartyRoleDefinitionsAsync` stamps). The roles themselves are declared by the
  modules that compose them.

## Party-type registry and All Parties

- **Party-type registry + All Parties UI**: Parties owns
  `IPartyTypeProvider` / `PartyTypeDescriptor` (Domain) and `PartyTypeCatalog`; the
  Parties admin menu, the route gates and `GET api/crest/parties/types` are all
  generated from it, so a module that composes a party type registers a descriptor
  and gets its menu item, its `/Parties/{key}` page and its All Parties pane at once.
  Parties registers Contacts (Person) and Organizations; other modules register the
  role types they compose. A module may register a **global kind** — a catalogue that is
  global and read-only, with the tenant's own standing kept in a tenant-side record — and
  fill the pane through Crest's page-region seam ([page-regions.md](page-regions.md); `PartiesPageRegions.Pane(key)`;
  `Detail(key)` opens a selected record's summary to downstream blocks). All Parties
  (`/Parties`, the Parties menu's "All Parties" child — the primary nav never links a
  parent that has children) lists the types down the left and the selected type's pane
  (the list/detail/inline-editor shape, generalised) in the main section.
  Hide/show is the admin menu's own mechanism: the tenant's layout overlay hides a
  type for everyone; the page's pane settings hide one for the signed-in user only,
  via the per-user layer Crest added for it (`api/crest/navigation/me/hidden`, keyed by
  the served menu item key, so the sidebar link goes with the pane — see
  [admin-menu.md](admin-menu.md) › Two hide layers). Parties registers no role of its own, so
  All Parties is exercised by the checks of whichever module registers one.

  **Rulings:**
  - The per-user menu layer is an **overlay, never a copy**: it stores only the user's
    own hidden item keys, is applied last on the served tree, and everything else
    (provider changes, tenant renames/moves/icons/hides) flows through untouched. A
    user cannot unhide what the tenant hid — the tenant layer wins by construction.
    Any future per-user move/rename would be the same shape (per-key deltas).
  - The Parties root menu item keeps its URL but the primary nav never links a parent
    with children, so an "All Parties" first child carries the link. No linked-parent
    nav mode is planned.
  - The party-type registry is the only way a type reaches the Parties UI: no page or
    menu entry is hand-written per type. A role type arrives when the module that
    composes it registers it. Parties gains nothing per type.
  - A type's dedicated page belongs to its kind: tenant kinds use Parties'
    `/Parties/{key}`; a global kind's descriptor names its owner's page and its All
    Parties pane is the owner's contribution, so Parties never references the owning
    module.

## Still to build

The party model as built is described in [docs/parties.md](parties.md). This plan
holds what is not built yet.

### Users and persons

- [ ] **Link new user accounts to a `Person`.** Creating a new Orchard user account should
  eventually auto-create or link a `Person` profile, but that does not mean every `Person`
  can log in.

### Organizations

- [ ] **Legal entity names** (ruling 2026-10-05). The display name (the TitlePart; for the
  tenant, `ISite.SiteName`) stays the default name everywhere a name is shown. An
  Organization additionally carries its legal entity names as **a list of kinds**, the way
  contact points are: an `OrganizationNames` bag whose entries carry a Kind from a
  tenant-editable option set (seed: legal, DBA / trade name, former, other) and the name,
  so an organization can hold several DBAs. **One primary entry per kind**, exclusive like
  contact points' Preferred. **No effective dates**: a superseded name becomes the
  `former` kind. None of these is a second display name ([branding.md](branding.md)).
- [ ] **Parent organization** (ruling 2026-10-05): a picker on Organization restricted to
  Organization. Its type rule and cycle refusal are a handler registered on the picker
  ([content-items.md](content-items.md) › Picker relationship handlers).
- [ ] **The tenant's own organization** (ruling 2026-10-05). It exists **before the first
  user** — created during tenant setup — because staff belong to it (members.md › Class
  becomes a set). A site-settings reference to an
  Organization party representing the tenant's legal entity: its legal names, tax id and
  addresses come from there; the display name stays `ISite.SiteName`. It is a **special
  organization that has no members** — no member binding may point at it — so staff
  (tenant-users) and members (org-users) never share an organization and their logins stay
  separate ([members.md](members.md)). Whether members may be bound to it is a **tenant
  setting, default: blocked** (ruling 2026-10-06), enforced by Members through a Parties
  check, "is this the tenant's organization", read from the site-settings reference — never
  a flag on the item.

### Tenant-defined party roles (deferred, far down the pipeline)

- [ ] **A tenant creates a party role without code.** Possible in principle: a party role
  is a content type with a `Party` picker (the shape `CreatePartyRoleDefinitionsAsync`
  stamps), so only the registry needs code — a built-in provider could register every
  content type the tenant marks as a party role. **Kept out for now (2026-10-05)** for
  simplicity: a role ties deeply into its UI (menu, `/Parties/{key}` page, panes, editors),
  and a tenant-made role has no module to supply that UI. **Must be figured out before the
  Blazor UI designer is done** ([blazordesigner.md](blazordesigner.md)), because the designer
  will hook into the custom data types system to build UIs for tenant-made types, and
  tenant-made party roles are one of those types. Until then party roles come only from
  modules.

### Contacts and addresses

- [ ] **One bag-element service** (ruling 2026-10-06). OrchardCore stores bags (`BagPart`,
  a list of contained items) and edits them only through its MVC editor; it has no API for
  adding, changing or removing elements. Crest's contacts service is that API plus Crest's
  rules (primary per kind, option keys, per-country address validation). It is generalized
  into one service, parameterized by bag, element type and kind list, used by contact
  points, addresses and legal entity names — never a second bag service.
- [ ] **One map index per bag element type** (ruling 2026-10-06). Stock indexes do not look
  inside bags, so every queryable bag facet gets its own Parties-owned index, one row per
  element with Published and Latest — the `PartyPositionIndex` pattern: a contact-point index
  (kind, type, preferred), an organization-name index (kind, primary).

- [ ] **Contact, address and position writes publish** (ruling 2026-10-06). They are live
  data: the write and the publish happen together, rather than saving a draft that never
  reaches the published version. `AddressGeoNodeIndex` gains Published and Latest columns,
  like the other Parties indexes, so consumers such as tax read published addresses.

- [ ] **One kind list, with a hidden type per kind** (ruling 2026-10-06). The user picks one
  contact kind from one tenant-editable list (`parties.contact-point-kind`); landline,
  mobile, VOIP, WhatsApp and LinkedIn are kinds, never separate fields or lookups. Each kind
  carries its **type** in the option's existing `Category` — phone, e-mail, web, social,
  messaging, other — which the user never selects. Validation runs per kind, defaulting to
  its type's rule (number, address, URL, handle); reports and the person's preferred
  contact methods can group by type, so a tenant-added kind rolls up once it has a type.
  The seed gives every kind a type; a tenant adding a kind chooses one, and the data lock
  freezes it once code relies on it.

- [ ] **Preferred contact methods are per Person only** (ruling 2026-10-05): an ordered
  party-level preference across channels (text, call, e-mail) on Person. `Preferred` on a
  contact point keeps picking one entry *within* a kind. Organizations have contact
  information (a main line, a general inbox) but no preferred method: an organization is
  reached through its people.
- **Addresses on non-party records are case by case** (ruling 2026-10-05). Every record
  hangs off a Person or an Organization; whether a record's editor edits addresses, and
  through which API, is decided per editor, and access follows ownership or access grants
  to organizations and party type. The contacts API is not opened generically to any type
  carrying an `Addresses` bag.

### Industry classification

- [ ] **Reseed `parties.industry` from NAICS.** It ships today as a
  hand-rolled 16-entry starter set (agriculture, construction, education…), which was
  fine as a placeholder but is not a classification anyone can map onto. NAICS is the
  Census Bureau's industry counterpart to NAPCS — the same open family, public domain
  under 17 U.S.C. § 105, so it can be bundled outright with no licence, membership or
  permission. That makes the party-industry dropdown a real standard while costing
  nothing.

  **Tenants still add their own.** This is a seed, not a lock: `SeedAsync` never
  overwrites an existing option, so tenant additions, relabels and hides survive every
  reseed. A vertical that needs an industry NAICS does not name just adds it. Standards
  start the vocabulary rather than bounding it.

  **Full NAICS, as a global list (ruling 2026-10-06).** All levels, 2 to 6 digits, held once
  in the global store rather than per tenant, so the 1,000+ codes are not copied into every
  tenant's list document. NAICS is a hierarchy (sector → subsector → industry group →
  industry → national industry), so this needs Content Part Lists' Category to handle
  hierarchy ([content-items.md](content-items.md) › Still to build).
- [ ] **Organization type is the legal form only (ruling 2026-10-06).** Drop "DBA" from the
  `parties.organization-type` seed (Incorporation, LLC, Community Group, NGO, Government):
  trade names are `dba` entries in the organization's legal entity names.

### Decisions needed

- [ ] **NAICS outside North America.** NAICS is North-America-only, which matters if party
  industry is ever reported on internationally.
- [ ] **Party merge/dedupe mechanics** (survivorship rules, live-reference rewriting UI,
  audit trail) — Parties module concern, design later. The enabling rule is already
  decided: live references by id + posted-document snapshots. What a posted document
  copies is decided by the posting workflow of the module that owns the document, not by
  Parties.
