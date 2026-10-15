# Grove architecture

Grove is a platform and the application layer built into it. This document is its shape: what it holds, the
modules that make it up, who signs in to it, and the rules that keep downstream modules plugging
into it rather than it reaching for them. The global store, the geographic tree and money have
their own documents: [globals.md](globals.md), [regions.md](regions.md),
[types.md](types.md).

## What Grove is

Grove has two halves in one repository. **The platform** (the projects listed in
`src/Grove.Infrastructure.Build/Platform.Projects.props`, and `test/`) is a hard fork of OrchardCore: tenants, users, permissions, content, features, settings and the module
system. Grove develops it as its own code. **The application layer** is everything a business-facing
application needs that is not a line of business. Its foundation is built — the Blazor admin and
site shells, the content-item API, icons, localization, Content Part Lists, the global
store and the geographic tree. Three modules complete it:

| Module | What it is |
| --- | --- |
| `Grove.Parties` | Who an application deals with: Person and Organization content types, contact points, addresses, positions, a role registry (`IPartyTypeProvider`) that downstream modules register their party roles with (customer, vendor, employee, lead, …), and the parties UI. |
| `Grove.Access.Members` | The other side of the business: member accounts (ordinary Orchard users with a class marker), organization bindings with per-organization roles, a hierarchy store, the member portal (login, register, external login, pages), and impersonation for staff support. Commercial memberships (tiers, seats, groups, perks, entitlements) are a downstream module's, which hooks member lifecycle events through `IMemberLifecycleHandler`, a seam Members declares. |
| `Grove.Access` ([access.md](access.md)) | The one access machinery every read and write passes through ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)): the caller built per request from the authenticated identity and server-held state (roles, permissions, organization, class ceiling) under a per-tenant permission version; one decision behind the platform's authorization service; scope providers per table compiled into every list, query and picker; the audit of decisions; the system caller for background work. Contracts in `Grove.Access.Abstractions`, below `Grove.Data`. One calculation per request, handed down the request's chain on the server, never across requests. |
| `Grove.Data.Queries` | The read side: the structured query model (a step list compiled through YesSql into parameterised SQL with the caller's scope conjoined for every table), the SQL source for external databases with its scope rewriter, the `System` source (`item`, `user`, `site`) that display slots bind, query descriptors and the catalog the workflow engine's registry reads. Every run is paged, cancellable, typed and caller-scoped. Queries owns no connections: it consumes SQL connections from the data layer and external connections from the protocol modules ([queries.md › Connections](queries.md#connections-the-protocol-modules-and-the-data-layer)). |
| `Grove.Workflows` | The action side: a workflow service built on a vendored fork of Elsa 3 (MIT). Its registry (activities, triggers, hook slots, flows, and queries as `RunQuery` activities) is the operation registry; units of work (one burst, one session, one transaction, with inline hooks and durable background calls); the access gate as engine middleware with the actor rule (a run acts as the caller who started it, or as the system when published so); connectors with API-key, bearer, basic and OAuth2 authentication and OpenAPI import (☐ the connection model moves into the protocol modules, the activities stay as consumers); approvals; field dependencies; ownership tiers and per-flow access; the platform modules' activities and triggers (the stock OrchardCore workflow module is gone); the designer bridge. |
| `Grove.Data.Types.Money` | The money type and the `PriceField` a tenant puts on any content type: `Amount` bound to an `ICurrency`, ISO 4217 metadata from the global store so minor units are never a per-tenant guess, and a default currency. Adapted from OrchardCore.Commerce (MIT). See [types.md](types.md). |
| `Grove.Contents.OptionPicker` | The one field type for picking: `OptionPickerField` over option sources (content part lists, users, content items, currencies, cultures, time zones), with cascading filters, its index, its API and its UI (the picker component and the field editor, from the module's own client library). Content items are a source, not a separate picker field. Every host gets it through `Grove.Application.Cms.Core.Targets`. See [server.md](server.md) § 3. |
| Content Part Lists | Tenant-editable option sets with shared global sets: the `Grove.Contents.PartLists` feature of `Grove.Contents`, opt-in per tenant. See [content-items.md](content-items.md). |
| `Grove.Data.Kinds` | The kind-registry contract, server-free: `IKindDescriptor` and `KindCatalog<TKind>` (merge what the enabled features register by key, order it, find by key or content type). Party types are one registry on it; a downstream module's own registries (asset kinds, transaction kinds) are others. `KindPageSource<TKind>` in Grove.Infrastructure.Server.Abstractions builds a registry's pages (menu items and route gates) from it. |

The thesis: **all an application needs is parties, workflows and content items.** A
party is who, a workflow is what happens, a content item is everything else. Line-of-
business objects (accounts, inventory, transactions, invoices) are content items with a
registry and a discipline, and they belong to the modules that own that business.
Members is Grove's because every business with a public face has members, customers or
users on the other side of it, and the member portal is how a product reaches them.

## Direction: Blazor or headless, nothing else

Grove is a hard fork of OrchardCore; the platform's projects sit beside Grove's own under
`src/`. When the platform lacks something, the platform is changed: no shims, no stock-id
module forks, no workarounds kept to stay mergeable.

**The platform is named `Grove`, fully**: namespaces, assemblies, project and folder names,
package ids, feature and module ids, recipes, configuration sections and static asset paths.
Identifiers that would carry the project name are neutral, per the naming rule
(`PlatformBuilder`, `AddPlatform()`, `IPlatformHelper`, JS globals likewise), so a later
rename touches module names only. Prose says "Grove", not "Orchard Core". External URLs and
the `OrchardCore.Translations` packages keep their names. Neither the platform nor
Grove.Infrastructure.Server is named just `Grove`:

- the platform's implementation library is `Grove.Infrastructure` (Grove is one system, with
  no framework-versus-CMS split);
- its contracts are `Grove.Infrastructure.Abstractions` (shell host, modules, extensions,
  caching, locking, background tasks, options invalidation);
- `Grove.Infrastructure.Abstractions` holds only primitives: JSON, routing, localization, setup and helpers;
- the document-manager contracts sit in `Grove.Data.Abstractions`, which depends on
  Infrastructure.Abstractions and not the reverse.

**A module is at most two projects, `Grove.X` and `Grove.X.Abstractions`.** The
`.Abstractions` project holds the module's contracts (interfaces, permissions, constants,
options, models, settings, fields, indexes); shared implementation stays in the module and
consumers reference the module. The GraphQL library builds on `Grove.Contents.Abstractions`,
since the Contents module itself depends on that library. Sub-module names such as `Grove.Search.Elasticsearch`
stay. Feature ids such as `Grove.Access.Roles.Core` are tenant data and keep their names.

Where a platform module or type meets Grove code that was written to sit over it, the side
Grove has already replaced is deleted, or the Grove code is merged into the platform module.
Platform modules Grove has not reached yet stay until a replacement exists. As each Blazor
replacement lands, the Liquid templates and the stock Razor/Vue UIs it replaces are removed,
until every page is a Blazor build. Headless (the content-item API, GraphQL, Queries) is first
class alongside Blazor: every capability a Blazor page has is reachable through the API, and
no feature exists only in the UI. Stock front-end assets and their build pipeline shrink with
the UIs they serve.

Everything is streamlined to Blazor: no work goes into other UI systems. Templates, layouts,
pages and content items become data rendered into Blazor components through the platform's own
display system, with its HTML binding replaced by a component binding
([blazor-display.md](blazor-display.md)). There is **one expression engine** for the whole
application: query parameters, route and title patterns, workflow expressions, notification
and email templates. Liquid as a view engine goes with the stock UIs.

- **Liquid (Fluid) stays for expressions** and is the default language.
- **The workflow engine's expression system is the one engine** (plan in
  [blazor-display.md](blazor-display.md) § 12): its expression model, descriptors and input
  metadata are the contract for workflows and for display; the platform's Liquid is its
  Liquid implementation; the platform-side evaluators and `Grove.Scripting` are removed,
  replaced by workflows. A workflow is written as a script or edited as a flow diagram; both
  are front ends for the same objects.
- Every read and write is an **operation** in one registry (the engine's descriptors plus a
  query kind), behind one access machinery
  ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)).
  A module declares its API; its implementation may be generated from its operations, and a
  generated route is an entry in Grove.Infrastructure.Server's API registry beside the declared ones
  (§ Modules declare, Grove.Infrastructure.Server registers and serves).
