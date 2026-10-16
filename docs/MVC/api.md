# MVC retirement › API: modules declare, Grove.Infrastructure.Server registers and serves

Workstream (c) of the MVC retirement, done together with moving the APIs out of Grove.Infrastructure.Server.
This document is the tracker for every **API surface** in `src/`: the route groups modules
declare, the workflow engine's management API, SignalR hubs and the machine (protocol)
endpoints, and for the MVC plumbing those surfaces used to depend on. Siblings:

- [views.md](views.md) (workstream a): every view surface to a Blazor page. Each port there is a
  page plus an API call; the API side of each port is tracked here.
- [shape-engine.md](shape-engine.md) (workstream b): drivers, shape templates, `IHtmlDisplay`, the
  fallback. It depends on this document's **error contract** (§ The error contract, its U1) and
  on the content and settings editor APIs.
- [server.md](../server.md): the per-area inventory of what leaves Grove.Infrastructure.Server and where it goes.
- [workflows.md › Operations](../workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery):
  steps 5-8 (move reads, move writes, generate surfaces, retire) are partly absorbed here.

Status: the seam, the defined layer, the moves out of Grove.Infrastructure.Server and every
module's API are built (☑ below): no `Controller` or `ControllerBase` class is left in `src/` or in
Fruitful, every API is a route group a contributor declares, and there is no MVC: no `AddMvc`
or `AddControllers()`, no `Grove.Mvc` or `Grove.Mvc.Abstractions`. What remains is P14 (generated
surfaces). Sizes: **S** (hours), **M** (a day),
**L** (several days), **XL** (a subsystem). Every tracked item names an owner module, its
dependencies, a size and a wave.

## Principles

- **The MVC layer goes**, in three workstreams: (a) views to Blazor components, (b) the shape
  engine, (c) the API layer (this document).
