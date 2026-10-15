# Parties — Person, Organization and the roles composed on them

`Grove.Parties` is the platform-wide party model: the `Person` and `Organization` records,
their contact points, addresses and org positions, the link from a person to a user
account, and the party-type registry that lets other modules compose roles on a party and
get its admin pages without Parties knowing them. Work not yet built is under
[Still to build](#still-to-build).

## Core party records

The base is two records, `Person` and `Organization`, and they are not interchangeable
(a person never becomes an organization). Every other party type is **composed on** one
of them: a separate role content item that points at its party through a `Party` picker
and carries the role's own data. A party can hold many roles at once: the same
organization is a customer, a vendor and a partner, each role with its own numbers,
terms and history. This is composition, never inheritance
([Why not inheritance?](#why-not-inheritance)).

- A `Person` represents a human contact.
- An `Organization` represents a company, nonprofit, agency, household, or other legal/social
  organization.
- A person is not a user account. A person can be linked to a portal/user account; creating
  one for a person is explicit, unless the user account is created first.
- The content type is fixed: a `Person` cannot become an `Organization`.
- Person and Organization are content types, extensible at run time.

**Each role declares its base; business roles are organization roles.** Roles composed on a
party declare which base they point at:

- **Organization roles**: Customer, Vendor, Partner, Lead, Prospect, and every role that is
  a business relationship. An individual (a coffee shop's regular, a street vendor) is an
  **Individual organization**: an `Organization` of kind *Individual*, named after the person,
  created automatically when an organization role is added to a person; the person holds a
  position in it and is its owner. An Individual organization has exactly one member; it
  reads as the person in the UI. A household or a job site (the roofer's house) is an
  ordinary organization with many members, not an Individual organization. An Individual
  organization can be converted into an ordinary one (it then takes more members) when the
  tenant setting allows it.
- **Person roles**: Employee, Member, Sales rep, and roles that describe the person
  themselves. Modules declare them (CRM declares Sales rep as a composable person role with
  its own fields, adds its fields to transactions, and grants real permission roles in its
  code); a person's links to organizations are positions, associations and member bindings,
  through the standard member and hierarchy system.
- **Access follows the base.** A business role reaches through its organization (a caller
  acting for an organization that is a Customer holds Customer: the derived role comes from
  the organization's roles); a person role derives from the person
  ([access.md](access.md) › Parties). A transaction's party is an organization role.
- **Portal access is a promotion.** A contact `Person` with a position in an organization is
  promoted ("invite to portal") to a user linked to it and bound to that organization (the
  first member is the organization's owner and an org-admin, [permissions.md](permissions.md)).
  Self-registration and external-login registration are tenant settings; when on they create
  the Person, the organization (an Individual organization if none is named) and the user
  together. A member binding's target must be an `Organization`.

Implementation:

- **Role base.** `CreatePartyRoleDefinitionsAsync` takes a `PartyRoleBase` (Organization or
  Person); the role's `Party` picker accepts that type only and is required, and the content
  picker save guard enforces the allowed type on every save path. `PartyRoleBases.GetAsync`
  reads a role type's base back. Accounting's Customer, Vendor, Partner and CRM's Lead,
  Prospect are organization roles; HR's Employee and CRM's Sales rep (`SalesRep`,
  `sales-reps`) are person roles.
- **Individual organizations.** The Organization part's `Individual` flag;
  `IIndividualOrganizationService` creates one for a person (named after them, with their
  position, and their ownership through `IOrganizationOwnership` when they have an
  account), finds it and its person, and converts it to ordinary when
  `PartiesSettings.AllowIndividualConversion` allows. One member only: a second position or
  member binding is refused. `IPartyRoleService` gives a party a role by its base (an
  organization role given to a person lands on their Individual organization; a person role
  given to an Individual organization lands on its person; any other mismatch is refused);
  the Create party role activity uses it. API: `POST api/grove/parties/{partyId}/roles`
  (`{ typeKey }`), `POST api/grove/parties/{orgId}/convert-to-ordinary`, `GET|PUT
  api/grove/parties/settings` (`ManageSettings`).
- **Portal promotion.** `POST api/grove/members/invite` (`ManageOrgMembers` against the
  organization): a Person holding a position there, without an account, becomes a member
  user linked to the person and bound to the organization (the first member is the owner);
  no password, the person sets one through portal password recovery. Portal
  self-registration and external-login registration that name no organization create the
  person's Individual organization. `MemberBindingService.MemberRefusalAsync` refuses a
  target that is not an Organization.
- **Transactions** accept an organization role only (Fruitful `TransactionService`).
- **UI.** `PartyRecordActions` on a selected Person or Organization in the party pane: *Add
  role* (the registered role types; an organization role added to a person reports that it
  landed on their Individual organization), the *Individual* badge and *Convert to ordinary
  organization* (only when `AllowIndividualConversion` is on), the organization's
  `GroveOrganizationOwnerPanel` (Access's shared component), and *Invite to portal* over the
  person's positions when the person has no account and Members answers
  (`api/grove/members/me`). Lists describe an Individual organization as "Individual" and a
  role's party reference says "Individual" rather than Organization (`PartyDisplay`, Domain,
  tested). The Parties settings section is contributed to the general settings page through
  the `SettingsPageRegions.General` region (Grove.Components).

Parties owns the **party-type registry** (`IPartyTypeProvider`). A module registers a
descriptor for each role it composes and gets, without further code, its Parties menu
item, its `/Parties/{key}` page and its pane on All Parties. Parties never references
the modules that compose on it; they reference Parties.

## Why not inheritance?

Content types are composition-first: named schemas made from parts and fields, not CLR-style
base classes. Separate inherited-style types (`PersonCustomer`, `OrganizationCustomer`,
`PersonVendor`, `OrganizationVendor`) would duplicate business logic and make querying,
reporting and cross-role relationships harder.

Nor is `CustomerPart` attached directly to `Person` and `Organization`: parts are attached at
the content type definition level, while these roles need to be per-party and repeatable. A
party may have no customer account, one, or potentially several.

## Vocabularies and pickers

- **Party vocabularies**: `parties.contact-point-kind` (each kind's `Category` is its
  hidden type: phone, email, web, social, messaging, other), `parties.address-kind`,
  `parties.organization-type` (the legal form only: Incorporation, LLC, Community Group, NGO,
  Government; trade names are `dba` legal entity names), `parties.organization-name-kind` and
  `parties.association-type` (data-locked module types): module-owned Content Part Lists
  seeded DEFERRED (Parties migrates before Contents' index tables exist at first-time setup,
  so inline seeding aborts provisioning; every early module defers), none locked (the
  address-kind seed gains a DataLock when posting starts snapshotting by kind). Organization
  carries OrganizationType and Industry pickers. Seed keys in
  `PartiesOptionSets`/`ContactPointKinds`/`AddressKinds`.
- **Content Part List adoption.** `Grove.Parties` declares `Grove.Contents.PartLists` in its
  manifest and its migration attaches pickers DEFERRED (global lists seed in a deferred task;
  an inline attach at first-time setup runs before the seeds and aborts provisioning): Person
  gets Honorific, Suffix and PreferredLanguage from Grove's global lists; the `ContactPoint`
  element gets PhoneCountry, and both bag elements get their Kind picker.

### Industry classification

Organization's Industry picker points at the global list
`global.naics` in `global-lists.json`: the 2,125 NAICS 2022 codes (all levels, 2 to 6
digits) from the Census Bureau's file, Category the level name, Parent the enclosing code
(sectors are ranges such as `31-33`), data-locked. NAICS is public domain (17 U.S.C. § 105)
and held once in the global store rather than per tenant. Tenants add their own industries
over it: seeding never overwrites an existing option, so tenant additions, relabels and
hides survive every reseed.

## Contact points and addresses

- **Contact points and addresses**: two named BagParts on Person and Organization,
  `ContactPoints` (element type `ContactPoint`: Kind picker, Value, Label, Preferred,
  PhoneCountry picker) and `Addresses` (element type `Address`: Kind picker, Line1/2,
  Locality, `Geo` (a `GeoStackField` from `Grove.Regions` whose level 1 is the country),
  PostalCode, Preferred). Person and Organization carry no scalar Email/Phone/Website fields.
- **One bag-element service.** The platform stores bags but edits them only through its MVC
  editor, so Parties owns the write surface: `PartyBagElementService` over `PartyBag`
  descriptors (`PartyBags.ContactPoints|Addresses|OrganizationNames`), one service
  parameterized by bag, element type and kind list, never a second bag service. API:
  `api/grove/parties/{partyId}/contacts|contact-points|addresses|names` (authorized with
  View/EditContent on the party item); kinds and phone countries travel as option KEYS,
  translated through the scoped `PartyOptionKeys`; an address's country selects its
  addressing map, which validates it server-side; "preferred" is exclusive per kind.
- **Writes publish.** Contact, address and position writes are live data: the write and the
  publish happen together (`PartyBagElementService.SaveAsync`).
- **One map index per bag element type**, one row per element with Published and Latest
  (the `PartyPositionIndex` pattern): `ContactPointIndex` (kind id, value, preferred),
  `OrganizationNameIndex`, `AddressGeoNodeIndex`. The contact type is not a column: it is the
  kind option's Category, so a query by type resolves the type to its kind ids from the list
  and filters on `KindId`.
- `PartyModel.Email/Phone` are DERIVED (`PartyContactRules.Primary`, pure Domain,
  unit-tested), so `PartyMapper` is a scoped service and its consumers await it. Live check:
  `party-contacts-api.js`.
- **One kind list, with a hidden type per kind.** The user picks one contact kind from one
  tenant-editable list (`parties.contact-point-kind`); landline, mobile, VOIP, WhatsApp and
  LinkedIn are kinds, never separate fields or lookups. Each kind carries its **type** in the
  option's `Category` (phone, e-mail, web, social, messaging, other), which the user never
  selects. Validation runs per kind, defaulting to its type's rule (number, address, URL,
  handle); reports and preferred contact methods group by type, so a tenant-added kind rolls
  up once it has a type. The seed gives every kind a type; a tenant adding a kind chooses one,
  and the data lock freezes it once code relies on it. (How code reaches this is open, P2.)
- **Preferred contact methods are per Person only**: an ordered party-level preference across
  channels on Person. `Preferred` on a contact point picks one entry *within* a kind.
  Organizations have contact information (a main line, a general inbox) but no preferred
  method: an organization is reached through its people. (Channel versus type is open, P3.)
- **Addresses are per-country.** A US and a UK address in the same tenant each take their own
  form; the per-country definitions belong to the localization layer. Parties owns the
  `Address` part and its write surface and consumes the geo tree and the per-country
  addressing map: one `Address` part, generic numbered levels labelled per country, a map per
  country driving form and validation ([regions.md](regions.md)). Gates county-level tax
  resolution.
- **Addresses on non-party records are case by case.** Every record hangs off a Person or an
  Organization; whether a record's editor edits addresses, and through which API, is decided
  per editor, and access follows ownership or access grants to organizations and party type.
  The contacts API is not opened generically to any type carrying an `Addresses` bag.

## Organizations

- **Legal entity names.** The display name (the TitlePart; for the tenant, `ISite.SiteName`)
  is the name shown everywhere. An Organization also carries its legal entity names as a list
  of kinds: the `OrganizationNames` bag (`OrganizationName` element: Kind from
  `parties.organization-name-kind` (legal, dba, former, other), Name, Primary), so an
  organization can hold several DBAs. One primary entry per kind. No effective dates: a
  superseded name becomes the `former` kind. None of these is a second display name
  ([branding.md](branding.md)). API: `api/grove/parties/{orgId}/names`.
- **Parent organization**: `ParentOrganization`, a picker on the Organization part restricted
  to Organization; `ParentOrganizationRelationshipHandler` refuses itself and any descendant
  ([content-items.md](content-items.md) › Picker relationship handlers).
- **The tenant's own organization** exists **before the first user**: created at tenant setup
  by a deferred task (`ITenantOrganizationService`, titled with the site name; `EnsureAsync`
  also creates it when a staff user arrives first) because staff belong to it. It is
  referenced from site settings (`TenantOrganizationSettings`) and represents the tenant's
  legal entity: its legal names, tax id and addresses come from there; the display name stays
  `ISite.SiteName`. By default it has no members, so staff and member logins never share an
  organization; the Members setting `AllowMembersOnTenantOrganization` (default: blocked,
  `api/grove/members/settings`) can allow it. One source of truth: Parties answers "is this
  the tenant's organization" from the site-settings reference (never a flag on the item), and
  every binding write goes through the one Members service that checks it with the setting.

## Party ↔ user link

`Person.PortalUser` is a stock `UserPickerField`, indexed by
`Grove.ContentFields.Indexing.SQL.UserPicker` so person-for-user is a query.
`IPartyUserLinkService` (Parties) owns the writes and the lookup;
`Grove.Members.CreateMemberAsync` calls it when a `PersonId` is supplied (unknown person →
error), so the link is recorded on both sides by the module that creates the account. Who a
member is, and the bindings and hierarchy around the link, are under
[Members](#members-persons-who-act-for-an-organization) below.

## Org structure

A `Positions` bag on Person (element type `OrgPosition`: an Organization option picker over content items
restricted to Organization, Title, Department, Primary; one primary per person). Bag-contained
fields are outside stock field indexes, so `PartyPositionIndex` (Parties-owned map index, one
row per position) makes the organization side a query: `api/grove/parties/{personId}/positions`
(CRUD) and `api/grove/parties/{orgId}/people`. Live check: `party-positions-api.js`.

## Members: persons who act for an organization

A **member** is a person who acts for an organization within the tenant (a customer's or a
vendor's person) through a user account; **staff** are the tenant's own people. Both are
ordinary platform users in the tenant's user store, with the same user management, the same
roles and permissions machinery and the same hierarchy; what differs is the organization
they act for and the shell they sign in to. The party model carries the relationship, the
user record carries authentication and roles. `Grove.Members` owns it, on `Grove.Parties`
(a member is a Person with a portal user; a binding points at an Organization) and on the
platform's user, role and permission pipelines; nothing in it is specific to a line of
business. The sign-in side (the member portal, sessions, sides) is
[shells-and-themes.md › The member portal](shells-and-themes.md#the-member-portal-login-sessions-sides);
the access side (the class, the ceiling, the caller) is [access.md](access.md) › Members and
[workflows.md › Operations › The member caller](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery).

**Layering: identity in the user layer, structure in content.** Identity and the
authorization hot path (class, active organization, effective roles) live in the user layer;
the declarative organization structure (organization settings) lives in content, editable,
versioned, permissioned and importable, which is what makes thousands of users tractable; the
hot path reads a flat projection rebuildable from it. Not everything in the user layer
(structure would not be content, so every admin surface would be hand-built), and not
everything in content (the content store on every request, or a cache that is the projection
again).

**Members and subscriptions are two systems.** Members is who may act for an organization:
the bindings, their roles, the hierarchy, the member admin. Subscriptions, seats,
entitlements, tiers and perks are a downstream module's, built on it. Holding a seat says
nothing about member access and member access implies no seat; what they share is the
resolution context, the member's active organization. Members raises a binding's lifecycle
through `IMemberLifecycleHandler` (`MemberBoundAsync` / `MemberUnboundAsync`, resolved in a
fresh scope after the binding write commits) and knows nothing more about its consumers.

**The link.** `Person.PortalUser` (above) is the person ↔ account link, written through
`IPartyUserLinkService.LinkPortalUserAsync` by `Grove.Members.CreateMemberAsync` whenever a
`PersonId` is supplied, and the person's org position links to the Organization.

**Bindings.** One account can hold several **organization bindings**, with different roles
per organization: the binding record (`GroveMemberInfo` aspect on the user, the record of
truth) says in which organization, in which class, and with which role templates.

- **Class is a set derived from the bindings**: staff is a binding to the tenant's own
  organization, member a binding to any other, so a user may be both. `UserConnectionIndex`
  (one row per `(UserId, OrganizationId, Class)`, unique, no nulls) answers "is staff", "may
  act for org X", "members of org X" and the switcher's organization list. One class claim
  per class; the login gate admits a staff connection at the tenant surfaces and a member
  connection at the portal; the caller acts as staff on the admin and site sides when the
  user has a staff connection.
- **One home per fact.** `UserConnectionIndex`, the flattened projection on the user object
  (what the caller builder reads) and the hierarchy nodes are projections of the binding
  record, written only by `MemberBindingService` (the one binding write path, which also
  enforces the tenant organization rule) and rebuilt by `POST api/grove/members/rebuild`
  (re-saves every user, places a user with no binding as staff and rebuilds the hierarchy).
- **No user exists outside an organization**: the tenant and its organizations exist before
  any user, so registration places the member in an organization or refuses.
- **Conversion is adding or removing a class**, its own permission-gated action
  (`api/grove/members/{userId}/classes`, `ConvertUserClass`, bumping the security stamp);
  adding staff keeps the bindings, removing member ends them. The binding API refuses to add
  or end a class.
- **Role templates.** Roles are per user in the platform and `RolesDocument` is one cached
  per-tenant blob, so per-organization roles are not thousands of role rows: the tenant
  defines a small set of member role templates (Member, MemberAdmin, …) in the normal role
  editor, and a binding's roles are placed in the organization's groups
  ([permissions.md](permissions.md)). The member's effective permissions come from (user,
  acting organization); the hierarchy places the member once per binding.

**Member admin.** Each organization has a member admin, defaulting to its first member (the
owner, [permissions.md](permissions.md) › The organization owner). Their scope: their
organization's members (hierarchy and sub-users), member data and credentials, and the
organization settings future modules expose to them (autopayment scheduling, payment types,
wallet connections). Not theirs: tenant features, tenant settings, users and data outside
their organization. Enforcement is two-layered: the hierarchy bounds the **who**, ordinary
permissions bound the **what**; the member admin holds a role the tenant shaped, never a
parallel authority. Organizations choose people, tenants choose powers.

### Hierarchies

**The hierarchy is relational, not a tree of slots**: a set of manager → report relationships
between users, not an entity users are placed into. A user can have two managers, a manager
many reports, and no two users' patterns have to match. It answers ownership and visibility,
who sees whose records, and who manages whom ([permissions.md](permissions.md) › Who manages
the permission model); permissions stay roles.

**Relationships are typed.** *Reports-to* is the hierarchy: it grants a manager visibility
of their reports' records and stays within one root (staff, or one organization). Other
types are **associations**, and they may cross between staff and an organization: a large
customer organization can have several sales reps assigned, each connected to different
contacts there; each rep sees only what their own connections give them, and the
organization's people do not see the reps' side. An association grants no visibility by
default. "Teams" are this hierarchy; there is no teams table.

**One shared store, one root for staff and one per organization.** The organization-A
versus organization-B boundary inside a members-only table is as sensitive as the
staff-versus-member boundary, so the mandatory root-scoped guard has to exist anyway; a
second table would duplicate schema and queries without adding protection. Every query
and write carries the root or organization id, so the scope resolver stays class-blind
(it only asks subtree(user)) and one tree-management path serves staff admins and member
admins alike. What the store answers: **subtree(user)** ("me and everyone under me", the
expansion in front of the fail-closed scope machinery), **chain(user)** (every path to the
root, for escalation and approval routing) and a node's membership (user, parent, the
organization or the staff root).

**A closure table on YesSql**, no hand-written SQL. `UserHierarchyMembership` is a document
per (user, organization) holding the direct managers and the computed ancestors;
`UserHierarchyClosureIndex` emits one row per ancestor (`AncestorId, DescendantId, OrgId`), so
"everyone under me" and "everyone above me" are index queries, and `HierarchyClosure` holds the
pure rules (ancestors, the cycle refusal, every path up). The staff hierarchy is the tenant
organization's. Edge writes take a per-organization lock held until the unit of work commits,
recompute the ancestors of the report and everyone below and save the documents that changed;
an edge is refused when the report is already above the manager; removing a node detaches its
reports; `RebuildAsync` repairs from the edges. API:
`api/grove/members/hierarchy/{organizationId}` (nodes, `edges` add and remove),
`ManageOrgMembers` against the organization.

**Associations are content items**, kept apart from the hierarchy so the root guard and the
closure table never see them: the `Association` content type (FromPerson, FromOrganization,
ToPerson, ToOrganization, Type from `parties.association-type`), `AssociationIndex`,
`IAssociationVisibilityResolver` (per type: whether the far end sees it, which parties it
grants; default nothing) and `GET api/grove/parties/associations/me`. Module types are built
in and not editable; tenant types are additions to the list; resolvers come from modules.

### What `Grove.Members` contains on the party side

- Bindings: the `GroveMemberInfo` aspect, `UserConnectionIndex`, `MemberBindingService`,
  `MemberService` (reads, member creation), `UserClassService` (add or remove a class), the
  endpoints (`api/grove/members`: me, active-org, list-by-org, create, invite, bindings
  add/remove, classes, settings, rebuild, hierarchy). The user list filters by class with the
  `class:member` / `class:staff` named term (`UserClassAdminListFilterProvider`).
- Provisioning: every new user is placed in an organization at creation
  (`UserClassStampHandler`: staff of the tenant organization, or on a portal request a member
  of the portal's organization, with a Person created for an external-login member); the
  hierarchy node is written inline once the user has its id; member role templates ensured by
  a deferred, idempotent migration.
- The hierarchy store above.
- Ordering rule: inline vocabulary seeding in Parties' `CreateAsync` aborts first-time
  provisioning (index tables not yet created), so it is deferred, like hierarchy writes.

## Party roles as data

Customer, Vendor, Lead, Contractor, Guest, Employee...: a party can hold many. They are
distinct from security roles (Admin, Supervisor...); this module is where that split lives.
Person and Organization are the base party records, and every other party type is a separate
role content item pointing at one of them through its `Party` picker (the shape
`CreatePartyRoleDefinitionsAsync` stamps). The roles themselves are declared by the modules
that compose them.

## Party-type registry and All Parties

Parties owns `IPartyTypeProvider` / `PartyTypeDescriptor` (Domain) and `PartyTypeCatalog`; the
Parties admin menu, the route gates and `GET api/grove/parties/types` are all generated from
it, so a module that composes a party type registers a descriptor and gets its menu item, its
`/Parties/{key}` page and its All Parties pane at once. Parties registers Contacts (Person) and
Organizations; other modules register the role types they compose. Parties registers no role
of its own, so All Parties is exercised by the checks of whichever module registers one.

- **The registry is the only way a type reaches the Parties UI**: no page or menu entry is
  hand-written per type. Parties gains nothing per type: the party-type catalog is a page
  registry source (`PartiesPages`, a `KindPageSource`), giving each type its menu item and its
  route gate ([architecture.md › One page registry](architecture.md#one-page-registry)).
- **A type's dedicated page belongs to its kind**: tenant kinds use Parties'
  `/Parties/{key}`; a module may register a **global kind** (a catalogue that is global and
  read-only, with the tenant's own standing kept in a tenant-side record), whose descriptor
  names its owner's page and whose All Parties pane is the owner's contribution through
  Grove's page-region seam ([page-regions.md](page-regions.md); `PartiesPageRegions.Pane(key)`;
  `Detail(key)` opens a selected record's summary to downstream blocks), so Parties never
  references the owning module.
- **All Parties** (`/Parties`, the Parties menu's "All Parties" child) lists the types down the
  left and the selected type's pane (the list/detail/inline-editor shape, generalised) in the
  main section. The Parties root menu item keeps its URL, but the primary nav never links a
  parent with children, so the "All Parties" first child carries the link.
- **Hide/show is the admin menu's own mechanism**: the tenant's layer hides a type for
  everyone; the page's pane settings hide one for the signed-in user only, through the per-user
  layer (`api/grove/navigation/me/hidden`, keyed by the served menu item key, so the sidebar
  link goes with the pane; [admin-menu.md](admin-menu.md) › Two hide layers). The per-user
  layer is an overlay, never a copy: it stores only the user's own hidden item keys, is applied
  last, and a user cannot unhide what the tenant hid.

## Still to build

- [ ] **Link new user accounts to a `Person`.** Creating a user account auto-creates or links
  a `Person` profile (today only external-login members and portal registrations get one);
  that does not mean every `Person` can log in.
- [ ] **User editor class section**, read-only, via `SectionDisplayDriver<User, T>`;
  conversion is not edited there, it is its own action.
- [ ] **Hierarchy-aware scope expansion** feeding the fail-closed scope providers
  ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)):
  subtree(user) as an input to the content scope, joining on the organization-scope axis
  (`GroveOrganizationPart` and its index). Not wired yet; the management bound on users
  already uses the closure table ([permissions.md](permissions.md)).
- [ ] **Per-type contact validation and the kind data lock** (› Contact points and
  addresses), pending P2.
- [ ] **Preferred contact methods on Person**, pending P3.
### Tenant-defined party roles

- [ ] (Deferred, far down the pipeline.) A tenant could create
  a party role without code: a party role is a content type with a `Party` picker, so only the
  registry needs code (a built-in provider could register every content type the tenant marks
  as a party role). Kept out for simplicity: a role ties deeply into its UI (menu,
  `/Parties/{key}` page, panes, editors), and a tenant-made role has no module to supply that
  UI. It must be figured out before the Blazor UI designer is done
  ([blazordesigner.md](blazordesigner.md)), because the designer builds UIs for tenant-made
  types and tenant-made party roles are one of those. Until then party roles come only from
  modules.

## Open questions

- **Grouping.** The permission model has groups that bind users to roles
  ([permissions.md](permissions.md)). A starting point for a separate visibility concept:
  **groups grant access (what you can see) and roles grant actions (create, update, delete and
  the other verbs)**, every action requiring that you can see the record first. Open with it:
  capability screens (settings, audit) as actions; answering the platform's view permissions
  from the access system; business roles as derived groups. Until then, sharing uses the
  **no-permission role shim** ([media.md](media.md) › Terms).
- **NAICS outside North America.** NAICS is North-America-only, which matters if party
  industry is ever reported on internationally.
- **Party merge/dedupe mechanics** (survivorship rules, live-reference rewriting UI, audit
  trail), designed later. The enabling rule is decided: live references by id plus
  posted-document snapshots; what a posted document copies is decided by the posting workflow
  of the module that owns the document, not by Parties.
- **Party merge cannot find references (P8, later).** Merging two parties must rewrite every
  live reference to the merged one; they are spread over other modules' role pickers,
  bag-contained positions, portal user links, member bindings and site settings, and nothing
  lets a module declare how its types reference a party. Options: (1) a party-reference
  provider each module registers, asked by merge; (2) one index of every picker pointing at a
  party, maintained on save. Recommendation: 1, designed with merge.
- **Code switches on contact kind keys (P2).** `PartyMapper` and the primary-contact rules
  hard-code the e-mail and phone/mobile kind keys; values are only checked for being
  non-empty; a kind without a type becomes "Uncategorized" and does not roll up; the data lock
  is enforced in the controller, not the service or the seed. Options: (1) code reads a
  contact's type, never its kind key; every seeded kind gets a type with a data lock; values
  are validated per type; a kind without a type is refused; the lock is enforced in the
  service; (2) keep kind keys in code, types only for reports. Recommendation: 1.
- **Preferred contact methods versus types (P3).** "Text" and "call" are both type "phone", so
  the preference is about a channel, which the type list cannot express; and with finer kinds
  (landline, mobile) "preferred within a kind" changes meaning. Options: (1) a small locked
  list of channels (call, text, e-mail, messaging, …) and an ordered picker on Person, each
  channel saying which contact types can serve it; (2) order the person's own contact points
  directly. Recommendation: 1; the preference survives a changed number, and "text" can pick
  the person's preferred mobile.
- **The tenant's own organization in lists (P6).** Positions, role pickers and the
  Organizations list show it like any customer; its addresses overlap the tenant's Locations;
  `TaxIdentifier` is one value but a business can have several tax ids; its title is a second
  name beside `ISite.SiteName` that can drift. Options: (1) show it marked "your organization"
  where relevant and exclude it from customer-facing pickers; Locations are premises, the
  organization's addresses are legal and mailing; tax ids become a typed list (like legal
  names); the title follows `ISite.SiteName` (editing one updates the other); (2) hide it from
  all lists, keep one tax id, title and site name independent. Recommendation: 1.