- Admins can grant C#, JavaScript and Python scripting access. Granting is only the first
  step toward sandboxing: those providers run code on the server and must be hardened
  before any tenant uses them. Neither C# nor Python exists in the vendored engine yet.

## Three shells

Grove serves three audiences, each with its own shell: the **admin** shell for staff, the
**site** shell for the public, and the **member** shell — the application a member or
customer actually uses, optional and present when Members is enabled. Modules contribute
pages to a shell rather than shipping their own. Themes are not interchangeable: a theme
declares which shell it serves and whether it hosts a Grove shell document, and a module
declares the contract its pages need. See [shells-and-themes.md](shells-and-themes.md).

## The user vocabulary matches the shells

Grove knows exactly three kinds of visitor, one per shell:

- **users** — tenant users, who sign in to the admin shell;
- **members** — organization-bound users, who sign in to the member shell;
- **anonymous** — the site shell's visitors.

Every one of them is an ordinary OrchardCore user in the tenant's own store (anonymous
aside); the class is a marker that decides which login surface accepts the account, and
Grove owns it along with the org bindings, the hierarchy and the surface separation.

**Employee is not one of them.** An employee is a person the business employs — an
employment record, a party role, payroll — so it belongs to a downstream module. Some
employees are tenant users and some never sign in at all; Grove neither knows nor cares.
Products keep the three words and label them for their domain without renaming anything.

