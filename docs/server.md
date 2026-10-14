# Crest.Server: the server, and what moves out of it

Crest.Server is the single server clients connect to: the entry point, the hosting and the
routing. It takes the incoming request, hands it to Access (authentication, antiforgery,
authorization), answers it from the surfaces modules have registered, and connects outward
through the data layer and the protocol modules (`Crest.Http`, `Crest.Rest`, `Crest.Soap`,
`Crest.GraphQL`,
[queries.md › Connections](queries.md#connections-the-protocol-modules-and-the-data-layer)).
It owns the **API endpoint registry** and the **page registry**; modules declare their surfaces and register into them
([architecture.md › Modules declare, Crest.Server registers and serves](architecture.md#modules-declare-crestserver-registers-and-serves)).
It is just the server: it keeps no domain API, no database connection (`Crest.Data`,
`Crest.Data.YesSql`), no outgoing connection of its own (the protocol modules) and no search index (`Crest.Indexing`). Keeping it thin is what allows a
later proxy in front of many Crest instances. Its name becomes Crest.Core after the MVC
retirement ([MVC/api.md](MVC/api.md)).

**Today** Crest.Server still hosts most of the tenant APIs, because it was built as an
application layer over OrchardCore while OrchardCore could not be changed: each API wraps a
platform module (`api/crest/roles` wraps Crest.Roles, and so on). Since the hard fork there is
one tree, so each API moves to the module that declares the surface and registers through
`Crest.Server.Abstractions`. The API plan, with its seam, waves and hosting, is
[MVC/api.md](MVC/api.md) (siblings [MVC/views.md](MVC/views.md) and
[MVC/shape-engine.md](MVC/shape-engine.md)); this document is the inventory of what moves and
where.

Moves are made by hand, one area at a time, each compile-checked; scripts list the
dependency chain (who uses a type, what a file needs) and nothing else. The rule for a file:
it goes where the surface it serves is declared. A controller, its view models and the helper
services only it uses move together. Tests move with their subject.

## Inventory and homes

Counts are source files. "Also used by" lists modules that reference the type today
(ContentPartLists, Parties, Workflows, Money, Regions and Members reference Crest.Server).

### 1. APIs and services → the declaring module

| Area (files) | Home | Notes |
| --- | --- | --- |
| AdminMenusController, AdminMenuLayoutExportController, AdminMenuLayoutService, AdminMenuLayoutInvalidation, AdminMenuBuilder, MenuCaptionResolver, MenuPlacementService, ProfileMenuService, ProviderMenuSync{Coordinator,Gate,Service,TenantEvents}, Recipes/AdminMenuLayoutStep, AdminMenusViewModels (15) | **Crest.AdminMenu** | The tenant layer of the admin menu. The provider sync retires with the page registry ([admin-menu.md](admin-menu.md) › The page registry). |
| NavigationController, PrimaryNavMenuSettingsStore, Navigation/CrestAdminMenu (name kept: clashes with the per-module AdminMenu classes), NavigationViewModels (4) | **Crest.Navigation** | NavigationItem view model is also used by Parties. |
| FeaturesController, FeaturesViewModels (2) | **Crest.Features** | Feature view model also used by Parties, Workflows, Money, Regions, Members. |
| IconsController, IconProvidersController, TenantIconsController, IconController, IconProviderSettingsStore, IconSourceStore, IconPermissions, TenantMediaIconsStartup (8) | **Crest.Icons** | |
| IconifyServerController, IconifyLocalMirrorPathProvider, IconifyCacheBackgroundTask (3) | **Crest.Iconify** | |
| IndexesController, IndexesViewModels (2) | **Crest.Indexing** | |
| LocalizationController, PoTranslationLookup, LocalizationViewModels (3) | **Crest.Localization** | |
| TranslationsController, TranslationsViewModels (2) | **Crest.DataLocalization** | |
| UsersController, LoginSettingsController, AuthController, LoginService, UserMenuPreferencesService, UserOptionSourceProvider, Users/LoginSettings/Auth view models (9) | **Crest.Users** (an Access module) | Sign-in, account, two-factor and login settings are Access's. LoginService is also used by Members; the login page plan ([branding.md](branding.md)) makes it a workflow action later. |
| AccessModelController | **Crest.Access** | Permission sets, groups, overrides. |
| MediaController, MediaOptionsController, MediaProfilesController, three view-model files (6) | **Crest.Media** | |
| QueriesController, QueriesViewModels (2) | **Crest.Queries** | The query system ([queries.md](queries.md)) replaces this endpoint later; move as is. |
| RecipesController, RecipesViewModels (2) | **Crest.Recipes** | |
| RolesController, RolesViewModels (2) | **Crest.Roles** | |
| SecurityHeadersController, SecurityHeadersViewModels (2) | **Crest.Security** | |
| SiteController, SiteViewModels (2) | **Crest.Settings** | Site name and home route; branding joins it ([branding.md](branding.md)). |
| AdminSettingsController, AdminSettingsNormalizer, TitleBarSettingsController, TitleBarSettingsStore, ThemeController, their view models (8) | **Crest.Admin** | Admin chrome preferences: title bar, theme choice, admin settings. |
| StandardMenusController, StandardMenusViewModels (2) | **Crest.Menu** | |
| TemplatesController, TemplatesViewModels (2) | **Crest.Templates** | |
| TenantsController, TenantsViewModels (2) | **Crest.Tenants** | Tenant view model also used by ContentPartLists, Parties, Workflows, Regions. |
| ThemesController, ThemesViewModels, ThemeContracts/{IShellContractProvider, MemberThemeService, ShellCompatibilityService, ThemeBuckets}, LegacyFrameThemeSelector, Themes/Crest.LegacyFrame (9) | **Crest.Themes** | Shell buckets and compatibility are theme concerns ([shells-and-themes.md](shells-and-themes.md)). |
| ContentItemsController, ContentItemsViewModels (2) | **Crest.Contents** | ContentItem view model is used by five modules. |
| ContentTypesController, ContentTypesViewModels, ContentGroups/* (5), FieldVisibilityController, FieldVisibilityEnforcer, FieldVisibilityRules, CrestFieldVisibilitySettings, DefinitionLockGuard, CrestDefinitionLockSettings, DefinitionLockExceptionFilter, LockedContentDefinitionServices, DefinitionLocksStartup, ContentDefinitionPermissions, ContentTypesMenuNavigationProvider, CrestContentTypeMenuSettings (19) | **Crest.ContentTypes** | Definition locks, field visibility and content groups all act on type definitions. ContentType and ContentPart view models are used by five modules. |
| ContentPartListPermissions (1) | **Crest.ContentPartLists** | The permission is parked in Crest.Server. |

Likewise `DebugSettingsController` → **Crest.Settings** (it owns `DebugSettings`) and the rest of
[MVC/api.md](MVC/api.md)'s Crest.Server rows; that table is the complete list.

### 2. Access → Crest.Access

ShellSelectionMiddleware (the gate), RouteGateMatcherPolicy, PermissionInvalidation,
RequestAccess's decision side (7). The route permission table is already in Access
(`IRoutePermissionProvider`, `IRouteAuthorization`). This is the "one gate" step in
[access.md](access.md) › The system; the move and that step are the same work. `IRequestAccess`, the
helper every API uses, goes to Crest.Server.Abstractions.

### 3. The option picker field family (23): home open

Fields/OptionPickerField, Settings/OptionPickerFieldSettings, Indexing/OptionPickerFieldIndex
and its migrations, Models/OptionSourceKeys, IOptionSourceProvider, the four option
source providers (content item, culture, time zone, user), Option{DependentFilterTranslator,
FilterMatcher, FilterResolver, ParentValueResolver, PickerFieldKeyResolver, RowSorter,
SourceReferenceGuard}, OptionSourcesController, OptionPickerAttachmentsController, four test
files. Used by ContentPartLists, Parties and Workflows. Its API
(`api/crest/option-sources`, `option-picker/attachment`) goes with it and registers through the
seam. Candidates:

- **Crest.ContentFields**, beside the platform's other fields: one fields module.
- **A new Crest.OptionPicker module**, since it carries its own index, settings, source
  providers and API, and other modules bind to it.

### 4. Assignment and organization parts

Assignments are retired (per-item grants are Sharing's; a rep's customers are a CRM person
role plus associations): `CrestAssignmentPart`, its index, service, scoped types, scope
provider and tests are deleted. Organization ownership on the row is **Crest.Parties**'
(`Crest.Parties.Organizations`): `CrestOrganizationPart`, its index and migration,
`OrganizationScopedTypes` and `OrganizationScopeProvider` ([access.md](access.md) › Members).

### 5. Stays: the host

Components/App.razor and imports, Endpoints/BlazorFrameworkScriptEndpoints,
Extensions/BlazorHosting, Routing/* (the route-component table, scanners, providers,
member options, theme owner metadata), the component shape-binding resolver, the display
endpoints and builders (Display/: the site page, the editor tree, the response status),
RoutingController, AppController and AppViewModels (the bootstrap aggregate), ServerApiClient,
UnsupportedJSRuntime, ShellAssemblies, DesignSystemStartup, Manifest and Startup (about 35),
plus the API hosting plumbing [MVC/api.md](MVC/api.md) § Placement lists. This is the Blazor
host that [blazor-display.md](blazor-display.md) describes. `AntiforgeryController` goes to
Access.

## Consequences

- ContentPartLists, Workflows, Global, Money and Regions declare a dependency on the feature
  `Crest` today; after the moves each declares the module it actually uses.
- The six project references to Crest.Server become references to Crest.Server.Abstractions
  and to the modules that now hold the types (mostly the content view models, the option
  picker, the route permission provider, the login service).
- Every moved API keeps its route; the API surface does not change in this work.
- Each step: move, repoint references, compile all three solutions, regenerate the graph.

## Order

Smallest and cleanest first, so the host shrinks visibly and the pattern is proven: Features,
Roles, Recipes, Queries, Templates, Tenants, Security, Menu, Media, Indexing; then Users,
Localization and DataLocalization, AdminMenu and Navigation, Settings and Admin, Themes,
Contents and ContentTypes, ContentPartLists; then Access (with the one-gate step); the option
picker once its home is chosen. [MVC/api.md](MVC/api.md) § Waves schedules the same moves
against the seam.

## Decisions

- **S1.** Each API goes to the module that declares the surface and registers in Crest.Server's
  API registry (§ 1).
- **S2.** Option picker home: open (§ 3).
- **S3.** `CrestAssignmentPart` is retired; `CrestOrganizationPart` and the organization scope
  are Crest.Parties' (§ 4).
- **S4.** Whether the host keeps the feature id `Crest` as its base feature: open.