- **Modules declare, Grove.Infrastructure.Server registers and serves**
  ([architecture.md](../architecture.md#modules-declare-groveserver-registers-and-serves)).
  Every API is declared by the module whose surface it is and registered in Grove.Infrastructure.Server's API
  registry; Grove.Infrastructure.Server keeps only the hosting plumbing (§ Placement).
- **`Grove.Data*` does not deal with APIs.** It is the type manager, the validation engine and
  the database connector (the host database and external databases as SQL connections);
  outgoing connections to other external systems are the protocol modules'
  ([queries.md › Connections](../queries.md#connections-the-protocol-modules-and-the-data-layer)). The API layer calls the data engine (content
  manager, definitions, YesSql sessions through `ScopedExecution`, the query pipeline) and the
  connection system; nothing in the data engine returns HTTP results (§ Grove.Data: verified
  clean).
- **Access modules hold sign-in, registration and the authorization and permission engines**,
  with their screens (login, registration, password reset, two-factor): authorization and
  permissions share a development cycle. They declare the sign-in, account, two-factor,
  antiforgery and login-settings APIs and the authentication handler; other modules register
  their access systems with Access.
- **Workflows builds on top of the registered APIs** and never replaces a module's API with a
  workflow endpoint. Its management API is declared like any module's; workflow-defined HTTP
  endpoints are defined entries in the registry (§ The seam).
- **The API never renders an error page.** It answers problem details; the shell renders the
  page: the tenant's one shared set of error pages, laid out by the admin theme
  ([views.md](views.md) › Shared screens).
- **Hosting: minimal APIs, staged** (§ Hosting).

## The inventory

Counts from a scan of `src/` (excluding `obj`, tests, the vendored engine where noted).

| Kind | Count | Where |
| --- | --- | --- |
| `Controller` / `ControllerBase` classes | 0 | none in `src/` or Fruitful |
| Route groups (`IApiContributor`) | 133 contributors in `src/`, 11 in Fruitful | every module's API, the protocol endpoints, the hubs, the engine's management API (§ The seam) |
| Workflow engine management API (vendored engine) | 72 endpoints | minimal APIs under `grove-workflows/api`, declared by `WorkflowsManagementApi` ([workflows.md](../workflows.md)) |
| SignalR hubs | 4 | each declared by a contributor: permissions (Grove.Access), admin-menu layout (Grove.Infrastructure.Server), media (Grove.Media); the engine's workflow instances hub |
| Middleware / static endpoints outside the registry | 7 | GraphQL `/api/graphql`, health checks, robots.txt, media file serving and image processing, the Liquid intellisense and Facebook SDK scripts, the host's Blazor framework scripts |

### The route groups that replaced the controllers

Engine = what the handlers call: `content-manager` (`IContentManager`), `definitions`
(`IContentDefinitionManager`), `yessql` (`ISession` / `QueryIndex`), `site-settings`,
`documents` (`IDocumentManager`), `identity` (user and role managers), `access`
(`IRequestAccess`, `IAccessDecision`, scope sets), `shells`, `file-store`, `queries`,
`workflow-engine`, `services` (a module service that itself calls one of these). The routes,
methods and JSON shapes are the controllers' ones; the clients did not change.

| Module | Contributor | Route prefix | Engine |
| --- | --- | --- | --- |
| Grove.AuditTrail | `AuditTrailApi` | `api/grove/audit`, `audit/settings` | access, site-settings |
| Grove.Workflows.BackgroundTasks | `BackgroundTasksApi` | `api/grove/background-tasks` | services |
| Grove.Contents.Fields | `UserPickerApi` | `api/grove/content-fields/users` | definitions |
| Grove.Contents (`Grove.Contents.PartLists`) | `ContentPartListsApi`, `GlobalListsApi` | `api/grove/content-part-lists`, `api/grove/global/lists` | services, yessql |
| Grove.Contents | `AuditTrailContentApi` | `api/grove/audit/content/{eventId}` | content-manager, yessql |
| Grove.Contents.Localization | `CultureRedirectApi` | the culture switch redirect (site links) | services |
| Grove.Communications.Email | `EmailTestApi` | `api/grove/email/providers`, `api/grove/email/channel`, `api/grove/email/test` (the channel's settings are the `email` settings group) | services |
| Grove.Communications | `DeliveryLogApi` | `api/grove/communications/deliveries` | yessql |
| Grove.Communications | `PortalNotificationsApi` | `api/grove/notifications` | yessql |
| Grove.Display.Layers | `LayersApi` (a layer's save runs Grove.Display.Rules' condition checks through `IRuleConditionChecker`, answered as the error contract's map keyed `conditions.{conditionId}`) | `api/grove/layers` | services |
| Grove.Display.Placements | `PlacementsApi` | `api/grove/placements` | services |
| Grove.Media.Scanning | `ScanningSettingsApi`, `ScanVerdictApi` | `api/grove/media/scanning/settings`, the scanner's verdict callback | services |
| Grove.Media | `FilesApi` | `api/files` | access, file-store |
| Grove.Media | `MediaApi`, `MediaTusApi` | `api/media` (the Vue media library and picker) | file-store |
| Grove.Access.Members | `MembersApi`, `MemberPortalApi` | `api/grove/members`, `api/grove/members/portal` | yessql, content-manager, site-settings, identity, access |
| Grove.Access.OpenId | `OpenIdApplicationsApi`, `OpenIdScopesApi`, `OpenIdServerConfigurationApi`, `OpenIdValidationConfigurationApi`, `OpenIdConsentApi` | `api/grove/openid/{applications,scopes,server,validation,consent}` | identity, shells, services |
| Grove.Access.OpenId | `OpenIdProtocolApi` | the server settings' `/connect/authorize`, `/connect/logout`, `/connect/token`, `/connect/userinfo` | identity |
| Grove.Access.Sharing | `SharingApi` | `api/sharing` | access |
| Grove.Access.Users | `AccountApi`, `UserSettingsApi` | `api/grove/account`, `api/grove/settings/users` | site-settings, identity |
| Grove.Access.Users | `ExternalAuthenticationApi`, `ExternalLoginApi` | `Grove.Access.Users/ExternalAuthentications/{ExternalLogin,ExternalLoginCallback}`, `api/grove/account/external-login` | identity |
| Grove.Parties | `PartyAssociationsApi`, `PartyContactsApi`, `PartyNamesApi`, `PartyPositionsApi`, `PartyRolesApi`, `PartyTypesApi` | `api/grove/parties/*` | content-manager, site-settings, access, services |
| Grove.Data.Regions | `AddressingMapsApi`, `GeoImportApi` | `api/grove/regions/addressing-maps`, `regions/geo/import` | yessql |
| Grove.Data.Queries | `QueriesApi` (with `queries/{name}/run`) | `api/grove/queries` | queries |
| Grove.Data.Feeds | `FeedsApi` | the RSS feed route | content-manager |
| Grove.Seo.Sitemaps | `SitemapDocumentsApi` | `sitemap.xml` and the sitemap index | services |
| Grove.Deployment.Remote | `RemoteImportApi` | the remote instance import (API key) | services |
| Grove.Security.ReCaptcha | `ReCaptchaApi` | `api/grove/recaptcha` | services |
| Grove.Infrastructure.Server | `AdminMenusApi`, `AdminMenuLayoutApi`, `AdminMenuLayoutHubApi`, `NavigationApi` | `api/grove/admin-menus`, `admin-menu-layout`, `navigation` | services |
| Grove.Infrastructure.Server | `IconsApi`, `IconProvidersApi`, `IconifyApi`, `TenantIconsApi` | `api/grove/icons`, `icons/providers`, `iconify`, `icons/tenant` | services |
| Grove.Infrastructure.Server | `AppApi`, `RoutingApi` | `api/grove/app`, `api/grove/routing` (the host's own) | site-settings, identity, services |
| Grove.Infrastructure.Tenants | `TenantApi` | `api/tenants` (remote callers) | shells |
| Grove.Workflows | `WorkflowApprovals`, `WorkflowWebhooks`, `WorkflowHooks`, `WorkflowRegistry`, `WorkflowDefinitions`, `WorkflowTriggers`, `WorkflowActions`, `WorkflowPending` | `api/grove/workflows/*` | workflow-engine, identity, access, yessql |
| Fruitful | `AccountsApi`, `CustomersApi`, `PricingApi`, `TransactionTaxApi`, `TransactionsApi`, `LedgerApi`, `AssetsApi`, `GpcImportApi`, `MembershipsApi`, `TaxesApi`, `TaxRateImportsApi` | `api/grove/accounting/*`, `assets`, `gs1`, `members/memberships`, `taxes` | services |

Not in the table: the vendored engine's management API (below) and the
designer.

### Other route groups (a sample)

The APIs built as route groups from the start; every module's `Api/` folder holds its own.

| Module | Routes | Notes |
| --- | --- | --- |
| Grove.Media (`Api/MediaApi.cs`, handlers in `Endpoints/Api/*`) | `api/media/*` (GetMediaItems, Upload, Move, Delete, folders, Tus info, localizations, ...) | the stock media library API the Vue media library and picker use; the native file system is `api/files` (`FilesApi`) |
| Grove.Infrastructure.Server (`Api/DisplayApi.cs`) | `api/grove/site/page`, `pages/{id}/preview`, `content-items/{id}/editor`, `custom-settings/{type}/editor` | the host's display endpoints, a route group |
| Grove.Infrastructure.Settings (`Api/SettingsGroupsApi.cs`) | `api/grove/settings/groups`, `settings/groups/{groupId}` (GET, PUT) | every registered settings group's form and save (§ Settings groups) |
| Grove.Shortcodes (`Api/ShortcodesApi.cs`) | `api/grove/shortcodes` | the shortcodes the HTML and Markdown editors insert |
| Grove.Shortcodes (`Api/ShortcodeTemplatesApi.cs`) | `api/grove/shortcodes/templates` (GET, POST, PUT, DELETE) | the shortcode templates page |
| Grove.Contents.OptionPicker (`Api/OptionSourcesApi.cs`, `Api/OptionPickerAttachmentsApi.cs`) | `api/grove/option-sources` (6), `api/grove/option-picker/attachment` (3) | the option picker's rows, columns, dependencies and a field's binding ([server.md](../server.md) § 3) |
| Grove.Data.Connectors (`Api/ConnectionsApi.cs`) | `api/grove/connections` (definitions, `import`, `protocols`, `{key}/credential`, `{key}/credentials/{userId}`, `app-credentials`, `me`, `{key}/oauth/authorize`, `oauth/callback`, `{key}/oauth/device[/poll]`) | the Connections and Connected accounts pages; admin-side staff callers only; the OAuth callback is served on the admin shell's path |
| Grove.Routes (`Api/RoutesApi.cs`) | `api/grove/routes/{pages,home,content-sources,content,rules,rules/reorder}` | the route management page (views.md › Routes) |
| Grove.Search (`Api/SearchApi.cs`) | `api/grove/search` | the site search, anonymous; the index's query permission decides |
| Grove.Seo.Sitemaps (`Api/SitemapsApi.cs`) | `api/grove/sitemaps`, `{id}`, `{id}/sources[/{sourceId}]`, `source-types`, `content-types`, `cache[/{fileName}]` | the sitemaps pages |
| Grove.Communications.Sms (`Api/SmsTestApi.cs`) | `api/grove/sms/providers`, `api/grove/sms/channel`, `api/grove/sms/test` | the SMS test page and the channel card |
| Grove.Infrastructure.Tenants (`Api/FeatureProfilesApi.cs`) | `api/grove/tenants/feature-profiles` (GET, POST, PUT, DELETE) | the feature profiles page (Default tenant) |

### Workflow engine

The vendored engine's management API under `grove-workflows/api` (73 operations), declared by
the contributor `WorkflowsManagementApi` (`Grove.Workflows/Server/Api/WorkflowsManagementApi.cs`,
P7) like every module's API: minimal-API handlers with explicit binding (a JSON body is an
`EngineBody<T>`, the engine's one `BindAsync` source, deserialized through the engine's
serializer), the error contract's problem details, and a policy per endpoint
(`EngineApiPolicies`, the engine permission names) that the engine's `ApiSecurityMiddleware`
(`Grove.Workflows/Server/Security/ApiSecurityMiddleware.cs`) grants per request from the
caller's Grove permissions ([workflows.md › The engine API](../workflows.md)). The engine's
`HttpEndpoint` activity routes are
a different thing: workflow-defined endpoints, the defined entries of `WorkflowHttpEndpointSource`
(P15).

### SignalR hubs

| Hub | Route | Module |
| --- | --- | --- |
| `PermissionHub` | `/api/grove/permissions` | Grove.Access (`Services/PermissionInvalidation.cs`, `PermissionHubApi`) |
| `AdminMenuLayoutHub` | `/api/grove/admin-menu-layout` | Grove.Infrastructure.Server (`Services/AdminMenuLayoutInvalidation.cs`) |
| `MediaHub` | `/hubs/media` | Grove.Media (`Hubs/MediaHub.cs`, `MediaHubApi`) |
| `WorkflowInstanceHub` | engine real-time | vendored engine |

Hubs need no MVC. The caller lifetime on hubs is already Grove.Access's
(`ConnectionCallerHubFilter`).

### Machine and protocol endpoints

| Endpoint | Module | Hosting |
| --- | --- | --- |
| `/api/graphql` | Grove.Data.Protocols.GraphQL | middleware (`GraphQLMiddleware`) |
| `/connect/authorize`, `/connect/token`, `/connect/logout`, `/connect/userinfo` | Grove.Access.OpenId | `OpenIdProtocolApi` (paths from the server settings; OpenIddict passthrough; the consent and logout decision posts validate their form's antiforgery token in the handler) |
| external sign-in start and callback | Grove.Access.Users | `ExternalAuthenticationApi` |
| health checks | Grove.Infrastructure.HealthChecks | `MapHealthChecks` |
| `robots.txt` | Grove.Seo | `RobotsMiddleware` |
| media files, image processing, secure media | Grove.Media | middleware; `FileServingEndpoint` for placements |
| scanner verdict callback | Grove.Media.Scanning | `ScanVerdictApi` (its own signature, no antiforgery) |
| RSS feeds | Grove.Data.Feeds | `FeedsApi`; the routes are declared through `FeedRoutesOptions.MapFeed` (Grove.Contents.Lists: `Contents/Lists/{contentItemId}/rss`) |
| sitemaps (`sitemap.xml`, index) | Grove.Seo.Sitemaps | `SitemapDocumentsApi` (`{**sitemap:sitemapPath}`, a route constraint that matches only an enabled sitemap's path) |
| remote deployment import | Grove.Deployment.Remote | `RemoteImportApi` (the gate's remote deployment key scheme; answers problem details on failure) |
| culture switch redirect | Grove.Contents.Localization | `CultureRedirectApi` |
| webhooks | Grove.Workflows | `WorkflowWebhooks` (anonymous, signed) |

## Grove.Data: verified clean

A scan of `Grove.Data`, `Grove.Data.Abstractions`, `Grove.Data.Global`, `Grove.Data.Types`,
`Grove.Data.YesSql` and `Grove.Data.YesSql.Abstractions` for controllers, endpoint mapping,
`IActionResult`, `HttpContext`, problem details, `Microsoft.AspNetCore.Mvc` and `api/grove`
finds **nothing**. What lives there is data-engine work and stays: documents, migrations, the
global store, the value types, and the scope compilation
(`Grove.Data.YesSql/Scoping/ScopeExpressions.cs`). The API pieces live elsewhere:

- the request access helper `IRequestAccess` (`Grove.Infrastructure.Server.Abstractions/Api/RequestAccess.cs`),
  which registering modules use;
- the error contract's helpers (`ApiProblems`, `ApiResults`, `ApiProblemExtensions`) in
  Grove.Infrastructure.Server.Abstractions, `ApiProblemDetailsMiddleware` and the JSON options
  (`ApiJsonOptionsConfiguration`) in Grove.Infrastructure.Server (§ Plumbing);
- `ApiAuthenticationHandler` in Grove.Access, which owns the gate;
- the validation sink (`IValidationSink`, shape-engine.md U1) is validation, not API; Grove.Infrastructure.Server
  turns its errors into problem details (§ The error contract).

## Placement

Applied to every surface:

1. **Each module declares its own APIs** and answers them with its own services. Protocols a
   module implements (OpenIddict, GraphQL, feeds, sitemaps, file serving, scanner callback,
   health, robots, the workflow engine) are the same case.
2. **Every module registers its endpoints** into **Grove.Infrastructure.Server's API registry** (§ The seam).
   Modules reference **Grove.Infrastructure.Server.Abstractions** only, never Grove.Infrastructure.Server, and Grove.Infrastructure.Server
   references no module for its APIs, so there is no reference cycle. A disabled feature's
   registrations are not in the tenant's registry; a downstream module can override a
   registration.
3. **Grove.Infrastructure.Server keeps only hosting plumbing**: the registries and their catalog, route
   groups, per-tenant gating, the antiforgery filter (validation asks Access), JSON options,
   the error contract, the OpenAPI document, and the host's own endpoints (the bootstrap
   aggregate `api/grove/app`, `api/grove/routing`, the Blazor framework scripts). It declares
   no domain API.
4. **Where each area goes:**

   | Surface | Module |
   | --- | --- |
   | Site settings, recipes, features, tenants, setup | `Grove.Infrastructure.Settings`, `Grove.Infrastructure.Recipes`, `Grove.Infrastructure.Features`, `Grove.Infrastructure.Tenants`, `Grove.Infrastructure.Setup` (`Grove.Infrastructure` for what is shell-level) |
   | Content items | `Grove.Contents` |
   | Content definitions: types, parts, field visibility, definition locks, content groups | `Grove.Contents.Types` |
   | Users | `Grove.Access.Users` |
   | Roles | `Grove.Access.Roles` |
   | The access model: permission sets, groups, overrides | `Grove.Access` |
   | Sign-in, account, two-factor, antiforgery, login settings | the Access modules (`Grove.Access.Users` for sign-in, account and two-factor; `Grove.Access` for antiforgery) |
   | Media | `Grove.Media` |
   | Other settings | each its module: admin settings, title bar and the admin theme preference `Grove.Infrastructure.Shells.Admin`; debug `Grove.Infrastructure.Settings`; security headers `Grove.Security` |
   | Workflow management | `Grove.Workflows` |
   | Everything else (menus, icons, localization, templates, indexes, queries, ...) | the module it serves ([server.md](../server.md) § 1) |

5. **Grove.Infrastructure.Server.Abstractions** holds what a registering module needs: the registration
   interfaces (`Api/`), route conventions, `IRequestAccess`, the error contract types, the
   handler base (`ApiHandlers`), and the page registry's contracts (`Routing/`). Each moved
   API's models moved with it into its module.
6. **Not here:** database connections (Grove.Data, Grove.Data.YesSql) and search indexes
   (`Grove.Data.Indexing` profiles, registered by their module like any other surface).

### Tracker

Owner is where the code ends up. Wave numbers are § Waves.

| ☐ | Item | Today | Target | Owner | Deps | Size | Wave |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ☑ | P1. Grove.Infrastructure.Server.Abstractions project | — | `Api/`: `IApiContributor` / `ApiContributor`, `ApiRoutes`, `IRequestAccess`, `IApiExceptionMapper`, `ApiProblems` / `ApiResults`, `ApiHandlers`, `IDefinedApiSource`, `IDefinedApiInvalidator`, `IApiEndpointRegistry` | Grove.Infrastructure.Server.Abstractions | — | M | 1 |
| ☑ | P2. The 47 Grove.Infrastructure.Server controllers | Grove.Infrastructure.Server, MVC-hosted | moved to their declaring modules as route groups ([server.md](../server.md) § 1); the display actions are the host's display endpoints (`Api/DisplayApi.cs`); the option picker is Grove.Contents.OptionPicker's (server.md S2). The host keeps `AppApi` and `RoutingApi` (the bootstrap), the admin menu and navigation over the page registry (`AdminMenusApi`, `AdminMenuLayoutApi`, `NavigationApi`) and the icons (`IconsApi`, `IconProvidersApi`, `IconifyApi`, `TenantIconsApi`) until a server-side icons module exists, all route groups | each declaring module | P1, P5 | L | 2-3 |
| ☑ | P3. Platform-module APIs | Email, Email.Smtp, AuditTrail (2), BackgroundTasks, Layers, Placements, Notifications portal, Media.Scanning settings, Users settings, ContentFields user picker, Contents audit, Tenants `api/tenants`, ReCaptcha, Sharing, OpenId admin + consent | route groups in their modules (§ The route groups that replaced the controllers); `api/tenants` (`TenantApi`, remote callers) keeps its path beside `api/grove/tenants` | each module | P1, P5 | M | 2 |
| ☑ | P4. App-layer module APIs | Parties (6), Members (2), Regions (2), Content Part Lists (2, the `Grove.Contents.PartLists` feature), Workflows (10), Fruitful's (11) | route groups in their modules; the modules reference Grove.Infrastructure.Server.Abstractions, and Grove.Infrastructure.Server only for the admin menu and routing types | each module | P1, P5 | M | 3 |
| ☑ | P5. The seam | — | § The seam: `ApiEndpointMapper` maps the tenant's contributors into route groups; `ApiEndpointRegistry` lists them and the defined entries | Grove.Infrastructure.Server, Grove.Infrastructure.Server.Abstractions | P1 | M | 1 |
| ☑ | P6. Access APIs | `api/grove/account` (Users) | `AuthApi` (the sign-in form and its extensions, the password and two-factor sign-in, the external sign-in start, the code sending), `RegistrationApi`, `PasswordResetApi`, `TwoFactorApi`, `LoginSettingsApi`, `UsersApi` (with `LoginService`; a user's two-factor authentication turned off), `AccountApi`, `UserSettingsApi`, the external sign-in (`ExternalAuthenticationApi`: start and callback; `ExternalLoginApi`: the pending model, register, link) in Grove.Access.Users; `AntiforgeryApi` and `AccessModelApi` in Grove.Access; `MembersApi` and `MemberPortalApi` in Grove.Access.Members; `SharingApi` in Grove.Access.Sharing; the OpenId admin, consent and protocol APIs in Grove.Access.OpenId | Grove.Access.Users, Grove.Access, Grove.Access.Members, Grove.Access.Sharing, Grove.Access.OpenId | P5 | M | 3 |
| ☑ | P7. The workflow engine's management API | engine-hosted under `grove-workflows/api` | `WorkflowsManagementApi`, a declared contributor keeping the engine's path; minimal APIs like every contributor ([workflows.md](../workflows.md)) | Grove.Workflows | P5 | S | 3 |
| ☑ | P8. Hubs | Server 2, Media 1, engine 1 | each mapped by a contributor: `PermissionHubApi` (Grove.Access), `MediaHubApi` (Grove.Media), `AdminMenuLayoutHubApi` (Grove.Infrastructure.Server, with the admin menu over the page registry); the engine's stays the engine's | each module | P5 | S | 3 |
| ☑ | P9. Media APIs | `api/files` (26 actions), `api/media/*` (17 endpoints) | `api/grove/media*` (`MediaFilesApi`, `MediaOptionsApi`, `MediaProfilesApi`), `api/files` (`FilesApi`, the native file system) and `api/media/*` (`MediaApi`, `MediaTusApi`: the Vue media library and picker, until the native library and pickers replace them, [views.md](views.md)) are Grove.Media's route groups | Grove.Media | P5, views.md | L | 3 |
| ☑ | P10. Setup off MVC | — | the static-SSR installation page of Grove.Infrastructure.Setup (views.md § Setup) posts its own form (`EditForm`, antiforgery-validated) in the setup shell, anonymous, before any store, to `SetupInstallation` → `ISetupService`; no setup API | Grove.Infrastructure.Setup | views.md § Setup | M | 4 |
| ☑ | P11. Protocol endpoints | GraphQL, OpenIddict, health, robots, media serving, feeds, sitemaps, remote deployment import, scanner callback | in their modules (§ Machine and protocol endpoints): the OpenIddict endpoints, feeds, sitemaps, the remote import, the scanner callback, the external sign-in callback and the webhooks are route groups; GraphQL, health checks, robots and media serving stay middleware or framework endpoints | each module | H3 | M | 4 |
| ☑ | P12. MVC JSON/redirect actions | the pickers' search, the culture switch, list ordering, taxonomy tags, email confirmation, admin two-factor (the deployment-target executors and add-to-plan are deleted; `ContentDeploymentApi` replaces them) | the culture switch is `CultureRedirectApi` (Grove.Contents.Localization); the others were deleted with their MVC screens, whose Blazor replacements call their module's route groups | per group | views.md | M | 3-4 |
| ☑ | P13. Retirements (workflows.md step 8) | `api/content` (Contents), `api/queries` (Queries), `api/lucene`, `api/elasticsearch`, `Notifications/MarkAsRead`, `url-rewriting/resort` | all deleted; their callers use the replacements: the content items API (`api/grove/content-items`, `ContentItemsApi`), a query's run (`api/grove/queries/{name}/run`, `QueriesApi`: the admin client's branding and the display actions' query slots), the index query APIs and the site search, the notifications API (`api/grove/notifications`), Grove.Routes' `rules/reorder` | each declaring module | step 5-7, views.md | M | 4 |
| ☐ | P14. Generated surfaces (workflows.md step 7) | — | REST + OpenAPI over the operation registry under `api/grove/ops/*` (route fixed in P14), entered as defined entries in the Server's API registry beside the declared ones; a module's declared API may be implemented by a generated route, never replaced by a workflow endpoint; GraphQL and RPC later | Grove.Infrastructure.Server (registry), Grove.Workflows | P15, workflows.md step 3 | XL | 5 |
| ☑ | P15. The defined layer | — | `DefinedApiEndpointDataSource`, one per tenant, reloaded after the scope that invalidated it; the workflow `HttpEndpoint` routes are `WorkflowHttpEndpointSource`'s entries served by the engine's `HttpWorkflowRequestHandler`; the engine's `IRouteTable`, `IRouteMatcher`, route table updater and `HttpWorkflowsMiddleware` are gone | Grove.Infrastructure.Server, Grove.Workflows | P5 | M | 4 |
| ☑ | P16. Convert the in-module controllers | P3, P4 and Fruitful's | each a route group registered through the seam; no `Controller` or `ControllerBase` class is left in `src/` or Fruitful | each module | H3 | L | 3-5 |

## The seam

Every module that declares an API registers it; Grove.Infrastructure.Server holds the registry and serves it.
The registration interface, in Grove.Infrastructure.Server.Abstractions (`Api/IApiContributor.cs`):

```csharp
public interface IApiContributor
{
    /// <summary>The registry name: unique per tenant, the OpenAPI tag. A later registration
    /// of a name replaces an earlier one (a downstream module's override).</summary>
    string Name { get; }

    /// <summary>The feature whose enablement turns these endpoints on.</summary>
    string FeatureId { get; }

    /// <summary>The group prefix: "api/grove" unless the API keeps a path of its own.</summary>
    string RoutePrefix { get; }

    /// <summary>Maps the contributor's endpoints into the group the server hands it.</summary>
    void Map(IEndpointRouteBuilder group);
}
```

A module derives from `ApiContributor` (the `api/grove` prefix) and registers it from the
startup of the feature that owns the API: `services.AddApiContributor<RolesApi>()`. Its handlers
are static methods, or an `ApiHandlers` class (services by constructor, the request through
`HttpContext`) created per request with `ApiHandlers.For<T>(httpContext)`; `ApiResults` keeps
the text bodies the controller-hosted APIs answered with, `ApiProblems` answers problem details.

- **Two layers.** *Declared* entries come from module code: mapped into the tenant's route
  builder when the shell's pipeline is built (`ApiEndpointMapper.MapApiRegistryAsync`, from
  Grove.Infrastructure.Server's `ConfigureAsync`), never stored. *Defined* entries are data: an
  `IDefinedApiSource` registered in the tenant (`services.AddDefinedApiSource<T>()`) returns
  `DefinedApiEntry`s (route pattern, methods, request delegate, metadata, order), served by the
  tenant's `DefinedApiEndpointDataSource`. `IDefinedApiInvalidator.Invalidate(source)` (a publish)
  reloads the sources after the current shell scope completes, so the reload reads committed
  data; a reload that yields the same routes leaves the endpoints alone, and one failing source
  leaves the others served. The workflow `HttpEndpoint` routes are the first defined source
  (P15); generated operation endpoints join them (P14). This mirrors the page registry's
  declared and data-defined sources (`Grove.Infrastructure.Server.Abstractions/Routing`).
- **Per tenant.** Contributors and sources are registered in the tenant's container from the
  startups of enabled features, and the data source is a singleton of the tenant's container
  added to the tenant's own route builder: a disabled feature's endpoints are not in the
  tenant's route table, and one tenant's defined entries never route in another.
- **What the group carries** (applied once by Grove.Infrastructure.Server): the prefix, the antiforgery
  endpoint filter (`ApiAntiforgeryEndpointFilter`: a state-changing call validates the
  `RequestVerificationToken` header through the tenant's `IAntiforgery`, which passes bearer
  callers; `.SkipApiAntiforgery()` exempts an endpoint), the exception mapping
  (`ApiExceptionEndpointFilter`, § The error contract), the OpenAPI tag (the contributor's name)
  and `ApiContributorMetadata`. JSON is the tenant's HTTP JSON options, with documents and
  entities written with the document serializer options (`ApiJsonOptionsConfiguration`).
- **The API registry.** `IApiEndpointRegistry` lists the declared contributors with their
  routes, and the defined entries. The OpenAPI document reads the endpoints through the endpoints explorer.
- **Explicit binding, built at mapping.** Every handler parameter names its binding source,
  and each contributor's endpoints are built as it maps them; one that cannot be built is left
  out and reported (§ Hosting).
- **Endpoint conventions.** A group or an endpoint states what a controller's attributes used
  to: `.RequireAuthorization()` (with an `AuthorizeAttribute` naming schemes or a policy),
  `.AllowAnonymous()`, `.SkipApiAntiforgery()` for callers that authenticate by their own
  signature or key, `.DisableAntiforgery()` beside it on a form-binding endpoint (the framework's
  form check, whose middleware is not run; the group's filter still validates the header),
  `RequestSizeLimitAttribute` metadata, `.WithName(...)` for link generation and rate limiting.
  A contributor whose paths are not under `api/grove` names its own `RoutePrefix` (`api/files`,
  `api/sharing`, `api/tenants`, `""` for protocol paths).

## The error contract

One shape for every API error, so shape-engine.md's validation sink (U1) and the content and
settings APIs agree:

- `application/problem+json` (RFC 9457) with `type`, `title`, `status`, `detail`, and an
  `errors` map `{ key: [messages] }` for validation: the key is the member's wire name (the JSON
  name the client sent, `items[2].name` for nested members, `""` for the object), the same for a
  request DTO's rules (§ Validation), a handler's own checks (`ApiProblems.Validation`) and the
  sink's errors (keyed by field path `Part.Field` or by setting name).
- Mapped exceptions, registered by the module that owns the exception
  (`services.AddApiExceptionMapper<T>()`, `IApiExceptionMapper` in Grove.Infrastructure.Server.Abstractions):
  `DefinitionLockedException` → 409 (`DefinitionLockedExceptionMapper`, Grove.Contents.Types),
  `WorkflowHookFailedException` → 409 with the slot and the attachment
  (`WorkflowHookFailedExceptionMapper`, Grove.Workflows), `ContentSaveRefusedException` → 400
  with the save guards' messages under `""` (`ContentSaveRefusedExceptionMapper`,
  Grove.Contents); to come: `ExchangeRateUnavailableException` → 409 (from the module that
  prices with it, not Grove.Data), access denials → 403 or 404 (deny-as-not-found) from the
  decision.
- One set of mappers on the route groups (`ApiExceptionEndpointFilter`).
- **The validation sink** (`IValidationSink`, shape-engine.md U1): the errors the content manager
  and the part and field handlers raise during a request. A handler answers them with
  `ApiProblems.Validation(sink)`; an endpoint marked `.AnswersValidationSink()`
  (`ValidationSinkMetadata`) has them answered by the group's `ApiValidationSinkEndpointFilter`:
  a successful result becomes the error contract's 400 with the sink's map (keys `Part.Field`,
  setting names, `""`) when the sink holds errors; an error result is left as it is.
- `ApiProblemDetailsMiddleware` (Grove.Infrastructure.Server, `Api/`), in the tenant pipeline
  ahead of the access gate's authentication and authorization, writes the body of a bare 4xx
  that opted out of the HTML error pages (the "Api" scheme's 401 and 403, an endpoint with
  `ISkipStatusCodePagesMetadata`).

| ☐ | Item | Owner | Deps | Size | Wave |
| --- | --- | --- | --- | --- | --- |
| ☑ | E1. Error contract types and `IApiExceptionMapper` | Grove.Infrastructure.Server.Abstractions | P1 | S | 1 |
| ☑ | E2. The mappers on the route groups; the MVC exception filters deleted; the problem-details middleware registered by Grove.Infrastructure.Server in the tenant pipeline | Grove.Infrastructure.Server | E1 | S | 2 |
| ☑ | E3. Validation sink errors (shape-engine.md U1) rendered as `errors`: `ApiProblems.Validation(IValidationSink)`, `.AnswersValidationSink()` and `ApiValidationSinkEndpointFilter` | Grove.Infrastructure.Server(.Abstractions) | E1, shape-engine U1 | S | 2 |

## Validation

Request validation is **ASP.NET's native validation** (`services.AddValidation()`, per tenant,
in Grove.Infrastructure.Server's startup) over **Grove's own ruleset**: the rule catalog FluentValidation
offers where DataAnnotations is thin, written as `ValidationAttribute`s so the same rules run in
ASP.NET's validation filter and in Blazor's `DataAnnotationsValidator` unchanged.

- **Home:** `Grove.Display.Management.Abstractions/Validation`, namespace `Grove.Validation`: the
  smallest assembly both the WebAssembly client (through Grove.Display.Components) and every API module
  (through Grove.Infrastructure.Server.Abstractions) reach, so a DTO shared with the client carries the same
  rules on both sides.
- **The rules, as attributes:** `NotEmpty` (whitespace-aware; empty collections and value-type
  defaults fail), `MinimumLength`, `MaximumLength`, `LengthBetween`, `Matches`, `Email`,
  `AbsoluteUrl`, `EqualTo` / `NotEqualTo` (another property), `GreaterThan`,
  `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual` (a value or another property),
  `InclusiveBetween`, `ExclusiveBetween`, `PrecisionScale`, `Must` (a predicate method of the
  object), `ValidateChild` (a nested object or each item of a collection, for Blazor, which does
  not recurse; the server does).
- **The rules, fluent:** a DTO derives from `Validated<T>` and declares cross-field, conditional
  and collection rules once in `Rules(RuleSet<T>)`: `For(x => x.Prop).NotEmpty().Length(..)`,
  `.Equal(x => x.Other)`, `.GreaterThan(x => x.Other)`, `.Must(...)`, `.When(...)` /
  `.Unless(...)`, `.WithMessage(...)`, `ForEach(x => x.Items, item => ...)`, `Child(x => x.Nested)`,
  object-level `Must(...)`. They run through `IValidatableObject`, so both hosts see them.
- **Service-backed rules** run in the handler after the shape passed: `AsyncRules<T>` with
  `MustAsync` per member or per object, or `ValidationErrors` filled by hand, answered by
  `ApiProblems.Validation(errors)`.
- **Keys and messages.** Errors are keyed by the member's wire name (`MemberKey`: the
  `[JsonPropertyName]`, else the camel-cased property; `parent.child`, `items[2].name`); the
  admin client's forms read the map case-insensitively. Messages are invariant templates on the
  rules (`{0}` the field, then the rule's arguments), translated through
  `IValidationMessageLocalizer` when the validation context offers one: Grove.Infrastructure.Server backs it
  with the platform's string localizer (`StringLocalizerValidationMessages`), so a message
  localizes like any other string.
- **On the server.** The route groups carry ASP.NET's validation filter. The DTOs live in the
  modules, where the compile-time validation generator cannot see them, so Grove.Infrastructure.Server resolves
  their rules by reflection (`ReflectionValidatableInfoResolver`: the properties carrying rules,
  the nested objects and collections the framework recurses into, the `IValidatableObject`).
  A failing request is answered with the framework's `HttpValidationProblemDetails`, adopted as
  is because its shape is the error contract's (`errors` map, wire-name keys): no mapping filter.
- **Tests:** `Grove.Infrastructure.Server.Tests/ValidationRulesTests.cs` (each rule, both forms, the map's
  shape, the localizer) and `ApiConformanceTests.cs` (a request that breaks its rules is answered
  with the map through a mapped endpoint).

## Settings groups

A module's settings without a page of their own are a **settings group**: the module declares
the settings type once, Grove.Infrastructure.Settings serves the group's form and API, the admin shell renders
it with `SettingsForm` at `/Settings/{groupId}` ([parity.md](parity.md) › New components).

- **Declaring.** `services.AddSettingsGroup<T>(groupId, caption, permission, group => ...)`
  (Grove.Infrastructure.Settings.Abstractions) from the startup of the feature that owns the settings: the
  group's menu place (`InMenu(parent, position, icon, entryId)`), further permissions
  (`AlsoAdmits`), the tenant reload a change needs (`ReloadsTenant`), a store other than the
  site settings (`StoredBy(load, save)`: a document, a service's own store), rules that need
  services (`ValidatedBy`) and reactions to a change (`OnSaved`: an options refresh). A module
  that adds a section to another's group (a provider of the SMS settings) declares
  `services.AddSettingsModel<T>(groupId, caption, ...)`; each model of a group of several is a
  section, its values and errors under the model's key.
- **The schema is the type.** The form's fields are `T`'s public read/write properties: label,
  help text, section, order and placeholder from `[Display]`, the input from the property type
  and `[DataType]`, the rules from DataAnnotations and `Grove.Validation` (§ Validation), hidden
  with `[ScaffoldColumn(false)]`. `[Setting]` adds what those do not say: a write-only secret
  (`Secret`, stored protected under `ProtectedBy`'s data protection purpose; an empty submission
  keeps the stored value), allowed values (`Options`, an `ISettingOptions` resolved from the
  request's services), a list stored as one string (`Separator`), a field shown by another's
  value (`VisibleWhen`), an explicit editor (`Editor`: `code` with a `Language`, `json`,
  `hidden` for an id carried through the form). Enums are selects; lists of objects and nested
  objects are edited as such.
- **The API.** `api/grove/settings/groups` lists the groups the caller may manage;
  `settings/groups/{groupId}` answers the schema, the values (secrets left out, with whether each
  is set) and saves a submission: applied field by field onto the stored model, checked by the
  model's rules and then the group's, answered with the error contract's map keyed by wire name
  (`policies[2].name`, `twilioSettings.phoneNumber` in a group of several), saved only when it
  changed. A caller is admitted by any permission the group registered (`ManageGroupSettings`
  asked against the group id, `SiteSettingsPermissionMapper`).
- **The page.** Grove.Infrastructure.Settings' `SettingsGroupPages` declares one page registry entry per group,
  gated at `/Settings/{groupId}` by the group's permissions; the generic page
  (`Pages/SettingsGroup.razor`, `[PageEntry(DeclaredBySource = true)]`) serves those concrete
  routes only.
- **Tests:** `Grove.Infrastructure.Server.Tests/SettingsGroupTests.cs` (schema derivation, values and secrets,
  save and protection, rule errors by field, groups of several models, the page entries and the
  route table).

## Relation to other plans

- **[server.md](../server.md)** is the per-area inventory: each wrapper goes to the module that
  declares the surface (P2); the APIs already in modules stay there (P3). The option picker's
  API (`api/grove/option-sources`, `option-picker/attachment`) is Grove.Contents.OptionPicker's
  (server.md S2). The access gate pieces (the shell selector, the route
  gate) go to Grove.Access, where permission invalidation already is; `IRequestAccess`, the
  helper every API uses, is in Grove.Infrastructure.Server.Abstractions (P1). The Blazor host (route table,
  `App.razor`, display endpoints) stays in Grove.Infrastructure.Server.
- **[workflows.md › Operations](../workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)**:
  - Steps 5 and 6 (move the reads and the writes onto the pipelines) are executed inside each
    declaring module's area (P2-P4): when an area is converted to route groups, its reads go
    through the query pipeline and its writes through the action pipelines in the same change.
    This plan does not wait for them, and an area converted after its step 5/6 work needs no
    second pass.
  - Step 7 (generate the surfaces) is P14: generated routes are defined entries in the Server's
    API registry, where they meet the routes modules declare.
  - Step 8 (retire) is P13 for `api/content`, `api/queries` and the search APIs. Retiring the
    hand-written `api/grove/*` controllers "as each generated route lands" means: the declaring
    module keeps the API, and only its implementation becomes a generated route over its
    operations.
  - The table "The surfaces today, and their fate" there stays authoritative for fate; this
    document adds where each surface is hosted.

## Hosting

Grove's API surface is **ASP.NET minimal APIs**: not controllers, not FastEndpoints. Every API is
a route group a contributor maps (§ The seam); the engine's management API is one like any other.
No API is hosted by MVC: one gating mechanism (feature-gated startups), one antiforgery path and
one error path. There is no MVC module: the string helpers MVC carried are
`Grove.Utilities.StringExtensions` (Grove.Infrastructure.Abstractions), the admin request marker is
`AdminRequest` (Grove.Infrastructure.Shells.Admin.Abstractions, set by the shell selection for the
admin shell).

- **Explicit binding.** Every handler parameter names its binding source: `[FromBody]`,
  `[FromRoute]`, `[FromQuery]`, `[FromHeader]`, `[FromForm]`, `[AsParameters]` or
  `[FromServices]`; only the framework's own types (`HttpContext`, `ClaimsPrincipal`,
  `CancellationToken`, `HttpRequest`/`HttpResponse`, streams, form collections) need nothing.
  Minimal APIs otherwise infer the source from the type, and a service type with a `TryParse`
  or `BindAsync` member is taken for a route value: the endpoint cannot be built, and the
  failure surfaces at the tenant's first request. The conformance test
  (`Grove.Infrastructure.Server.Tests/ApiConformanceTests.cs`) maps every contributor Grove ships and fails on
  a parameter without a source, so a wrong signature fails the suite, never a tenant.
- **Request shape.** One request DTO per endpoint where it is cheap (REPR); the DTO carries
  its rules (§ Validation); services come by `[FromServices]` or through the `ApiHandlers`
  base.
- **Never a bare `HttpContext`-only lambda.** `MapGet(pattern, (HttpContext http) => ...)`
  binds to the `RequestDelegate` overload, which awaits the task and discards the `IResult`
  it returns: the endpoint answers 200 with an empty body. A handler with no bound parameters
  is mapped with `ApiHandlers.Handle<THandlers>(h => h.Method())`, which goes through the
  request delegate factory. The conformance test fails any endpoint mapped as a raw
  `RequestDelegate` whose delegate returns a result.
- **Built at mapping.** `ApiEndpointMapper` builds each contributor's endpoints right after it
  maps them: a contributor that throws is left out, logged with an `ApiContributorException`
  naming the contributor, its type, feature and the usual fix, kept in
  `IApiEndpointRegistry.Failures` and reported by the `api-registry` health check; the other
  contributors keep serving.
- Gating: minimal endpoints are mapped per tenant from feature-gated startups (§ The seam).
- Antiforgery: `ApiAntiforgeryEndpointFilter` on the registry's route groups validates the
  `RequestVerificationToken` header for cookie-authenticated, state-changing calls through the
  tenant's `IAntiforgery` (bearer callers pass); it is the only antiforgery path for APIs.
- JSON: the tenant's HTTP JSON options, with the document converters for `IDocument`,
  `IEntity` and content elements (`ApiJsonOptionsConfiguration`); there is no MVC output
  formatter.
- Errors: the error contract's mappers on the route groups, § The error contract.
- Validation: ASP.NET's native validation over Grove's ruleset, § Validation; `ModelState` is
  gone from the converted areas, and the validation sink (shape-engine.md U1) is the content
  manager's only error collector.
- OpenAPI: the endpoints explorer describes the route groups; Grove.Data.Protocols.OpenApi's documents read
  them, tagged per contributor.

| ☐ | Item | Owner | Deps | Size | Wave |
| --- | --- | --- | --- | --- | --- |
| ☑ | H1. Antiforgery endpoint filter (`ApiAntiforgeryEndpointFilter`) | Grove.Infrastructure.Server | P1 | S | 1 |
| ☑ | H2. Grove JSON options for minimal endpoints (`ApiJsonOptionsConfiguration`) | Grove.Infrastructure.Server | — | S | 1 |
| ☑ | H3. Convert areas to route groups: every area moved out of Grove.Infrastructure.Server and every in-module controller (P16) | each declaring module | H1, H2, E2, P5 | XL | 2-3 |
| ☑ | H4. `AddControllers()`/`AddMvc`, `Grove.Mvc`, `Grove.Mvc.Abstractions` and `Grove.Application.Mvc.Targets` deleted, with their package versions, solution entries and project lists | Grove.Infrastructure.Server | H3, views.md wave 4, shape-engine.md wave 4 | M | 5 |

## Plumbing

Where each piece of MVC non-UI plumbing lives after the refactor:

| ☐ | Piece | Today | After | Owner | Deps | Size | Wave |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ☑ | Per-tenant feature gating of controllers | — | gone; the seam's startup gating is the only one | — | H3 | — | 5 |
| ☑ | Global antiforgery | `Grove.Mvc/Startup.cs` (`AutoValidateAntiforgeryTokenAttribute`) | the antiforgery endpoint filter on the registry's route groups; the protocol endpoints that post forms (the OpenId consent and logout decisions) validate their form token in the handler; there is no MVC filter | Grove.Infrastructure.Server | H1 | S | 1 |
| ☑ | `IDocument` / `IEntity` JSON | — | `ApiJsonOptionsConfiguration` in Grove.Infrastructure.Server (documents, entities, content elements); the MVC output formatters and their `MvcOptions` configurations are deleted | Grove.Infrastructure.Server | H2 | S | 1 |
| ☑ | Exception filters | — | `IApiExceptionMapper` registrations (`DefinitionLockedExceptionMapper`, `WorkflowHookFailedExceptionMapper`, `ContentSaveRefusedExceptionMapper`); the filters are deleted | Grove.Contents.Types / Grove.Workflows / Grove.Contents | E2 | S | 2 |
| ☑ | Problem details | — | `ApiProblemDetailsMiddleware` and `AddProblemDetails()` in Grove.Infrastructure.Server (`Api/`, its startup); `ApiProblems`, `ApiResults` in Grove.Infrastructure.Server.Abstractions; the `HttpContext` helpers (`ApiForbidProblem`, `ApiBadRequestProblem`, `ApiNotFoundProblem`, `ApiValidationProblem`) in Grove.Infrastructure.Server.Abstractions (`ApiProblemExtensions`); the controller extensions are deleted | Grove.Infrastructure.Server(.Abstractions) | E1 | S | 2 |
| ☑ | ModelState as the content manager's sink | — | the validation sink (shape-engine.md U1, U2); the API returns its `errors` (E3) | Grove.Display.Management.Abstractions / Grove.Contents.Management | shape-engine U1 | S | 2 |
| ☑ | Request validation | hand checks in handlers, `ModelState` | ASP.NET native validation over the ruleset (§ Validation); the hand checks the rules cover are gone | Grove.Infrastructure.Server / Grove.Display.Management.Abstractions | — | M | 2 |
| ☑ | `LoginService` TempData / ActionContext | — | `LoginService.LoginAsync(HttpContext, ...)` in Grove.Access.Users; the login form events return a `LoginRefusal` (`Errors`, an optional `Redirect`; `~/` is tenant-relative, resolved by `LoginService.ResolveRedirect`) from `LoggingInAsync` and `ValidatingLoginAsync` (Grove.Access.Users.Abstractions/Events), so no `ActionContext`, `IActionResult` or TempData is involved | Grove.Access.Users | P6 | M | 3 |
| ☑ | `IUrlHelper` | — | `IShellLinkGenerator` (Grove.Infrastructure.Abstractions, `Grove.Routing`; implemented in Grove.Infrastructure over the tenant's `LinkGenerator` and the current request: `GetPathByRouteValues`, `GetUriByRouteValues`, `GetPathByName`, `Content`, `ToAbsoluteUrl`, `IsLocalUrl`) in the API handlers, the feed and sitemap builder contexts (`ContextualizeContext.Links`, `SitemapBuilderContext.Links`), the Seo handlers and the link field handler; `INavigationManager.BuildMenuAsync(name, HttpContext)` uses `LinkGenerator` ☑. The Liquid URL filters (`href`, `absolute_url`, `switch_culture_url`) use it; `display_url` uses `LinkGenerator`; the Razor users are deleted with the views | Grove.Infrastructure(.Abstractions) | — | M | 3 |
| ☑ | `ShellRouteValuesAddressScheme` | — | in Grove.Infrastructure.Abstractions (it is routing, used by `LinkGenerator` for Autoroute, HomeRoute, Sitemaps); registered by Grove.Infrastructure's tenant routing services, last, with `RouteOptions.SuppressCheckForUnhandledSecurityMetadata` | Grove.Infrastructure | — | S | 3 |
| ☑ | `FormValueRequiredMatcherPolicy` | — | deleted with its last user (the OpenId consent and logout decisions dispatch on the posted submit key in one handler) | — | — | S | 5 |
| ☑ | `ShellFileVersionProvider` | — | `Grove.Infrastructure/Modules/ShellFileVersionProvider.cs` (`Grove.Modules`), the tenant's `IFileVersionProvider`, registered by Grove.Infrastructure without MVC, for `Grove.Display.Resources.Management` and Media's URL helpers | Grove.Infrastructure | — | S | 3 |
| ☑ | `ApiAuthenticationHandler` | — | Grove.Access (`Services/ApiAuthenticationHandler.cs`), whose startup registers the "Api" scheme (Grove.Access is always enabled) | Grove.Access | — | S | 3 |
| ☑ | Area routing (`IAreaControllerRouteMapper`, `[Admin]` routes) | — | gone; no API or protocol endpoint maps an area route | — | views.md wave 4 | — | 5 |

## Waves

| Wave | Content | Depends on | Exit |
| --- | --- | --- | --- |
| 0 | views.md's remaining open module rows (Grove.Display.Rules, Grove.Infrastructure.Server.ReverseProxy, Grove.Data.Protocols.Https) | user | decisions recorded in views.md |
| 1 | P1, P5, E1, H1, H2 | — | Grove.Infrastructure.Server.Abstractions exists; a contributor can register a gated route group |
| 2 | P2 (first areas to their declaring modules), P3 (registered in place), E2, E3; plumbing: exception filters, problem details, the content manager's sink; H3 for these areas | shape-engine.md U1 | the platform modules' APIs registered through the seam; one error contract |
| 3 | P2 (remaining areas), P4, P6, P7, P8, P9, P12; plumbing: `LoginService`, `IUrlHelper`, address scheme, file versions, `ApiAuthenticationHandler`; H3 for the remaining areas | views.md waves 1-3 (the MVC callers of the JSON actions and of `api/media/*`) | every declared API in the Server's API registry |
| 4 | P10, P11, P13, P15 | views.md wave 4, workflows.md steps 5-7 | no machine endpoint on MVC; retired surfaces gone; workflow endpoints are defined entries |
| 5 | H4 (delete `AddControllers()`, `Grove.Mvc*`), the remaining plumbing deletions; P14 starts | views.md wave 4, shape-engine.md wave 4 | see exit criterion |

Cross-workstream: views.md's ports each need their API in the matching wave here (wave 2 for
settings and admin APIs, wave 3 for the access screens' APIs, P6); shape-engine.md wave 2 needs
E1/E3 for the validation sink; shape-engine.md wave 4 and views.md wave 4 are what allow H4.

## Exit criterion

- Every API endpoint in `src/` is declared by its module and registered in Grove.Infrastructure.Server's API
  registry (declared through `IApiContributor`, or as a defined entry), all listed in the
  per-tenant registry and the OpenAPI document.
- No API code in `Grove.Data*`; Grove.Infrastructure.Server holds no API beyond the host's own endpoints.
- One error contract (problem details with `errors`), one antiforgery path, one JSON
  configuration.
- No `Controller` or `ControllerBase` class in `src/`, `AddMvc`/`AddControllers` gone,
  `Grove.Mvc` and `Grove.Mvc.Abstractions` deleted, the setup shell included.
