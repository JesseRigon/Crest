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
  by `Crest.ContentFields.Indexing.SQL.UserPicker` so person-for-user is a
  query. `IPartyUserLinkService` (Parties) owns the writes and the lookup;
  `Crest.Members.CreateMemberAsync` calls it when a `PersonId` is supplied
  (unknown person → error), so the link is recorded on both sides by the module
  that creates the account. Who a member is, and the bindings and hierarchy around the
  link, are under [Members](#members-persons-who-act-for-an-organization) below.

## Org structure

- **Org structure**: a `Positions` bag on Person (element type
  `OrgPosition`: Organization ContentPickerField restricted to Organization, Title,
  Department, Primary — one primary per person). Bag-contained fields are outside
  stock field indexes, so `PartyPositionIndex` (Parties-owned YesSql map index, one
  row per position) makes the organization side a query:
  `api/crest/parties/{personId}/positions` (CRUD) and
  `api/crest/parties/{orgId}/people`. Live check: `party-positions-api.js`.

## Members: persons who act for an organization

A **member** is a person who acts for an organization within the tenant (a customer's or a
vendor's person) through a user account; **staff** are the tenant's own people. Both are
ordinary platform users in the tenant's user store, with the same user management, the same
roles and permissions machinery and the same hierarchy; what differs is the organization
they act for and the shell they sign in to. The party model carries the relationship, the
user record carries authentication and roles. `Crest.Members` owns it, on `Crest.Parties`
(a member is a Person with a portal user; a binding points at an Organization) and on the
platform's user, role and permission pipelines; nothing in it is specific to a line of
business. The sign-in side (the member portal, sessions, sides) is
[shells-and-themes.md › The member portal](shells-and-themes.md#the-member-portal-login-sessions-sides);
the access side (the class, the ceiling, the caller) is
[workflows.md › Operations › The member caller](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery).

**Members and subscriptions are two systems.** Members is who may act for an organization:
the bindings, their roles, the hierarchy, the member admin. Subscriptions, seats,
entitlements, tiers and perks are a downstream module's, built on it; Crest has no group
concept (Decisions needed › Grouping). Holding a seat says nothing about member access and
member access implies no seat; what they share is the resolution context, the member's
active organization. Members raises a binding's lifecycle through `IMemberLifecycleHandler`
(`MemberBoundAsync` / `MemberUnboundAsync`, resolved in a fresh scope after the binding
write commits) and knows nothing more about its consumers.

**The link.** `Person.PortalUser` (above) is the person ↔ account link, written through
`IPartyUserLinkService.LinkPortalUserAsync` by `Crest.Members.CreateMemberAsync` whenever a
`PersonId` is supplied, and the person's org position links to the Organization.

**Bindings.** One account can hold several **organization bindings**, with different roles
per organization: the binding record (`CrestMemberInfo` aspect, `MemberOrgBindingIndex`,
one row per binding) says which role templates the member holds in which organization.
Roles are per user in the platform and `RolesDocument` is one cached per-tenant blob, so
per-organization roles are not thousands of role rows: the tenant defines a small set of
ordinary member role templates (Member, MemberAdmin, …) in the normal role editor, the
tenant governs powers, and the binding names the template. The member's effective
permissions come from (user, active binding); the hierarchy places the member once per
binding.

**Member admin.** Each organization has a member admin, **defaulting to the first member
added** for that organization (`MemberService`). Their scope: their organization's members
(hierarchy and sub-users), member data and credentials, and the organization settings
future modules expose to them (autopayment scheduling, payment types, wallet connections).
Not theirs: tenant features, tenant settings, users and data outside their organization.
Enforcement is two-layered: the hierarchy's root guard bounds the **who**, ordinary
permissions bound the **what**; the member admin holds a role the tenant shaped, never a
parallel authority. Organizations choose people, tenants choose powers.

**The tenant's own organization** is special: it exists before the first user, is
referenced from site settings, and has no members by default (Still to build ›
Organizations).

### Hierarchies

**The hierarchy is relational, not a tree of slots** (ruling 2026-10-05): a set of
manager → report relationships between users, not an entity users are placed into. A user
can have two managers, a manager many reports, and no two users' patterns have to match.
It answers ownership and visibility, who sees whose records, and nothing else; permissions
stay roles.

**Relationships are typed.** *Reports-to* is the hierarchy: it grants a manager visibility
of their reports' records and stays within one root (staff, or one organization). Other
types are **associations**, and they may cross between staff and an organization: a large
customer organization can have several sales reps assigned, each connected to different
contacts there; each rep sees only what their own connections give them, and the
organization's people do not see the reps' side. An association grants no visibility by
default. "Teams" are this hierarchy; there is no teams table and no group concept.

**One shared store, one root for staff and one per organization.** The organization-A
versus organization-B boundary inside a members-only table is as sensitive as the
staff-versus-member boundary, so the mandatory root-scoped guard has to exist anyway; a
second table would duplicate schema and queries without adding protection. Every query
and write carries the root or organization id, so the scope resolver stays class-blind
(it only asks subtree(user)) and one tree-management path serves staff admins and member
admins alike. What the store answers: **subtree(user)** ("me and everyone under me", the
expansion in front of the fail-closed scope machinery), **chain(user)** (the path to the
root, for escalation and approval routing) and a node's membership (user, parent, the
organization or the staff root).

As built: `CrestUserHierarchy`, a plain table (adjacency plus a materialized path, created
by a data migration) behind the root-guarded `UserHierarchyService` over
`IDbConnectionAccessor` with dialect quoting; multi-organization members get one node per
binding; hierarchy writes run as deferred tasks after commit (the raw transaction deadlocked
SQLite against the ambient session). It is superseded by the closure table under Still to
build › Relational hierarchy, and the subtree expansion is not wired into the scope
machinery yet.

### What `Crest.Members` contains on the party side

- Bindings: the `CrestMemberInfo` aspect, `MemberOrgBindingIndex`, `MemberService`
  (first member of an organization becomes its member admin), the binding endpoints
  (`api/crest/members`: me, active-org, list-by-org, create, bindings add/remove).
- Provisioning: member creation and `MemberStampService` (shared by `CreateMemberAsync`
  and the portal's registration path: class, binding, deferred hierarchy node); member
  role templates ensured by a deferred, idempotent migration.
- The hierarchy store above.
- Ordering rule the build found: inline vocabulary seeding in Parties' `CreateAsync`
  aborted first-time provisioning (index tables not yet created), so it is deferred, like
  hierarchy writes.

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
  user** — created during tenant setup — because staff belong to it (› Members and
  organizations › Class becomes a set). A site-settings reference to an
  Organization party representing the tenant's legal entity: its legal names, tax id and
  addresses come from there; the display name stays `ISite.SiteName`. It is a **special
  organization that has no members** — no member binding may point at it — so staff
  (tenant-users) and members (org-users) never share an organization and their logins stay
  separate. Whether members may be bound to it is a **tenant
  setting, default: blocked** (ruling 2026-10-06), enforced by Members through a Parties
  check, "is this the tenant's organization", read from the site-settings reference — never
  a flag on the item.

### Members and organizations

- [ ] **Class becomes a set, one index of connections** (ruling 2026-10-06). A user's
  connections are rows keyed by the composite **`(UserId, OrgId, Class)`**, unique, no
  nulls: staff belong to the tenant's own organization, so staff is `(user, tenant org,
  staff)`, and acting for an organization as a member is `(user, org, member)`. Membership
  derives from the bindings (the record of truth); the index is a rebuildable projection of
  the user record and answers "is staff", "may act for org X", "members of org X" and the
  switcher's organization list. **No user exists outside an organization**: the tenant and
  its organizations exist before any user, so a member with no organization is a failure
  mode and registration places the member in an organization or refuses. Being both staff
  and a member of the tenant's own organization needs the tenant setting (default blocked).
  Conversion is adding or removing a class through one permission-gated action that
  replaces today's `convert-to-staff` endpoint, bumping the security stamp either way;
  adding staff keeps the bindings, removing member ends them (ruling 2026-10-05, both
  directions).
- [ ] **One home per fact: the binding record, not a second declaration.** The record is
  the seam of record; the flattened projection on the user object (what the caller builder
  reads) stays but is written only by the record's write path and is rebuildable from the
  records. `AddBindingAsync`/`RemoveBindingAsync` stop being public API and become internal
  projection writers; a rebuild command ships with the work, as drift repair and as the
  migration that populates existing data.
- [ ] **Layering: identity in the user layer, structure in content** (ruling 2026-09-16,
  option C of three). Identity and the authorization hot path (class, active organization,
  effective roles) live in the user layer; the declarative organization structure
  (organization settings; groups were part of this until 2026-10-05 and are dropped) lives
  in content, editable, versioned, permissioned and importable, which is what makes
  thousands of users tractable; the hot path reads a flat projection rebuildable from it.
  Rejected: everything in the user layer (fast, but structure is not content, so every
  admin surface is hand-built); everything in content (the content store on every request,
  or a cache that is the projection again).
- [ ] **The tenant's own organization has no members by default** (rulings 2026-10-05,
  2026-10-06): a tenant setting, default **block**, decides whether bindings may point at
  the Organization that represents the tenant (› Organizations above), so by default staff
  and member logins never share an organization. **One source of truth**: Parties exposes
  "is this the tenant's organization", read from the site-settings reference, and every
  binding write (the binding API, the member stamp path, the portal register endpoint) goes
  through one Members service that checks it with the tenant setting. No flag is stored on
  the organization.
- [ ] **Person creation for external-login auto-registered members** (today they get the
  class and the binding only).
- [ ] **Admin list class filter.** `IUsersAdminListFilterProvider` adds a named search term
  (`class:member`), copying `RolesAdminListFilterProvider` (`.AlwaysRun()` for forced
  scoping, useful later for portal-side lists); the dropdown is a display driver on
  `UserIndexOptions`. The stock `UsersFilter` enum is closed, so the named term is the
  route, not an enum fork.
- [ ] **User editor class section**, read-only, via `SectionDisplayDriver<User, T>`;
  conversion is not edited there, it is its own action.

### Relational hierarchy

- [ ] **Replace the single-parent tree with manager → report relationships** (rulings
  2026-10-05, 2026-10-06), built **on YesSql as a closure table**, no hand-written SQL:
  each person in a hierarchy has a small document per organization holding their direct
  managers and their computed ancestor list; a map index emits one row per ancestor,
  `(AncestorId, DescendantId, OrgId)`, which is the closure table, created and queried like
  every other index. "Everyone under me" and "my chain up" (every path, for escalation and
  approval routing) are index queries; adding an edge is refused when the report is already
  above the manager; changing an edge recomputes the ancestor lists of the report and
  everyone below, saved in one session; edge writes are serialized per organization so two
  writes cannot together create a cycle; a rebuild from the edges doubles as repair. The
  root guard holds: every query and write carries the organization. Replaces
  `UserHierarchyService`'s plain-SQL tree and its path math.
- [ ] **Association relationship types** (ruling 2026-10-05). Typed connections beside
  reports-to (sales rep → contact, account rep → organization, …) that may cross staff and
  an organization; they grant no visibility by default, and what a type grants is decided
  per type. Modules and tenants both declare types: module types are built in and not
  editable, tenant types are an editable list on top. **Associations are content items**
  (ruling 2026-10-06), kept apart from the hierarchy so the root guard and the closure table
  never see them: an Association type with two ends (each a person and the organization
  they act in) and a type picker; editing, versioning, permissions and audit come from the
  content system; a map index on the two ends and the type answers "my connections"; a
  type may register a visibility resolver (default: nothing), and the reading API enforces
  that a rep sees only their own connections and the organization's people do not see the
  rep side unless the type allows it.
- [ ] **Hierarchy-aware scope expansion** feeding the fail-closed scope providers
  ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery) step 1): subtree(user) as an input to the content
  and assignment scopes. Deliberately not wired yet; the organization-scope axis on data
  (the owner part and index) landed 2026-10-10, the hierarchy expansion joins on it.

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

- [ ] **Grouping, not final; a starting point for next time** (2026-10-05). For now Crest
  has **no group concept**: roles stay as they are. Where the discussion landed: **groups
  grant access (what you can see) and roles grant actions (create, update, delete and the
  other verbs)**, every action requiring that you can see the record first. Open with it:
  capability screens (settings, audit) as actions; answering the platform's view
  permissions from the access system; business roles as derived groups. Until then,
  sharing uses the **no-permission role shim** ([media.md](media.md) › Terms).
- [x] **Conversion semantics.** Ruled 2026-10-05: both directions, as adding or removing a
  class (› Members and organizations).
- [ ] **NAICS outside North America.** NAICS is North-America-only, which matters if party
  industry is ever reported on internationally.
- [ ] **Party merge/dedupe mechanics** (survivorship rules, live-reference rewriting UI,
  audit trail) — Parties module concern, design later. The enabling rule is already
  decided: live references by id + posted-document snapshots. What a posted document
  copies is decided by the posting workflow of the module that owns the document, not by
  Parties.
- [ ] **Code switches on contact kind keys (P2).** Ruled: one kind list with a hidden type
  per kind (phone, e-mail, web, social, messaging, other). Today `PartyMapper` and the
  primary-contact rules hard-code the e-mail and phone/mobile kind keys; "social" and
  "messaging" are kinds now but become types; values are only checked for being non-empty;
  a kind without a type becomes "Uncategorized" and would not roll up; the data lock is
  enforced in the controller, not the service or the seed. Options: (1) code reads a
  contact's type, never its kind key; every seeded kind gets a type with a data lock; values
  are validated per type; a kind without a type is refused; the lock is enforced in the
  service; (2) keep kind keys in code, types only for reports. Recommendation: 1.
- [ ] **Preferred contact methods versus types (P3).** Ruled: preferred contact methods
  are an ordered list on a Person (text, then e-mail). But "text" and "call" are both type
  "phone", so the preference is about a channel, which the type list cannot express; and
  with finer kinds (landline, mobile) "preferred within a kind" changes meaning. Options:
  (1) a small locked list of channels (call, text, e-mail, messaging, …) and an ordered
  picker on Person, each channel saying which contact types can serve it; (2) order the
  person's own contact points directly. Recommendation: 1; the preference survives a
  changed number, and "text" can pick the person's preferred mobile.
- [ ] **The tenant's own organization (P6).** Ruled: it exists before the first user
  (created at tenant setup), is referenced from site settings, and members on it are a
  tenant setting (default blocked). Open: positions, role pickers and the Organizations
  list would show it like any customer; its addresses overlap the tenant's Locations;
  `TaxIdentifier` is one value but a business can have several tax ids; its title is a
  second name beside `ISite.SiteName` that can drift. Options: (1) show it marked "your
  organization" where relevant and exclude it from customer-facing pickers; Locations are
  premises, the organization's addresses are legal and mailing; tax ids become a typed
  list (like legal names); the title follows `ISite.SiteName` (editing one updates the
  other); (2) hide it from all lists, keep one tax id, title and site name independent.
  Recommendation: 1.
- [ ] **Party merge cannot find references (P8, later).** Merging two parties must rewrite
  every live reference to the merged one; they are spread over other modules' role pickers,
  bag-contained positions, portal user links, member bindings and site settings, and
  nothing lets a module declare how its types reference a party. Options: (1) a
  party-reference provider each module registers, asked by merge; (2) one index of every
  picker pointing at a party, maintained on save. Recommendation: 1, designed with merge.
