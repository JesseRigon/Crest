# Crest architecture

Crest is a platform and the application layer built into it. This document is its shape: what it holds, the
modules that make it up, who signs in to it, and the rules that keep downstream modules plugging
into it rather than it reaching for them. The global store, the geographic tree and money have
their own documents: [globals.md](globals.md), [regions.md](regions.md),
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
| `Crest.Access` ([access.md](access.md)) | The one access machinery every read and write passes through ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)): the caller built per request from the authenticated identity and server-held state (roles, permissions, organization, class ceiling) under a per-tenant permission version; one decision behind the platform's authorization service; scope providers per table compiled into every list, query and picker; the audit of decisions; the system caller for background work. Contracts in `Crest.Access.Abstractions`, below `Crest.Data`. One calculation per request, handed down the request's chain on the server, never across requests. |
| `Crest.Queries` | The read side: the structured query model (a step list compiled through YesSql into parameterised SQL with the caller's scope conjoined for every table), the SQL source for external databases with its scope rewriter, the `System` source (`item`, `user`, `site`) that display slots bind, query descriptors and the catalog the workflow engine's registry reads. Every run is paged, cancellable, typed and caller-scoped. The connection system (external systems by protocol) is planned here ([queries.md](queries.md)). |
| `Crest.Workflows` | The action side: a workflow service built on a vendored fork of Elsa 3 (MIT). Its registry (activities, triggers, hook slots, flows, and queries as `RunQuery` activities) is the operation registry; units of work (one burst, one session, one transaction, with inline hooks and durable background calls); the access gate as engine middleware with the actor rule (a run acts as the caller who started it, or as the system when published so); connectors with API-key, bearer, basic and OAuth2 authentication and OpenAPI import; approvals; field dependencies; ownership tiers and per-flow access; the platform modules' activities and triggers (the stock OrchardCore workflow module is gone); the designer bridge. |
| `Crest.Money` | The money type and the `PriceField` a tenant puts on any content type: `Amount` bound to an `ICurrency`, ISO 4217 metadata from the global store so minor units are never a per-tenant guess, and a default currency. Adapted from OrchardCore.Commerce (MIT). See [money.md](money.md). |
| `Crest.Kinds` | The kind-registry contract, server-free: `IKindDescriptor` and `KindCatalog<TKind>` (merge what the enabled features register by key, order it, find by key or content type). Party types are one registry on it; a downstream module's own registries (asset kinds, transaction kinds) are others. `KindAdminMenu<TKind>` in Crest.Navigation builds a registry's admin menu from it. |

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
`Crest.Workflows.Platform` module was merged into `Crest.Workflows` as part of the rename and
then deleted (2026-10-10, its activities ported to engine activities in their modules), and
the four sample site themes (TheTheme, TheBlogTheme, TheAgencyTheme, TheComingSoonTheme) are
pruned first. Neither the platform nor Crest.Server is named just `Crest`: the
platform's implementation library is `Crest.Infrastructure` (the former `Crest.Core` merged into it 2026-10-10, since Crest is one system with no framework-versus-CMS split), its contracts are `Crest.Infrastructure.Abstractions` (the shell host, modules, extensions, caching, locking, background tasks and options-invalidation contracts moved there from `Crest.Abstractions` the same day; `Crest.Abstractions` keeps only primitives: JSON, routing, localization, setup and helpers; the document-manager contracts sit in `Crest.Data.Abstractions`, which depends on Infrastructure.Abstractions and not the reverse). A module is at most two projects, `Crest.X` and `Crest.X.Abstractions`: the inherited `Crest.X.Core` libraries were folded into their modules on 2026-10-10 (`tools/fold-project.py`), with `Crest.Contents.Core` becoming the new `Crest.Contents.Abstractions` (the GraphQL library needs its contracts and the Contents module depends on that library), and `Crest.Mvc.Core` renamed `Crest.Mvc`. The contracts those libraries held (interfaces, permissions, constants, options, models, settings, fields, indexes) were then extracted into each module's `.Abstractions` project with `tools/extract-abstractions.py`, creating the project where none existed (Autoroute, AzureAI, ContentFields, Elasticsearch, Features, Flows, Html, Lucene, Navigation, OpenApi, OpenId, RateLimits, ReCaptcha, Settings, Taxonomies); shared implementation stays in the module and consumers reference the module. Sub-module names such as `Crest.Search.Elasticsearch` stay. Feature ids such as `Crest.Roles.Core` are tenant data and keep their names. Crest.Server's assembly and package become
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
    ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery), ruling 2026-10-10).
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
`Crest.Data.Global`, `Crest.ContentPartLists`. Namespaces, assembly names, feature ids,
manifests, permission names, option-set keys (`parties.*` — already unbranded), route
constants and test names follow.

