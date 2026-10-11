# Crest.Server — dissolving the pre-merge app layer into the modules

**Status: planned 2026-10-10, nothing moved yet.** Crest.Server was built as an application
layer over OrchardCore while OrchardCore was a dependency that could not be changed. Every
tenant API it exposes wraps a platform module (`api/crest/roles` wraps Crest.Roles, and so
on), because the only place Crest could put code was beside the platform. Since the hard
fork there is one tree, so each wrapper belongs in the module it wraps, and Crest.Server
keeps only what is genuinely the host: the Blazor hosting, the route table, the bootstrap.

Moves are made by hand, one area at a time, each compile-checked; scripts list the
dependency chain (who uses a type, what a file needs) and nothing else. The rule for a file:
it goes where the service it calls lives. A controller, its view models and the helper
services only it uses move together. Tests move with their subject.

## Inventory and proposed homes

Counts are source files. "Also used by" lists modules that reference the type today
(ContentPartLists, Parties, Workflows, Money, Regions and Members reference Crest.Server).

### 1. Wrappers over one platform module → that module

| Area (files) | Proposed home | Notes |
| --- | --- | --- |
| AdminMenusController, CrestAdminMenuLayoutExportController, CrestAdminMenuLayoutService, CrestAdminMenuLayoutInvalidation, CrestAdminMenuBuilder, CrestMenuCaptionResolver, CrestMenuPlacementService, CrestProfileMenuService, CrestProviderMenuSync{Coordinator,Gate,Service,TenantEvents}, Recipes/CrestAdminMenuLayoutStep, AdminMenusViewModels (15) | **Crest.AdminMenu** | The tenant admin-menu layout and provider sync are the admin-menu feature. |
| NavigationController, CrestPrimaryNavMenuSettingsStore, Navigation/CrestAdminMenu, NavigationViewModels (4) | **Crest.Navigation** | NavigationItem view model is also used by Parties. |
| FeaturesController, FeaturesViewModels (2) | **Crest.Features** | Feature view model also used by Parties, Workflows, Money, Regions, Members. |
| IconsController, IconProvidersController, TenantIconsController, CrestIconController, CrestIconProviderSettingsStore, CrestIconSourceStore, CrestIconPermissions, TenantMediaIconsStartup (8) | **Crest.Icons** | |
| IconifyServerController, CrestIconifyLocalMirrorPathProvider, IconifyCacheRefreshService (3) | **Crest.Iconify** | |
| IndexesController, IndexesViewModels (2) | **Crest.Indexing** | |
| LocalizationController, CrestPoTranslationLookup, LocalizationViewModels (3) | **Crest.Localization** | |
| TranslationsController, TranslationsViewModels (2) | **Crest.DataLocalization** | |
| UsersController, LoginSettingsController, CrestAuthController, CrestLoginService, CrestUserMenuPreferencesService, UserOptionSourceProvider, Users/LoginSettings/CrestAuth view models (9) | **Crest.Users** | CrestLoginService is also used by Members; the login page plan ([branding.md](branding.md)) makes it a workflow action later. |
| MediaController, MediaOptionsController, MediaProfilesController, three view-model files (6) | **Crest.Media** | |
| QueriesController, QueriesViewModels (2) | **Crest.Queries** | The query system ([queries.md](queries.md)) replaces this endpoint later; move as is. |
| RecipesController, RecipesViewModels (2) | **Crest.Recipes** | |
| RolesController, RolesViewModels (2) | **Crest.Roles** | |
| SecurityHeadersController, SecurityHeadersViewModels (2) | **Crest.Security** | |
| SiteController, SiteViewModels (2) | **Crest.Settings** | Site name and home route; branding joins it ([branding.md](branding.md)). |
| AdminSettingsController, CrestAdminSettingsNormalizer, TitleBarSettingsController, CrestTitleBarSettingsStore, CrestThemeController, their view models (8) | **Crest.Admin** | Admin chrome preferences: title bar, theme choice, admin settings. |
| StandardMenusController, StandardMenusViewModels (2) | **Crest.Menu** | |
| TemplatesController, TemplatesViewModels (2) | **Crest.Templates** | |
| TenantsController, TenantsViewModels (2) | **Crest.Tenants** | Tenant view model also used by ContentPartLists, Parties, Workflows, Regions. |
| ThemesController, ThemesViewModels, ThemeContracts/{IShellContractProvider, MemberThemeService, ShellCompatibilityService, ThemeBuckets}, LegacyFrameThemeSelector, Themes/Crest.LegacyFrame (9) | **Crest.Themes** | Shell buckets and compatibility are theme concerns ([shells-and-themes.md](shells-and-themes.md)). |
| ContentItemsController, ContentItemsViewModels (2) | **Crest.Contents** | ContentItem view model is used by five modules. |
| ContentTypesController, ContentTypesViewModels, ContentGroups/* (5), FieldVisibilityController, CrestFieldVisibilityEnforcer, CrestFieldVisibilityRules, CrestFieldVisibilitySettings, CrestDefinitionLockGuard, CrestDefinitionLockSettings, CrestDefinitionLockExceptionFilter, LockedContentDefinitionServices, DefinitionLocksStartup, CrestContentDefinitionPermissions, ContentTypesMenuNavigationProvider, CrestContentTypeMenuSettings (19) | **Crest.ContentTypes** | Definition locks, field visibility and content groups all act on type definitions. ContentType and ContentPart view models are used by five modules. |
| CrestContentPartListPermissions (1) | **Crest.ContentPartLists** | Already a module; the permission was parked here. |

### 2. Access → Crest.Access

BlazorAdminThemeMiddleware (the gate), RouteGateMatcherPolicy, CrestRouteAuthorizationService
with ICrestRoutePermissionProvider and CrestRoutePermission (used by Parties and Workflows),
CrestPermissionInvalidation, CrestRequestAccess (7). This is the "one gate" step in
[access.md](access.md) § 3; the move and that step are the same work.

### 3. Needs a ruling: the option picker field family (23)

Fields/OptionPickerField, Settings/OptionPickerFieldSettings, Indexing/OptionPickerFieldIndex
and its migrations, Models/CrestOptionSourceKeys, IOptionSourceProvider, the four option
source providers (content item, culture, time zone, user), Option{DependentFilterTranslator,
FilterMatcher, FilterResolver, ParentValueResolver, PickerFieldKeyResolver, RowSorter,
SourceReferenceGuard}, OptionSourcesController, OptionPickerAttachmentsController, four test
files. Used by ContentPartLists, Parties and Workflows.

- **Option A: Crest.ContentFields**, beside the platform's other fields. One fields module.
- **Option B: a new Crest.OptionPicker module**, since it carries its own index, settings,
  source providers and API, and other modules bind to it.

### 4. Needs a ruling: assignment and organization parts (7)

Models/CrestAssignmentPart, Models/CrestOrganizationPart, Indexing/CrestAssignmentIndex and
migrations, Indexing/CrestOrganizationIndex, CrestAssignmentService,
AssignmentScopeProvider, OrganizationScopeProvider, CrestAssignmentTests. These scope
content items to an organization and to assignees, which is the party model's territory.

- **Option A: Crest.Parties** (organizations are parties; [parties.md](parties.md)).
- **Option B: Crest.Members** (the scope providers decide member access today).

### 5. Stays: the host (what Crest.Server becomes)

Components/App.razor and imports, Endpoints/BlazorFrameworkScriptEndpoints,
Extensions/CrestBlazorHosting, Routing/* (the route-component table, scanners, providers,
member options, theme owner metadata), ICrestBlazorComponentRegistry and the assembly-scanning
registry, CrestBlazorComponentPart with its driver, migration, shape-binding resolver and
view model, CrestRoutingController, CrestAntiforgeryController, AppController and
AppViewModels (the bootstrap aggregate), CrestServerApiClient, UnsupportedJSRuntime,
ShellAssemblies, DesignSystemStartup, Manifest and Startup (about 35). This is the Blazor
host that [blazor-display.md](blazor-display.md) describes; its name is an X3 question
([architecture.md](architecture.md)).

## Consequences

- The feature id `Crest` stays on the host. ContentPartLists, Workflows, Global, Money and
  Regions declare a dependency on it today; after the moves each declares the module it
  actually uses.
- The six project references to Crest.Server become references to the modules that now
  hold the types (mostly the content view models, the option picker, the route permission
  provider, the login service).
- Every moved controller keeps its route; the API surface does not change in this work.
- Each step: move, repoint references, compile all three solutions, regenerate the graph.

## Order

Smallest and cleanest first, so the host shrinks visibly and the pattern is proven: Features,
Roles, Recipes, Queries, Templates, Tenants, Security, Menu, Media, Indexing; then Users,
Localization and DataLocalization, AdminMenu and Navigation, Settings and Admin, Themes,
Contents and ContentTypes, ContentPartLists; then Access (with the one-gate step); the two
rulings last.

## Decisions

- **S1. Wrappers go to the module they wrap** (2026-10-10, proposed): see § 1.
- **S2. Option picker home**: open (§ 3).
- **S3. Assignment and organization parts home**: open (§ 4).
- **S4. The host keeps the `Crest` feature id**: proposed.