**Super-tenancy splits.** The Default tenant, cross-tenant scope
(`IShellHost.GetScopeAsync`) and the platform-user class are OrchardCore concepts, so the
mechanism and its seams are Grove's. A SaaS product built on them — tenant provisioning
and lifecycle UI, plans and billing, operator dashboards, the hosting infrastructure — is
not.

**Grove.Access.Members keeps its own permissions and hierarchy.** Member roles, the org-admin
ceiling and the per-organization permission scope are Grove's, parallel to but separate
from the tenant-user side: a product on Grove alone needs members with differentiated
access and org admins who manage their own people, with no line-of-business module present.
A downstream module's entitlements are a layer above those permissions, never a replacement
for them.

## Parties is the model, not the roles

Grove has the party **model** — there is no engine to speak of: content types (Person,
Organization, Address, ContactPoint, Position, Location, the `parties.*` vocabularies on
the `Grove.Contents.PartLists` feature, the address's geo stack (`GeoStackField`) on `Grove.Data.Regions`), the contacts /
positions / locations services and API, the Person ↔ portal-user link, three indexes, the
`IPartyTypeProvider` registry with the menu and generic role pages generated from its
catalog, and two workflow activities. It is Grove's for two reasons, neither of them
cleverness: Members depends on it (a member is a Person with a portal user; an org binding
points at an Organization), and every product needs people and organizations before it needs
anything else. It ships no party role of its own.

Parties defines **no field types of its own**: the party picker on a role is an
`OptionPickerField` over content items constrained to Person or Organization, the portal-user link a stock
`UserPickerField`, an address's country and the levels below it a `GeoStackField` from
`Grove.Data.Regions`, and honorific, address kind, phone country, organization type and
industry are `OptionPickerField`s (Grove.Contents.OptionPicker) over content part lists. The only Parties-specific
helper is `PartyRoleDefinitions` ("define a role type: party picker + number field +
profile"), which downstream modules and products call. The role content types are defined
and registered by the modules that own them.

## Every content field type lives in Grove

Field types are platform vocabulary: a tenant composes content from them in the type
editor, and an application on Grove alone must be able to model prices, places and option
sets without a line-of-business module. So **every custom `ContentField` belongs to Grove**
unless it is proprietary or depends on proprietary code. Three exist: `OptionPickerField`
(Grove.Contents.OptionPicker, [server.md](server.md) S2), `GeoStackField` (Grove.Data.Regions) and `PriceField` (Grove.Data.Types.Money,
[types.md](types.md)).

## Naming

`Grove.Parties`, `Grove.Access.Members`, `Grove.Workflows`, matching `Grove.Data.Regions`,
`Grove.Data.Global`, `Grove.Contents.OptionPicker`. Namespaces, assembly names, feature ids,
manifests, permission names, option-set keys (`parties.*` — already unbranded), route
constants and test names follow.

**"Grove" appears only in module naming.** Modules, assemblies and packages carry the
`Grove.` prefix; nothing inside them does — not permission names, types, functions, settings
keys or other internal names (`ManageConnections`, not `ManageGroveWorkflowConnections`). The
project must be renameable by renaming its modules alone. The same rule holds for downstream
projects. Routes, JSON property names and recipe step names are not renamed for it.

The rule is about services: `Grove…` service, controller, claim, settings-key and permission
names are not used (`IGlobalStore`, `ContentPartListService`, `AuthController`,
`ClientWebAssemblyHost`, `LazyModulesManifest`, `[BlazorComponent]`,
`ManageGlobalReferenceData`). The UI component library (`GroveButton`, `GroveDataGrid`, …)
keeps its prefix for conflict resolution with HTML and Blazor names. Where a Grove service
would clash with a platform service of the same name, the Grove one keeps the prefix. Names
that still carry it, and why:

- **Stored by type name** (renaming would orphan data): the content parts `GroveOptionPart`,
  `GroveContentPartListPart`, `GroveRegionalProfilePart`, `GroveRegionalProfileReferencePart`,
  `GroveAssignmentPart`, `GroveOrganizationPart`, `GroveBlazorComponentPart`; the indexes
  `GroveAssignmentIndex`, `GroveOrganizationIndex`; the migrations (the migration record keys
  on the class name) `GroveContentPartListMigrations`, `GroveAssignmentIndexMigrations`,
  `GroveOrganizationIndexMigrations`; the documents `GroveAdminMenuLayoutDocument`,
  `GroveContentGroupsDocument`, `GroveMenuPlacementDocument`,
  `GroveGlobalSchemaState`; the definition and entity settings `GroveOptionFieldSettings`,
  `GroveDefinitionLockSettings`, `GroveFieldVisibilitySettings`, `GroveContentTypeMenuSettings`,
  `GroveContentGroupsSettings`, `GroveLocalizationSettings`, `GroveMemberThemeSettings`,
  `GroveUserMenuPreferences`, `GroveMemberInfo`; and the site-settings keys `GroveTitleBar`,
  `GroveIconProviders`, `GroveAdminPrimaryNavMenu`. They go together at the first tenant reset
  that allows it, or with an upgrade step after the first public release.
- **Would clash with a platform type of the same name** (the clash exception): the admin
  API view models and their client mirrors that duplicate a module's own model
  (`GroveUser`, `GroveTenant`, `GroveQuery`, `GroveLoginSettings`, `GroveTemplatePreview`,
  `GroveDeploymentPlan`, `GroveLayer`, `GroveMediaOptions`, the icon catalog records and about
  25 more), `GroveMediaProfilesController`, `GroveAntiforgeryToken`. Each
  folds into its module's own model when the API it serves moves out of Grove.Infrastructure.Server into the
  module that declares it ([MVC/api.md](MVC/api.md) § Placement).
- **The component library**, `Grove.Display.Components` whole, including its client plumbing
  (`[GroveShell]`, `[GroveEager]`, `[GroveJSComponent]`, `GroveShellContext`,
  `GroveLateServiceProviders`, `GroveApiLocalizer`, the files and sharing clients); whether that plumbing counts as
  components is an open question (below).
- **Still to rename:** the Workflows permission names (`ViewGroveWorkflows` and siblings; the
  connection one becomes the connection system's `ManageConnections`, [queries.md](queries.md)),
  the client API root property `Api.Grove`, and `IsGroveBlazor`/`IsGroveBlazorTheme`
  (a JSON property on the themes API).

**No downstream name appears in Grove.** Grove is open source and knows nothing of the
products built on it — not in a namespace, an assembly name, a route, a comment, a test name
or a fixture. That includes the vendored engine subtree:
`engine/src/modules/Grove.Workflows.Core`, `.Engine`, `.Api` and the rest are
`Grove.Workflows.*`, with their namespaces and internal references renamed with them. The
engine API prefix is `grove-workflows/api` and activity type names are
`Grove.Workflows.*`.

The cost is accepted deliberately: the subtree is tracked against
`engine/UPSTREAM-COMMIT`, and renaming it makes future upstream diffs noisier. Pulling an
upstream Elsa change therefore means applying it against renamed files — a mechanical
rename of the incoming diff, documented at the top of the subtree
([UPSTREAM.md](../src/Grove.Workflows/UPSTREAM.md)) so whoever does it next knows.
`engine/LICENSE` (Elsa, MIT) and `UPSTREAM-COMMIT` stay exactly as they are; the fork's
provenance is recorded in the subtree README rather than in its type names.

## Rules that hold

- Grove never references a downstream module. Downstream modules register with the
  registries Grove declares (`IPartyTypeProvider`, workflow activity/trigger/hook/flow
  providers) and attach their own parts to Grove's content types from their own
  migrations. The consumer owns the interface; the host composes.
- No literal path strings; routes flow through the platform's own systems (see
  [agents.md](../agents.md)).
- Nothing reaches data except through the access machinery: a caller, a decision and a
  scope for every read and write, in every surface ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)).
