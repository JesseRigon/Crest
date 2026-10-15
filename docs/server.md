# Grove.Infrastructure.Server: the server, and what moves out of it

Grove.Infrastructure.Server is the single server clients connect to: the entry point, the hosting and the
routing. It takes the incoming request, hands it to Access (authentication, antiforgery,
authorization), answers it from the surfaces modules have registered, and connects outward
through the data layer and the protocol modules (`Grove.Http`, `Grove.Rest`, `Grove.Soap`,
`Grove.GraphQL`,
[queries.md › Connections](queries.md#connections-the-protocol-modules-and-the-data-layer)).
It owns the **API endpoint registry** and the **page registry**; modules declare their surfaces and register into them
([architecture.md › Modules declare, Grove.Infrastructure.Server registers and serves](architecture.md#modules-declare-groveserver-registers-and-serves)).
It is just the server: it keeps no domain API, no database connection (`Grove.Data`,
`Grove.Data.YesSql`), no outgoing connection of its own (the protocol modules) and no search index (`Grove.Data.Indexing`). Keeping it thin is what allows a
later proxy in front of many Grove instances. Its name becomes Grove.Core after the MVC
retirement ([MVC/api.md](MVC/api.md)).

Each module's API is declared by the module and registered through `Grove.Infrastructure.Server.Abstractions`
(`IApiContributor`, [MVC/api.md › The seam](MVC/api.md#the-seam)); Grove.Infrastructure.Server maps the
contributors registered in the tenant into route groups and serves them. The API plan, with its
seam, waves and hosting, is [MVC/api.md](MVC/api.md) (siblings [MVC/views.md](MVC/views.md) and
[MVC/shape-engine.md](MVC/shape-engine.md)); this document is the inventory of what each module
holds and of what is still in Grove.Infrastructure.Server.

## Inventory and homes

Counts are source files. "Also used by" lists modules that reference the type today
(Parties, Workflows, Money, Regions and Members reference Grove.Infrastructure.Server).

### 1. APIs and services → the declaring module

Every area below is a route group (minimal API, `Api/*Api.cs`) in its module, registered from
the module's startup; its models moved with it (`<Module>.Api` namespaces). Routes are unchanged.
Handlers name their binding sources and request DTOs carry the validation ruleset
([MVC/api.md › Hosting](MVC/api.md#hosting), [› Validation](MVC/api.md#validation)).

| Area | Module | Contributor(s) |
| --- | --- | --- |
| Roles list | **Grove.Access.Roles** | `RolesApi` |
| Recipes | **Grove.Infrastructure.Recipes** | `RecipesApi` |
| Saved queries, the SQL runner | **Grove.Data.Queries** | `QueriesApi` |
| Templates, overrides, designed pages, the components list | **Grove.Display.Templates** | `TemplatesApi`, `ComponentOverridesApi`, `DesignedPagesApi`, `ComponentsApi` |
| Tenants (`api/grove/tenants`) | **Grove.Infrastructure.Tenants** | `TenantsApi` |
| Security headers | **Grove.Security** | `SecurityHeadersApi` |
| Standard menus | **Grove.Contents.SiteMenu** | `StandardMenusApi` |
| Media folders and files, options, profiles, the media hub | **Grove.Media** | `MediaFilesApi`, `MediaOptionsApi`, `MediaProfilesApi`, `MediaHubApi` |
| Indexes | **Grove.Data.Indexing** | `IndexesApi` |
| Site settings, home route, the anonymous branding read, branding renditions; `SiteIdentity` | **Grove.Infrastructure.Settings** | `SiteSettingsApi`, `BrandingSettingsApi` |
| Admin settings, title bar (`TitleBarSettingsStore`), the admin theme preference | **Grove.Infrastructure.Shells.Admin** | `AdminSettingsApi`, `TitleBarSettingsApi`, `ThemeSettingsApi` |
| Deployment plans, steps, import, export | **Grove.Deployment** | `DeploymentApi` (the remote targets through `IRemoteDeploymentTargets`) |
| Sending a plan to a remote instance, remote instances and clients | **Grove.Deployment.Remote** | `RemoteDeploymentApi` |
| Custom settings | **Grove.Infrastructure.CustomSettings** | `CustomSettingsApi` |
| Users, sign-in (`LoginService`), login settings, the user's own culture | **Grove.Access.Users** (an Access module) | `UsersApi`, `AuthApi`, `LoginSettingsApi`, `UserCultureApi` |
| The access model, the antiforgery token, the permissions hub (`IPermissionInvalidator`) | **Grove.Access** | `AccessModelApi`, `AntiforgeryApi`, `PermissionHubApi` |
| Content definitions: types, parts, content groups, field visibility, definition locks | **Grove.Contents.Types** | `ContentDefinitionsApi`, `ContentTypesEditorApi`, `ContentGroupsApi`, `FieldVisibilityApi` |
| Content items; the content groups' services and pages | **Grove.Contents** | `ContentItemsApi` |
| Themes and the shell contracts (`ShellContracts/`) | **Grove.Themes** | `ThemesApi` |
| Features | **Grove.Infrastructure.Features** | `FeaturesApi` |
| Localization settings, the admin client's strings, `PoTranslationLookup` | **Grove.Data.Localization** | `LocalizationApi` (stored edits through `IClientStringOverrides`) |
| Translations | **Grove.Data.Localization.Translations** | `TranslationsApi` |
| The option picker: option sources, a field's picker binding | **Grove.Contents.OptionPicker** | `OptionSourcesApi`, `OptionPickerAttachmentsApi` |

Still in Grove.Infrastructure.Server, each for a stated reason:

- **Icons** (`IconsApi`, `IconProvidersApi`, `TenantIconsApi`, `IconifyApi`, their stores, the
  `Grove.Display.Icons` features): an exception to fix
  later with a small server-side icons module. `Grove.Display.Icons` and `Grove.Display.Icons.Iconify` are libraries in
  the WebAssembly client's closure, so neither can host a server module.
- **The admin menu and navigation** (`AdminMenusApi`, `AdminMenuLayoutApi`, `AdminMenuLayoutHubApi`,
  `NavigationApi`, the menu services): the page registry's views
  ([admin-menu.md](admin-menu.md) › The page registry).

### 2. Access → Grove.Access

ShellSelectionMiddleware (the gate) and RouteGateMatcherPolicy. The page gate is already in
Access (`IRouteAuthorization`, over the page registry's entries), and so is permission
invalidation (`IPermissionInvalidator` in Grove.Access.Abstractions, the hub in Grove.Access).
This is the "one gate" step in [access.md](access.md) › The system; the move and that step are
the same work. `IRequestAccess`, the helper every API uses, is in Grove.Infrastructure.Server.Abstractions.

### 3. The option picker → Grove.Contents.OptionPicker

The option picker is **its own module, Grove.Contents.OptionPicker** (feature `Grove.Contents.OptionPicker`), and
the one field type for picking: list options, users, content items, cultures, time zones and
currencies are all option sources of `OptionPickerField`. It carries its own index, source
providers, API and UI, and other modules bind to it. Every host gets it:
`Grove.Application.Cms.Core.Targets` references it with `PrivateAssets="none"`
([architecture.md](architecture.md) › Modules, features and how a module reaches the host). The
features that bind fields to sources declare it (Grove.Parties, Grove.Data.Types.Money, Grove.Contents.SiteMenu, the
`Grove.Contents.PartLists` feature; Fruitful's modules through them).

- **Grove.Contents.OptionPicker.Abstractions** (WebAssembly-safe, no framework reference): the source
  contract (`IOptionSourceProvider`, `OptionRow` with its `EditUrl`, `OptionSourceQuery` with its
  `TitlePattern`, `OptionSourceLookup`, `IOptionSourceCreateLinks`/`OptionCreateLink`,
  `OptionSourceColumnDescriptor`, `ResolvedOptionFilter`), the picker's column and filter settings
  (`OptionColumn`, `OptionFilter`, `OptionFilterOperators`, `OptionParentChangeBehaviors`),
  `OptionSourceKeys` and `ContentItemSourceScope`, the pure helpers (`OptionFilterResolver`,
  `OptionFilterMatcher`, `OptionDependentFilterTranslator`, `OptionRowSorter`,
  `OptionSourceReferenceGuard`) and the option sources API's request and response types
  (`Grove.Contents.OptionPicker.Api`).
- **The field's stored shape** is with the other field types in Grove.Contents.Fields.Abstractions:
  `OptionPickerField` (`SourceKey`, `SelectedIds`), `OptionPickerFieldSettings` (source, Multiple,
  Required, Hint, Placeholder, columns, display template, `TitlePattern`, search and sort columns,
  filters) and `ContentItemPickerSettings` (`AsContentItemPicker(types, ...)` declares a content
  item pick; `ContentTypes()` reads its types).
- **Grove.Contents.OptionPicker**: the field's registration, display driver (`OptionPickerFieldDisplayDriver`,
  the `OptionPickerField` shape) and save rules (`OptionPickerFieldHandler`: required, single);
  `OptionPickerFieldIndex` and its migration (the source key falls back to the attachment's when a
  value has none); the search index handler; the user, content item, time zone and culture
  sources; `OptionPickerFieldKeyResolver`, `OptionParentValueResolver`; the GraphQL type
  (`sourceKey`, `selectedIds`, `contentItems`); Liquid access to the field and its display model;
  `OptionPickerPermissions` (`QueryOptionSources` gates the option sources API; each source decides
  which rows the caller sees) and the API: `api/grove/option-sources` (`OptionSourcesApi`: list,
  query, dependencies, validate-selection, resolve, create-links, columns) and
  `api/grove/option-picker/attachment` (`OptionPickerAttachmentsApi`). Tests:
  `Grove.Contents.OptionPicker/tests/Grove.Contents.OptionPicker.Tests`.
- **The content items source** (`contentitem:…`, `ContentItemOptionSourceProvider`): the qualifier
  names types (`contentitem:Customer,Vendor`), stereotypes (`contentitem:@Widget`) or every type
  without a stereotype (`contentitem:*`). It searches by title, pushes index filters and sorts into
  SQL (field-index joins when the qualifier names one type), labels rows with the attachment's
  Liquid `TitlePattern`, gives selected rows an edit link where the caller may edit the item, keeps
  the stored order of a multiple selection, and offers a create link per allowed creatable type the
  caller may create. On save, `ContentPickerRelationshipGuard` refuses a target of a type the source
  does not name and runs the relationship handlers the field's owner registered
  (`IContentPickerRelationshipHandler`, [content-items.md](content-items.md) › Picker relationship
  handlers).
- **The UI** is the module's client library, `Grove.Contents.OptionPicker.BlazorWasm` (eager,
  `[assembly: GroveEager]`, so the editor is in the shell's component registry before any route
  loads): `GroveOptionPicker`, registered as the placeable component `OptionPicker` for templates
  and the display manager, and `OptionPickerFieldEditor`, the editor facet for `OptionPickerField`
  (for a content item source it lists the selection in order with edit links and up/down, and
  shows the create links). The field's display component, `OptionPickerField`, is a field display
  in Grove.Display.Components like the other fields'; the editor-state helpers (`ContentEditorContext`
  picker settings, columns, cascade state) and the wire models (`OptionPickerModels`) stay in
  Grove.Display.Components, which the content editor and the admin's attachment editor share.
- **The retired content picker converts** (`Grove.Contents.OptionPicker/ContentItems`): the
  `ContentPickerFieldMigrations` data migration turns every `ContentPickerField` definition into an
  `OptionPickerField` over the content items source with equivalent settings (Multiple, Required,
  Hint, Placeholder, displayed types/stereotypes/all, title pattern) and moves every version of every
  item's values, contained items included, from `ContentItemIds` to `SelectedIds` with the source
  key; `ContentPickerRecipeMapping` converts old recipes' definition and content steps on the way
  in. Both use `ContentPickerConversion`. The SQL `ContentPickerFieldIndex` table is dropped by
  Grove.Contents.Fields' index migration.
- The types keep the namespaces they had in Grove.Infrastructure.Server (`Grove.Fields`, `Grove.Infrastructure.Settings`,
  `Grove.Services`, `Grove.Models`, `Grove.Data.Indexing`): stored settings, the index migration's
  record and the downstream modules name them.
- **Field visibility** is Grove.Contents.Types': `IFieldVisibilityEnforcer` and its implementation
  `FieldVisibilityEnforcer` clear the fields a condition hides on save. The values a condition
  compares come through `IFieldComparisonValueResolver`; Grove.Contents.OptionPicker registers one that
  resolves a picker parent's stored ids to its source's keys (`OptionParentValueResolver`).
  Without it the stored values are compared as they are.

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
`custom-settings/{type}/editor`), `RoutingApi`, `AppApi` and AppViewModels (the
bootstrap aggregate), ServerApiClient, UnsupportedJSRuntime, ShellAssemblies,
DesignSystemStartup, Manifest and Startup, plus the API hosting plumbing in `Api/`
([MVC/api.md](MVC/api.md) § Placement). This is the Blazor host that
[blazor-display.md](blazor-display.md) describes.

## Consequences

- Workflows, Global, Money, Regions and the `Grove.Contents.PartLists` feature declare a
  dependency on the feature `Grove`; each declares the modules it actually uses as well
  (`Grove.Contents.PartLists`: `Grove.Contents.Types` for the definition lock guard,
  `Grove.Data.Global`, `Grove.Contents.OptionPicker`).
- The modules that register APIs reference Grove.Infrastructure.Server.Abstractions. Parties, Members, Money
  and Workflows reference Grove.Contents.OptionPicker for the option picker; Grove.Contents references
  only Grove.Contents.OptionPicker.Abstractions (Grove.Contents.OptionPicker references Grove.Contents). Members,
  Parties, Workflows, Money and Regions still reference Grove.Infrastructure.Server for the routing types, a
  reference that becomes Grove.Infrastructure.Server.Abstractions.
- Every moved API kept its route.
- Each step: move, repoint references, compile all three solutions, regenerate the graph.

## Order

What is left in Grove.Infrastructure.Server's API, in order: the admin menu and navigation (with the page
registry), the icons (a server-side icons module), then Access's gate (the one-gate step). [MVC/api.md](MVC/api.md) § Waves schedules the
rest of the API work.

## Decisions

- **S1.** Each API goes to the module that declares the surface and registers in Grove.Infrastructure.Server's
  API registry (§ 1).
- **S2.** The option picker is its own module, Grove.Contents.OptionPicker, with a WebAssembly-safe
  Grove.Contents.OptionPicker.Abstractions for its contracts and DTOs; field visibility is
  Grove.Contents.Types', with the picker plugged in through `IFieldComparisonValueResolver`; where
  the picker's UI component lives is open (§ 3).
- **S3.** `GroveAssignmentPart` is retired; `GroveOrganizationPart` and the organization scope
  are Grove.Parties' (§ 4).
- **S4.** Whether the host keeps the feature id `Grove` as its base feature: open.