**"Crest" appears only in module naming (ruling 2026-10-06).** Modules, assemblies and
packages carry the `Crest.` prefix; nothing inside them does — not permission names,
types, functions, settings keys or other internal names (`ManageConnections`, not
`ManageCrestWorkflowConnections`). The project must be renameable by renaming its modules
alone. The same rule holds for downstream projects. OrchardCore's own names are not ours to
change.

**Existing names that carry "Crest" (ruling 2026-10-06, X3).** The rule is about services:
the `Crest…` service, controller, claim, settings-key and permission names are renamed, so
a module rebrand is a module rename. The UI component library (`CrestButton`,
`CrestDataGrid`, …) keeps its prefix for conflict resolution with HTML and Blazor names.
The one exception on the service side: where a Crest service would clash with a platform
service of the same name, the Crest one keeps the prefix. Applied to Crest.Workflows on
2026-10-10 (26 identifiers; stored definitions using the old trigger type id
`Crest.Workflows.CrestTrigger` need re-creating). Applied to the rest of `src/` on
2026-10-13, Crest.Server included, ahead of its extraction ([server.md](server.md)): about 200
types and members lose the prefix (`ICrestGlobalStore` → `IGlobalStore`,
`CrestContentPartListService` → `ContentPartListService`, `CrestAuthController` →
`AuthController`, `CrestWebAssemblyHost` → `ClientWebAssemblyHost`, the generated
`CrestLazyModulesManifest` → `LazyModulesManifest`, `[CrestBlazorComponent]` →
`[BlazorComponent]`, the permission `ManageCrestGlobalReferenceData` →
`ManageGlobalReferenceData` — re-grant it on dev tenants). Routes, JSON property names and
recipe step names did not change. Names that stay, and why:

- **Stored by type name** (renaming would orphan data): the content parts `CrestOptionPart`,
  `CrestContentPartListPart`, `CrestRegionalProfilePart`, `CrestRegionalProfileReferencePart`,
  `CrestAssignmentPart`, `CrestOrganizationPart`, `CrestBlazorComponentPart`; the indexes
  `CrestAssignmentIndex`, `CrestOrganizationIndex`; the migrations (the migration record keys
  on the class name) `CrestContentPartListMigrations`, `CrestAssignmentIndexMigrations`,
  `CrestOrganizationIndexMigrations`; the documents `CrestAdminMenuLayoutDocument`,
  `CrestContentGroupsDocument`, `CrestMenuPlacementDocument`, `CrestProviderMenuSyncDocument`,
  `CrestGlobalSchemaState`; the definition and entity settings `CrestOptionFieldSettings`,
  `CrestDefinitionLockSettings`, `CrestFieldVisibilitySettings`, `CrestContentTypeMenuSettings`,
  `CrestContentGroupsSettings`, `CrestLocalizationSettings`, `CrestMemberThemeSettings`,
  `CrestUserMenuPreferences`, `CrestMemberInfo`; and the site-settings keys `CrestTitleBar`,
  `CrestIconProviders`, `CrestAdminPrimaryNavMenu`. They go together at the first tenant reset
  that allows it, or with an upgrade step after the first public release.
- **Would clash with a platform type of the same name** (the clash exception): the admin
  API view models and their client mirrors that duplicate a module's own model
  (`CrestUser`, `CrestTenant`, `CrestQuery`, `CrestLoginSettings`, `CrestTemplatePreview`,
  `CrestDeploymentPlan`, `CrestLayer`, `CrestMediaOptions`, the icon catalog records and about
  25 more), `CrestMediaProfilesController`, `CrestAdminMenu`, `CrestAntiforgeryToken`. Each
  folds into its module's own model when Crest.Server's wrappers move ([server.md](server.md)).
- **The component library**, `Crest.Components` whole, including its client plumbing
  (`[CrestShell]`, `[CrestEager]`, `[CrestJSComponent]`, `CrestShellContext`,
  `CrestLateServiceProviders`, `CrestApiLocalizer`, the files and sharing clients) — open
  whether that plumbing counts as components.
