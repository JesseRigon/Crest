# Grove.Server: the server, and what moves out of it

Grove.Server is the single server clients connect to: the entry point, the hosting and the
routing. It takes the incoming request, hands it to Access (authentication, antiforgery,
authorization), answers it from the surfaces modules have registered, and connects outward
through the data layer and the protocol modules (`Grove.Http`, `Grove.Rest`, `Grove.Soap`,
`Grove.GraphQL`,
[queries.md › Connections](queries.md#connections-the-protocol-modules-and-the-data-layer)).
It owns the **API endpoint registry** and the **page registry**; modules declare their surfaces and register into them
([architecture.md › Modules declare, Grove.Server registers and serves](architecture.md#modules-declare-groveserver-registers-and-serves)).
It is just the server: it keeps no domain API, no database connection (`Grove.Data`,
`Grove.Data.YesSql`), no outgoing connection of its own (the protocol modules) and no search index (`Grove.Indexing`). Keeping it thin is what allows a
later proxy in front of many Grove instances. Its name becomes Grove.Core after the MVC
retirement ([MVC/api.md](MVC/api.md)).

Each module's API is declared by the module and registered through `Grove.Server.Abstractions`
(`IApiContributor`, [MVC/api.md › The seam](MVC/api.md#the-seam)); Grove.Server maps the
contributors registered in the tenant into route groups and serves them. The API plan, with its
seam, waves and hosting, is [MVC/api.md](MVC/api.md) (siblings [MVC/views.md](MVC/views.md) and
[MVC/shape-engine.md](MVC/shape-engine.md)); this document is the inventory of what each module
holds and of what is still in Grove.Server.

## Inventory and homes

Counts are source files. "Also used by" lists modules that reference the type today
(ContentPartLists, Parties, Workflows, Money, Regions and Members reference Grove.Server).

### 1. APIs and services → the declaring module

Every area below is a route group (minimal API, `Api/*Api.cs`) in its module, registered from
the module's startup; its models moved with it (`<Module>.Api` namespaces). Routes are unchanged.
Handlers name their binding sources and request DTOs carry the validation ruleset
([MVC/api.md › Hosting](MVC/api.md#hosting), [› Validation](MVC/api.md#validation)).

| Area | Module | Contributor(s) |
| --- | --- | --- |
| Roles list | **Grove.Roles** | `RolesApi` |
| Recipes | **Grove.Recipes** | `RecipesApi` |
| Saved queries, the SQL runner | **Grove.Queries** | `QueriesApi` |
| Templates, overrides, designed pages, the components list | **Grove.Templates** | `TemplatesApi`, `ComponentOverridesApi`, `DesignedPagesApi`, `ComponentsApi` |
| Tenants (`api/grove/tenants`) | **Grove.Tenants** | `TenantsApi` |
| Security headers | **Grove.Security** | `SecurityHeadersApi` |
| Standard menus | **Grove.Menu** | `StandardMenusApi` |
| Media folders and files, options, profiles, the media hub | **Grove.Media** | `MediaFilesApi`, `MediaOptionsApi`, `MediaProfilesApi`, `MediaHubApi` |
| Indexes | **Grove.Indexing** | `IndexesApi` |
| Site settings, home route, the anonymous branding read, branding renditions, debugging; `SiteIdentity` | **Grove.Settings** | `SiteSettingsApi`, `BrandingSettingsApi`, `DebugSettingsApi` |
| Admin settings, title bar (`TitleBarSettingsStore`), the admin theme preference | **Grove.Admin** | `AdminSettingsApi`, `TitleBarSettingsApi`, `ThemeSettingsApi` |
| Deployment plans, steps, import, export | **Grove.Deployment** | `DeploymentApi` (the remote targets through `IRemoteDeploymentTargets`) |
| Sending a plan to a remote instance, remote instances and clients | **Grove.Deployment.Remote** | `RemoteDeploymentApi` |
| Custom settings | **Grove.CustomSettings** | `CustomSettingsApi` |
| Users, sign-in (`LoginService`), login settings, the user's own culture | **Grove.Users** (an Access module) | `UsersApi`, `AuthApi`, `LoginSettingsApi`, `UserCultureApi` |
| The access model, the antiforgery token, the permissions hub (`IPermissionInvalidator`) | **Grove.Access** | `AccessModelApi`, `AntiforgeryApi`, `PermissionHubApi` |
| Content definitions: types, parts, content groups, field visibility, definition locks | **Grove.ContentTypes** | `ContentDefinitionsApi`, `ContentTypesEditorApi`, `ContentGroupsApi`, `FieldVisibilityApi` |
| Content items; the content groups' services and pages | **Grove.Contents** | `ContentItemsApi` |
| Themes and the shell contracts (`ShellContracts/`) | **Grove.Themes** | `ThemesApi` |
| Features | **Grove.Features** | `FeaturesApi` |
| Localization settings, the admin client's strings, `PoTranslationLookup` | **Grove.Localization** | `LocalizationApi` (stored edits through `IClientStringOverrides`) |
| Translations | **Grove.DataLocalization** | `TranslationsApi` |

Still in Grove.Server, each for a stated reason:

- **Icons** (`IconsController`, `IconProvidersController`, `TenantIconsController`,
  `IconifyServerController`, their stores, the `Grove.Icons` features): an exception to fix
  later with a small server-side icons module. `Grove.Icons` and `Grove.Iconify` are libraries in
  the WebAssembly client's closure, so neither can host a server module.
- **The option picker's API** (`OptionSourcesController`, `OptionPickerAttachmentsController`)
  waits for its home (§ 3, S2), and `FieldVisibilityEnforcer` with it: it reads the option
  picker's parent values. Grove.ContentTypes declares `IFieldVisibilityEnforcer`; the host
  implements it.
- **The admin menu and navigation** (`AdminMenusController`, `AdminMenuLayoutExportController`,
  `NavigationController`, the menu services): the page registry's work
  ([admin-menu.md](admin-menu.md) › The page registry).
- `ContentPartListPermissions` belongs in **Grove.ContentPartLists**.

### 2. Access → Grove.Access

ShellSelectionMiddleware (the gate) and RouteGateMatcherPolicy. The page gate is already in
Access (`IRouteAuthorization`, over the page registry's entries), and so is permission
invalidation (`IPermissionInvalidator` in Grove.Access.Abstractions, the hub in Grove.Access).
This is the "one gate" step in [access.md](access.md) › The system; the move and that step are
the same work. `IRequestAccess`, the helper every API uses, is in Grove.Server.Abstractions.

### 3. The option picker field family (23): home open

Fields/OptionPickerField, Settings/OptionPickerFieldSettings, Indexing/OptionPickerFieldIndex
and its migrations, Models/OptionSourceKeys, IOptionSourceProvider, the four option
source providers (content item, culture, time zone, user), Option{DependentFilterTranslator,
FilterMatcher, FilterResolver, ParentValueResolver, PickerFieldKeyResolver, RowSorter,
SourceReferenceGuard}, OptionSourcesController, OptionPickerAttachmentsController, four test
files. Used by ContentPartLists, Parties and Workflows. Its API
(`api/grove/option-sources`, `option-picker/attachment`) goes with it and registers through the
seam. Candidates:

- **Grove.ContentFields**, beside the platform's other fields: one fields module.
- **A new Grove.OptionPicker module**, since it carries its own index, settings, source
  providers and API, and other modules bind to it.

### 4. Assignment and organization parts

Assignments are retired (per-item grants are Sharing's; a rep's customers are a CRM person
role plus associations): `GroveAssignmentPart`, its index, service, scoped types, scope
provider and tests are deleted. Organization ownership on the row is **Grove.Parties**'
(`Grove.Parties.Organizations`): `GroveOrganizationPart`, its index and migration,
`OrganizationScopedTypes` and `OrganizationScopeProvider` ([access.md](access.md) › Members).

### 5. Stays: the host

Components/App.razor and imports, Endpoints/BlazorFrameworkScriptEndpoints,
Extensions/BlazorHosting, Routing/* (the route-component table, scanners, providers,
member options, theme owner metadata), the component shape-binding resolver, the display
builders (Display/: the site page, the editor tree, the response status) and their endpoints
(`Api/DisplayApi.cs`: `site/page`, `pages/{id}/preview`, `content-items/{id}/editor`,
`custom-settings/{type}/editor`), RoutingController, AppController and AppViewModels (the
bootstrap aggregate), ServerApiClient, UnsupportedJSRuntime, ShellAssemblies,
DesignSystemStartup, Manifest and Startup, plus the API hosting plumbing in `Api/`
([MVC/api.md](MVC/api.md) § Placement). This is the Blazor host that
[blazor-display.md](blazor-display.md) describes.

## Consequences

- ContentPartLists, Workflows, Global, Money and Regions declare a dependency on the feature
  `Grove`; each declares the modules it actually uses as well (ContentPartLists:
  `Grove.ContentTypes` for the definition lock guard).
- The modules that register APIs reference Grove.Server.Abstractions. Members, Parties,
  ContentPartLists, Workflows, Money and Regions still reference Grove.Server for the option
  picker and the routing types; that reference becomes Grove.Server.Abstractions and the
  modules holding the types once the option picker has its home.
- Every moved API kept its route.
- Each step: move, repoint references, compile all three solutions, regenerate the graph.

## Order

What is left in Grove.Server's API, in order: the admin menu and navigation (with the page
registry), the option picker once its home is chosen (S2), the icons (a server-side icons
module), then Access's gate (the one-gate step). [MVC/api.md](MVC/api.md) § Waves schedules the
rest of the API work.

## Decisions

- **S1.** Each API goes to the module that declares the surface and registers in Grove.Server's
  API registry (§ 1).
- **S2.** Option picker home: open (§ 3).
- **S3.** `GroveAssignmentPart` is retired; `GroveOrganizationPart` and the organization scope
  are Grove.Parties' (§ 4).
- **S4.** Whether the host keeps the feature id `Grove` as its base feature: open.
