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

Each module's API is declared by the module and registered through `Crest.Server.Abstractions`
(`IApiContributor`, [MVC/api.md › The seam](MVC/api.md#the-seam)); Crest.Server maps the
contributors registered in the tenant into route groups and serves them. The API plan, with its
seam, waves and hosting, is [MVC/api.md](MVC/api.md) (siblings [MVC/views.md](MVC/views.md) and
[MVC/shape-engine.md](MVC/shape-engine.md)); this document is the inventory of what each module
holds and of what is still in Crest.Server.

## Inventory and homes

Counts are source files. "Also used by" lists modules that reference the type today
(ContentPartLists, Parties, Workflows, Money, Regions and Members reference Crest.Server).

### 1. APIs and services → the declaring module

Every area below is a route group (minimal API, `Api/*Api.cs`) in its module, registered from
the module's startup; its models moved with it (`<Module>.Api` namespaces). Routes are unchanged.
Handlers name their binding sources and request DTOs carry the validation ruleset
([MVC/api.md › Hosting](MVC/api.md#hosting), [› Validation](MVC/api.md#validation)).

| Area | Module | Contributor(s) |
| --- | --- | --- |
| Roles list | **Crest.Roles** | `RolesApi` |
| Recipes | **Crest.Recipes** | `RecipesApi` |
| Saved queries, the SQL runner | **Crest.Queries** | `QueriesApi` |
| Templates, overrides, designed pages, the components list | **Crest.Templates** | `TemplatesApi`, `ComponentOverridesApi`, `DesignedPagesApi`, `ComponentsApi` |
| Tenants (`api/crest/tenants`) | **Crest.Tenants** | `TenantsApi` |
| Security headers | **Crest.Security** | `SecurityHeadersApi` |
| Standard menus | **Crest.Menu** | `StandardMenusApi` |
| Media folders and files, options, profiles, the media hub | **Crest.Media** | `MediaFilesApi`, `MediaOptionsApi`, `MediaProfilesApi`, `MediaHubApi` |
| Indexes | **Crest.Indexing** | `IndexesApi` |
| Site settings, home route, the anonymous branding read, branding renditions, debugging; `SiteIdentity` | **Crest.Settings** | `SiteSettingsApi`, `BrandingSettingsApi`, `DebugSettingsApi` |
| Admin settings, title bar (`TitleBarSettingsStore`), the admin theme preference | **Crest.Admin** | `AdminSettingsApi`, `TitleBarSettingsApi`, `ThemeSettingsApi` |
| Deployment plans, steps, import, export | **Crest.Deployment** | `DeploymentApi` (the remote targets through `IRemoteDeploymentTargets`) |
| Sending a plan to a remote instance, remote instances and clients | **Crest.Deployment.Remote** | `RemoteDeploymentApi` |
| Custom settings | **Crest.CustomSettings** | `CustomSettingsApi` |
| Users, sign-in (`LoginService`), login settings, the user's own culture | **Crest.Users** (an Access module) | `UsersApi`, `AuthApi`, `LoginSettingsApi`, `UserCultureApi` |
| The access model, the antiforgery token, the permissions hub (`IPermissionInvalidator`) | **Crest.Access** | `AccessModelApi`, `AntiforgeryApi`, `PermissionHubApi` |
| Content definitions: types, parts, content groups, field visibility, definition locks | **Crest.ContentTypes** | `ContentDefinitionsApi`, `ContentTypesEditorApi`, `ContentGroupsApi`, `FieldVisibilityApi` |
| Content items; the content groups' services and pages | **Crest.Contents** | `ContentItemsApi` |
| Themes and the shell contracts (`ShellContracts/`) | **Crest.Themes** | `ThemesApi` |
| Features | **Crest.Features** | `FeaturesApi` |
| Localization settings, the admin client's strings, `PoTranslationLookup` | **Crest.Localization** | `LocalizationApi` (stored edits through `IClientStringOverrides`) |
| Translations | **Crest.DataLocalization** | `TranslationsApi` |

Still in Crest.Server, each for a stated reason:

- **Icons** (`IconsController`, `IconProvidersController`, `TenantIconsController`,
  `IconifyServerController`, their stores, the `Crest.Icons` features): an exception to fix
  later with a small server-side icons module. `Crest.Icons` and `Crest.Iconify` are libraries in
  the WebAssembly client's closure, so neither can host a server module.
- **The option picker's API** (`OptionSourcesController`, `OptionPickerAttachmentsController`)
  waits for its home (§ 3, S2), and `FieldVisibilityEnforcer` with it: it reads the option
  picker's parent values. Crest.ContentTypes declares `IFieldVisibilityEnforcer`; the host
  implements it.
- **The admin menu and navigation** (`AdminMenusController`, `AdminMenuLayoutExportController`,
  `NavigationController`, the menu services): the page registry's work
  ([admin-menu.md](admin-menu.md) › The page registry).
- `ContentPartListPermissions` belongs in **Crest.ContentPartLists**.

### 2. Access → Crest.Access

ShellSelectionMiddleware (the gate) and RouteGateMatcherPolicy. The page gate is already in
Access (`IRouteAuthorization`, over the page registry's entries), and so is permission
invalidation (`IPermissionInvalidator` in Crest.Access.Abstractions, the hub in Crest.Access).
This is the "one gate" step in [access.md](access.md) › The system; the move and that step are
the same work. `IRequestAccess`, the helper every API uses, is in Crest.Server.Abstractions.

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
builders (Display/: the site page, the editor tree, the response status) and their endpoints
(`Api/DisplayApi.cs`: `site/page`, `pages/{id}/preview`, `content-items/{id}/editor`,
`custom-settings/{type}/editor`), RoutingController, AppController and AppViewModels (the
bootstrap aggregate), ServerApiClient, UnsupportedJSRuntime, ShellAssemblies,
DesignSystemStartup, Manifest and Startup, plus the API hosting plumbing in `Api/`
([MVC/api.md](MVC/api.md) § Placement). This is the Blazor host that
[blazor-display.md](blazor-display.md) describes.

## Consequences

- ContentPartLists, Workflows, Global, Money and Regions declare a dependency on the feature
  `Crest`; each declares the modules it actually uses as well (ContentPartLists:
  `Crest.ContentTypes` for the definition lock guard).
- The modules that register APIs reference Crest.Server.Abstractions. Members, Parties,
  ContentPartLists, Workflows, Money and Regions still reference Crest.Server for the option
  picker and the routing types; that reference becomes Crest.Server.Abstractions and the
  modules holding the types once the option picker has its home.
- Every moved API kept its route.
- Each step: move, repoint references, compile all three solutions, regenerate the graph.

## Order

What is left in Crest.Server's API, in order: the admin menu and navigation (with the page
registry), the option picker once its home is chosen (S2), the icons (a server-side icons
module), then Access's gate (the one-gate step). [MVC/api.md](MVC/api.md) § Waves schedules the
rest of the API work.

## Decisions

- **S1.** Each API goes to the module that declares the surface and registers in Crest.Server's
  API registry (§ 1).
- **S2.** Option picker home: open (§ 3).
- **S3.** `CrestAssignmentPart` is retired; `CrestOrganizationPart` and the organization scope
  are Crest.Parties' (§ 4).
- **S4.** Whether the host keeps the feature id `Crest` as its base feature: open.