- **Not yet:** the Workflows permission names (`ViewCrestWorkflows` and siblings; the
  connection one becomes `ManageConnections` with the connection system, [queries.md](queries.md)),
  the client API root property `Api.Crest`, and `IsCrestBlazor`/`IsCrestBlazorTheme`
  (a JSON property on the themes API).

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
- Nothing reaches data except through the access machinery: a caller, a decision and a
  scope for every read and write, in every surface ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)).
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

The global store's host-call rough edge is in [globals.md](globals.md).

### Standing alone

- [x] **3. Add a party role of Crest's own.** A party role of Crest's own (or a test fixture
  that registers one), so the generic role pages can be exercised without a host's modules.
  *What landed (2026-10-13):* the fixture route - feature `Crest.Parties.SampleRole`
  (category Samples, off by default) composes a `SampleRole` role type with
  `CreatePartyRoleDefinitionsAsync` and registers it as the `sample-roles` party type, so the
  menu item, `/Parties/sample-roles`, the All Parties pane and the route gate exist with no host
  module. Browser checks that want it enable the feature in their fixture recipe.
- [ ] **4. Remove the host references below, and add the publishing gate check.** Crest must
  build, test and read without any host present.
  - [x] `Crest.Tests.slnx` lists a host's projects. Root cause:
    `Crest.AdminTheme.Client.csproj` globs `..\..\..\**\blazor-wasm\*.csproj`, reaching out
    of the repository. Replace the glob with an item the host supplies.
  - [x] Workflow object constants and a payload fallback key name a host's records; object
    names are free strings, and the subject key becomes a generic `ContentItemId`.
  - [x] Test fixtures and code comments use a host's flow keys and vocabulary; reword them
    generically.
  - [ ] A global list for item classification ships in Crest although a downstream module
    seeds and owns it; it moves to that module's global schema.
  - [x] Two code mentions name a product (the member home page, the members portal check).
  - [x] **Publishing gate:** no host or product name anywhere in the repository, checked
    automatically in Crest's test run (the list of names comes from configuration outside
    the repository, so Crest never contains them).
  *What landed (2026-10-13):* the glob was already gone; the slnx's leftover host entries are
  removed. `WorkflowsConstants.Objects` keeps only Crest's own objects (a module names its
  own) and the copy-fields target falls back to the payload's `ContentItemId` only. Fixtures,
  comments, docs and the README use neutral names (`approve-order`, `order.placed`). The gate
  is `PublishingGateTests` in `Crest.Tests`: it reads the names from `PUBLISHING_GATE_NAMES`
  (comma-separated) or the file named by `PUBLISHING_GATE_NAMES_FILE`, scans every file git
  tracks or would add, whole words, case-insensitive, and skips when no names are set. Still
  open: the item-classification global list (it moves with the host's open ruling on that
  list) and the pre-publish secrets/attribution review.
  - [ ] **Before publishing code that moved in from a private host:** no secrets, tokens,
    connection strings or real credentials in the moved trees (`Data/`, recipes, test
    fixtures, `appsettings*`); test fixtures use invented people and organizations only;
    third-party attribution present (Elsa's MIT licence for the engine and the Studio fork,
    and the header of anything else adapted at file boundaries); the moved plans read
    without the host's repository; the README lists the modules.
- [x] **5. Build and test a host that has no line-of-business modules** as a fresh install,
  to prove Crest stands alone. *Landed 2026-10-13:* `test/Crest.Tests.Standalone` (in
  Crest.Tests.slnx) builds a host the way a product does (AddPlatformCms, AddHostDefaults,
  UsePlatform) over Crest's application layer only, installs it fresh into a temp
  `Crest:DataRoot` from its own setup recipe, and checks the data root, that only Crest's
  features are on, the registries' defaults (party types, the `currency` source, exchange
  rates), the shipped workflow roles, gap-free numbering and that the site and login serve.
  Setup takes a few minutes. It found a dependency cycle on fresh install
  (`PersonalDriveUserEventHandler` → `UserManager` → user store → user event handlers),
  fixed by resolving the manager lazily. A host's content root needs a `wwwroot` (the
  resized-image cache) and the Web SDK (Crest.Server maps the static-assets manifest).