- Pre-release: no compatibility code; restructure outright. Until the first public release,
  migrations are edited in place and dev tenants are reset when shipped data changes; from the first public release on, every change ships an upgrade step
  (`UpdateFrom`, backfill tasks). A plan that relies on a reset says so.
- Blazor pages and components that depend on a platform module being enabled are kept
  isolated, one place per module, so they move with that module when it is reworked or
  removed.

## Modules declare, Grove.Infrastructure.Server registers and serves

A central tenet of the application.

- **A module declares its own surfaces in code**: its APIs, pages, display templates,
  components, placements, access and permission systems, content, media, queries, search
  indexes and so on. Its own services answer them.
- **Each seam has one central registry, and modules register into it.** Another module
  consumes a seam only through its registry, never by reaching into the declaring module.
  This is what makes a feature switch on and off cleanly (a disabled module's registrations
  vanish with it), lets a downstream module override a module's registrations, and keeps the
  system modular. The component registry, zone keys, value types and party types follow it;
  Grove.Infrastructure.Server owns the two registries of what it serves: the **API endpoint registry** and
  the **page registry**.
- **Grove.Infrastructure.Server is the single server clients connect to, and only that.** It is the entry
  point and the hosting: it takes incoming requests, routes them, hands them to Access
  (authentication, antiforgery, authorization), answers them from the registered surfaces, and
  connects outward through the data layer and the protocol modules (`Grove.Http`, `Grove.Rest`,
  `Grove.Soap`, `Grove.GraphQL`, ☐ to be created; [queries.md › Connections](queries.md#connections-the-protocol-modules-and-the-data-layer)). It keeps
  only hosting plumbing: route groups, per-tenant gating, the antiforgery filter (validation is
  Access's), JSON options, the error contract, the OpenAPI document and the registries. Every
  API is declared by a module, system management included: features, tenants, setup, recipes
  and settings belong to `Grove.Infrastructure.Features`, `Grove.Infrastructure.Tenants`, `Grove.Infrastructure.Setup`, `Grove.Infrastructure.Recipes`,
  `Grove.Infrastructure.Settings` and `Grove.Infrastructure` ([MVC/api.md](MVC/api.md) § Placement). Keeping
  the server thin is what allows a later proxy in front of many Grove instances. After the MVC
  retirement Grove.Infrastructure.Server is renamed Grove.Core ([server.md](server.md)).
- **Not Grove.Infrastructure.Server's:** database connections (`Grove.Data`, `Grove.Data.YesSql`), outgoing
  connections to external systems (the protocol modules) and search
  indexes (`Grove.Data.Indexing`; index profiles belong to their module and register like any
  other surface).
- **Modules reference `Grove.Infrastructure.Server.Abstractions` only** (the contributor interfaces), never
  Grove.Infrastructure.Server, so registering creates no reference cycle.
- **The API surface is ASP.NET minimal APIs**, not controllers and not FastEndpoints: every
  handler names its binding source, and request DTOs carry Grove's validation ruleset
  ([MVC/api.md › Hosting](MVC/api.md#hosting), [› Validation](MVC/api.md#validation)).

### Two layers in each registry

Both the API registry and the page registry have two layers:

- **Declared**: from module code. Built when the tenant shell builds, follows feature
  enablement, never stored in the database.
- **Defined**: data in the tenant database, changeable at runtime, served by Grove.Infrastructure.Server
  through a dynamic source that is invalidated on publish. Designed pages, workflow HTTP
  endpoints, generated operation endpoints and the tenant's admin-menu edits are defined
  entries.

The page registry's declared layer is the routed components' `[PageEntry]` declarations and the
modules' `IPageSource`s; its defined layer is the `IDynamicPageSource`s (designed pages, New ›
*content type*, content groups, custom settings types) and the tenant's menu edits
(`src/Grove.Infrastructure.Server/Routing`, contracts in `Grove.Infrastructure.Server.Abstractions/Routing`).

### Workflows builds on top

Workflows builds on the API surfaces modules register: automated emails, invoice-to-bill,
budgeting, approval flows from downstream applications. A domain API is never replaced by, or
moved into, a workflow endpoint. Workflows hosts nothing itself: its management API is declared
and registered like any module's, and its `HttpEndpoint` routes are defined entries in the
Server's API registry, not a private table
([workflows.md › Workflows builds on top of the module APIs](workflows.md#workflows-builds-on-top-of-the-module-apis)).

### One page registry

Each page declares its route, shell, permission and menu placement (parent path, caption,
position, icon) once. The route table, the route gate and the admin menu's default tree all
derive from that entry. The admin menu is a view over the registry plus its defined layer
(tenant renames, hides, moves, custom placements and links, applied by stable entry id) plus
each user's own hide layer; per-tenant and per-user page permissions resolve through the access
model, and the menu and the gate read the same permission. Legacy MVC pages are declared
entries pointing at their legacy route until they are ported; dynamic items (New › *content
type*) are a registry source. The mechanism is in [admin-menu.md](admin-menu.md) › The page
registry.

### Modules, features and how a module reaches the host

A **module** is an assembly with a manifest; a **feature** is the unit a tenant enables. The
host discovers modules at build time: each module package carries a `build/` props file that
names it, and the host assembly lists every module so named (`ModuleName` attributes). NuGet
does not pass a dependency's `build/` assets on, so a module that reaches the host only as
another module's dependency is copied as a library and never loaded: its startups, permissions
and registrations do not exist.

- **Standard: separate modules, pulled in by their parent.** Each concern is its own module
  (own features, startups, migrations, client library), replaceable and removable downstream.
  A parent that always brings children references them with `PrivateAssets="none"` so their
  module props flow to the host (as `Grove.Application.Cms.Core.Targets` does for the
  platform); the host references only the parent.
- **Exception: one module, several features**, when the pieces truly ship together and the
  rest of Grove relies on them. The children become `[Feature]`s of the parent assembly, each
  with feature-gated startups; enabling, disabling and registry registration are unchanged,
  only removal from the shipped code is lost.
- Either way, registration into a seam's registry is done by the startups of *enabled
  features*, so how an assembly arrived never changes what a tenant gets.
- ☑ A test fails when a module assembly in the host's output is not in its module list:
  `ModuleListGuardTests` in `Grove.Tests.Standalone` (a host that references Grove's modules as
  projects) compares every output assembly carrying the module marker with the host's
  `ModuleName` list.
- A module client library (`*.BlazorWasm`) belongs to the longest module or feature id its name
  starts with, and is served while that module or feature is enabled
  (`Grove.Contents.PartLists.BlazorWasm` follows its feature, not all of Grove.Contents).

## The rules that keep it composable

1. **Dependencies run one way.** Application layer (Grove: content, Parties, Members,
   Workflows, UI and API) ← downstream modules (a line-of-business layer's registries and
   its domain modules); products sit beside them and reference Grove. A downstream module
   injects upward through a seam the upstream module declares (interfaces, registries, page
   regions, part attachment from the downstream module's own migration). Upstream code never
   names a downstream type.
2. **The consumer owns the interface.** A module that consumes tax resolution owns its
   resolver interface; a tax module implements it. Parties consumes party types, so
   `IPartyTypeProvider` is Parties'; a module with a party role implements it.
   **Nothing declares its own version of a core object.** A module with a new kind of
   party or workflow registers it with the registry that owns that object; the registry is
   where the shape, the menu, the pages and the cross-module lookups come from, once. The
   same holds for the surfaces a module serves (§ Modules declare, Grove.Infrastructure.Server registers and
   serves).
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

## Still to build

Parties, Members, Workflows and Money exist, and so does the member shell
([shells-and-themes.md](shells-and-themes.md) phases 1–2). This section is the checklist of
what remains for Grove to stand alone, in build order; done items stay as a pointer to where
the piece lives. The global store's host-call rough edge is in [globals.md](globals.md).

### Standing alone

- [x] **3. A party role of Grove's own.** Feature `Grove.Parties.SampleRole` (category Samples,
  off by default) registers the `sample-roles` party type, so the menu item,
  `/Parties/sample-roles`, the All Parties pane and the route gate exist with no host module.
  Browser checks that need it enable the feature in their fixture recipe.
- [ ] **4. No host references, and a publishing gate.** Grove must build, test and read without
  any host present.
  - [x] Grove's test solution lists no host project; workflow object constants name only
    Grove's own objects, and the copy-fields target falls back to the payload's
    `ContentItemId`; fixtures, comments and docs use neutral names (`approve-order`,
    `order.placed`).
  - [ ] A global list for item classification ships in Grove although a downstream module
    seeds and owns it; it moves to that module's global schema.
  - [x] **Publishing gate:** `PublishingGateTests` in `Grove.Tests` reads the forbidden names
    from `PUBLISHING_GATE_NAMES` (comma-separated) or the file named by
    `PUBLISHING_GATE_NAMES_FILE`, scans every file git tracks or would add (whole words,
    case-insensitive), and skips when no names are set, so Grove never contains the list.
  - [ ] **Before publishing code that moved in from a private host:** no secrets, tokens,
    connection strings or real credentials in the moved trees (`Data/`, recipes, test
    fixtures, `appsettings*`); test fixtures use invented people and organizations only;
    third-party attribution present (Elsa's MIT licence for the engine and the Studio fork,
    and the header of anything else adapted at file boundaries); the moved plans read
    without the host's repository; the README lists the modules.
- [x] **5. A host with no line-of-business modules.** `test/Grove.Tests.Standalone` builds a
  host the way a product does (`AddPlatformCms`, `AddHostDefaults`, `UsePlatform`) over Grove's
  application layer only, installs it fresh into a temp `Grove:DataRoot` from its own setup
  recipe, and checks that only Grove's features are on, the registries' defaults (party types,
  the `currency` source, exchange rates), the shipped workflow roles, gap-free numbering and
  that the site and login serve. Setup takes a few minutes. A host's content root needs a
  `wwwroot` (the resized-image cache) and the Web SDK (Grove.Infrastructure.Server maps the static-assets
  manifest).
- [x] **6. The generic pieces every Grove application needs live in Grove**, none referencing
  a host's types:
  - the `currency` option source (`CurrencyOptionSourceProvider`), the money wire DTO
    (`MoneyModel`) and the exchange-rate seam (`ExchangeRate`, `IExchangeRateProvider`,
    `IExchangeRateRegistry`, `ManualExchangeRateProvider` over `ExchangeRateSettings`), in
    `Grove.Data.Types.Money` ([types.md](types.md));
  - workflow browser checks against a Grove fixture: feature `Grove.Workflows.Samples`
    (category Samples, off by default; trigger `sample.content-published`, system flow
    `samples.content-published`, shipped draft `samples.template`) with
    `Grove.Parties.SampleRole`; the checks are in `src/Grove.Workflows/tests/playwright/checks/`
    and `buildGroveModuleChecks()`, enable first, tenant isolation last (written, not yet run);
  - workflow editor and viewer roles, created when the feature is installed
    (`WorkflowRolesFeatureEvents`);
  - host defaults, `ServerHostDefaults` in Grove.Infrastructure.Server: `services.AddHostDefaults()` (the
    `Grove:DataRoot` override of the shells data path: tenants, `tenants.json`, the global
    store) and `app.UseHostStaticFiles()` (`Cache-Control: no-cache`, revalidated by ETag);
  - kind-registry plumbing: `Grove.Data.Kinds` (`IKindDescriptor`, `KindCatalog<TKind>`: merge by
    key, first wins, ordered, `Find`, `ForContentType`, `RouteOf`), `KindPageSource<TKind>` in
    Grove.Infrastructure.Server.Abstractions (the section, its pages and their gates in the page registry), and
    `GroveKindBrowser<TKind>` / `GroveKindPage<TKind>` in the component library; a downstream
    registry passes its kinds endpoint, how a kind reads and its pane; a registry whose kinds
    are not pages uses the catalog only;
  - gap-free record numbering, `IRecordNumbering` in Grove.Infrastructure.Server (named sequences per tenant in
    one document, a number taken only when the record commits; `Format(prefix, n)`);
  - the content-part-list import, `IContentPartListImporter` in the `Grove.Contents.PartLists` feature (upsert
    by key, a tenant's label, position and hidden flag never overwritten, failing rows reported)
    and `POST api/grove/content-part-lists/{key}/import` (manage permission; refused under an
    edit lock);
  - shared dev tooling: `tools/dev-lib.sh` (`ensure_feed`, `pack_into_output`, `restore_host`,
    `stop_server`, `start_test_server` / `stop_test_server`, `wait_until_provisioned`,
    `check_solution_membership`, `is_legacy_script`, `run_host_suite`) and `runHostSuite()` in
    `tests/playwright/harness/run-suite.js`.
- [ ] **The access machinery, the query pipeline and the action pipelines** exist in their
  first form ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery), steps 1, 2 and 4). Left there:
  the registry migration, the generated surfaces, the read job, the connection system
  (the protocol modules, which Workflows' connectors move down into, [queries.md](queries.md#connections-the-protocol-modules-and-the-data-layer)),
  the retirements.

### Recorded for later

- [ ] **Organization ownership of records.** Applications that serve many organizations inside
  one tenant need records that belong to an organization and are visible only to its members,
  fail-closed (no organization binding means no records). Members already rules the scope; the
  generic facility — an owner-organization part attachable to any content type, its index
  column, a query scope the member portal applies — is designed when the first application
  asks for it.

## Open questions

- **Translations of Grove's own UI.** Hosts currently ship them in their own catalogs, so a
  second host gets an untranslated admin. Ship them with Grove, or keep translations the
  host's?
- **Component library plumbing.** Whether `Grove.Display.Components`' client plumbing (`[GroveShell]`,
  `[GroveEager]`, `GroveShellContext`, …) counts as components and keeps the prefix
  (§ Naming).