- [ ] **6. Move the generic pieces below into Crest, one at a time.** Hosts built on Crest
  currently carry their own copies of things every Crest application needs. Each belongs in
  Crest once a host-neutral version exists; none references a host's types.
  - [x] **A currency option source** over `ICurrencyProvider`, in `Crest.Money` (the content
    items doc already lists `currency` as a source a host supplies). *Landed 2026-10-13:*
    `CurrencyOptionSourceProvider`, registered by `Crest.Money` ([types.md](types.md)).
  - [x] **A money wire DTO** (value and currency) for `Amount` over the API, in `Crest.Money`.
    *Landed 2026-10-13:* `MoneyModel` in `Crest.Money.Domain`, with `From`/`ToAmount`.
  - [x] **An exchange-rate seam** (rate, policy, registry, manual rates) in `Crest.Money`, so a
    multi-currency application converts without a line-of-business module. *Landed
    2026-10-13:* `ExchangeRate`, `IExchangeRateProvider` (name, priority),
    `IExchangeRateRegistry`, `ManualExchangeRateProvider` over the tenant's
    `ExchangeRateSettings` (site settings: rates, failure behaviour); [types.md](types.md).
    Dev tenants that kept manual rates in a host's own settings re-enter them.
  - [ ] **Workflow browser checks** for connectors, ownership tiers, party triggers and engine
    tenant isolation, written against a Crest fixture (a content-published trigger, a
    shipped fixture flow, a fixture party role) instead of a host's documents. The tenant
    isolation check runs last.
  - [x] **Workflow editor and viewer role stereotypes** granting only the Crest workflow
    permissions, so every host can run the ownership checks. *Landed 2026-10-13:* the
    stereotypes were already Crest's; the roles themselves are now created when the feature
    is installed (`WorkflowRolesFeatureEvents`), so no host recipe has to ship them.
  - [x] **Host defaults:** the data-root shell-options override and the no-cache static files
    fix, as a Crest server host extension (configuration key `Crest:DataRoot`). *Landed
    2026-10-13:* `ServerHostDefaults` in Crest.Server: `services.AddHostDefaults()` (the
    `Crest:DataRoot` override of the shells data path: tenants, `tenants.json`, the global
    store) and `app.UseHostStaticFiles()` (`Cache-Control: no-cache`, revalidated by ETag).
  - [ ] **Kind-registry plumbing:** the catalog, menu, route gates, "All" page and per-kind pane
    that `Crest.Parties` implements, as a generic `KindCatalog<T>` and kind pane hosts reuse
    for their own registries. *Partly landed 2026-10-13:* `Crest.Kinds` (server-free:
    `IKindDescriptor`, `KindCatalog<TKind>` — merge by key, first wins, ordered, `Find`,
    `ForContentType`, `RouteOf`) and `KindAdminMenu<TKind>` in Crest.Navigation (the section,
    its "All" child and one child per kind). Parties' catalog and menu are built on them.
    Still open: the route gates (two lines per registry, not worth a base yet), the "All"
    page and the per-kind pane (Blazor, in the component library).
  - [x] **Gap-free record numbering** as a Crest server service. *Landed 2026-10-13:*
    `IRecordNumbering` in Crest.Server (named sequences per tenant in one document, a number
    taken only when the record commits; `IRecordNumbering.Format(prefix, n)`). A host that
    kept its own counters starts its sequences at 1 again: reset dev tenants.
  - [ ] **A content-part-list import** that upserts options by key and never overwrites a
    tenant's label.
  - [x] **Shared dev tooling:** feed pack and check, stopping the local server, the throwaway
    test server, the solution-membership gate, the legacy-script filter, provisioning
    verification and the suite entry point, as `tools/dev-lib.sh` and a `runHostSuite()`
    harness helper. Hosts pass their solution, features, ports and credentials. *Landed
    2026-10-13:* `tools/dev-lib.sh` (sourced; functions only: `ensure_feed`,
    `pack_into_output`, `restore_host`, `stop_server`, `start_test_server` /
    `stop_test_server` on `Crest:DataRoot`, `wait_until_provisioned`,
    `check_solution_membership`, `is_legacy_script`, `run_host_suite`) and `runHostSuite()`
    in `tests/playwright/harness/run-suite.js` (BASE_URL, CHECK_FILTER, OUTPUT_ROOT, VERBOSE,
    login, summary, exit code).

- [x] **The access machinery, the query pipeline and the action pipelines** landed in their
  first form on 2026-10-10 ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery), steps 1, 2 and 4). Open there:
  the registry migration, the generated surfaces, the read job, the connection system
  (Workflows' connectors move down into it, [queries.md](queries.md)), the retirements.

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

