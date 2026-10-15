# MVC retirement › API: modules declare, Grove.Server registers and serves

Workstream (c) of the MVC retirement, done together with moving the APIs out of Grove.Server.
This document is the tracker for every **API surface** in `src/`: attribute-routed
controllers, minimal-API endpoints, the workflow engine's management API, SignalR hubs and the
machine (protocol) endpoints, and for the MVC plumbing those surfaces still depend on. Siblings:

- [views.md](views.md) (workstream a): every view surface to a Blazor page. Each port there is a
  page plus an API call; the API side of each port is tracked here.
- [shape-engine.md](shape-engine.md) (workstream b): drivers, shape templates, `IHtmlDisplay`, the
  fallback. It depends on this document's **error contract** (§ The error contract, its U1) and
  on the content and settings editor APIs.
- [server.md](../server.md): the per-area inventory of what leaves Grove.Server and where it goes.
- [workflows.md › Operations](../workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery):
  steps 5-8 (move reads, move writes, generate surfaces, retire) are partly absorbed here.

Status: the seam, the defined layer and the moves out of Grove.Server are built (☑ below);
the in-module controllers are listed in the registry and converted later. Sizes: **S** (hours), **M** (a day),
**L** (several days), **XL** (a subsystem). Every tracked item names an owner module, its
dependencies, a size and a wave.

## Principles

- **The MVC layer goes**, in three workstreams: (a) views to Blazor components, (b) the shape
  engine, (c) the API layer (this document).
- **Modules declare, Grove.Server registers and serves**
  ([architecture.md](../architecture.md#modules-declare-groveserver-registers-and-serves)).
  Every API is declared by the module whose surface it is and registered in Grove.Server's API
  registry; Grove.Server keeps only the hosting plumbing (§ Placement).
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
  page. Where shared screens' defaults live is [views.md](views.md)'s open question.
- **Hosting: minimal APIs, staged** (§ Hosting).

## The inventory

Counts from a scan of `src/` on 2026-10-14 (excluding `obj`, tests, the vendored engine where
noted).

| Kind | Count | Where |
| --- | --- | --- |
| Attribute-routed `ControllerBase` APIs | 56 classes | Grove.Server 11 (admin menu and navigation, icons, option picker, the host's app and routing); 25 other modules 45 |
| Route groups (`IApiContributor`) | 43 contributors | every API moved out of Grove.Server, the hubs, the engine's management API (§ The seam) |
| Minimal-API endpoint files | 30 (28 routes + 2 file-serving) | Media 18, Contents 3, Users 2, Facebook 1, Liquid 1, Notifications 1, UrlRewriting 1, Media.Scanning 1, Media file serving 1, Grove.Server 1 |
| Workflow engine management API (vendored engine) | 72 endpoints | minimal APIs under `grove-workflows/api`, declared by `WorkflowsManagementApi` ([workflows.md](../workflows.md)) |
| SignalR hubs | 4 | Grove.Server 2 (permissions, admin-menu layout), Media 1, engine 1 (workflow instances) |
| Middleware / protocol endpoints | 9 | GraphQL `/api/graphql`, OpenIddict `/connect/*`, health checks, robots.txt, media files and image processing, secure media, workflow engine security |
| MVC `Controller` classes with machine (non-view) actions | 19 | feeds, sitemaps, remote deployment, XmlRpc, OpenId user info, pickers' search, culture picker, list ordering, export files |
| MVC `Controller` classes returning views | 53 | [views.md](views.md) |

### Attribute-routed APIs

Engine = what the controller calls (scan of its constructor and body): `content-manager`
(`IContentManager`), `definitions` (`IContentDefinitionManager`), `yessql` (`ISession` /
`QueryIndex`), `site-settings`, `documents` (`IDocumentManager`), `identity` (user and role
managers), `access` (`IRequestAccess`, `IAccessDecision`, scope sets), `shells`, `file-store`,
`queries`, `workflow-engine`, `services` (a module service that itself calls one of these).

| Module | Controller | Route prefix | Actions | Engine |
| --- | --- | --- | --- | --- |
| Grove.AuditTrail | `AuditTrailApiController` | `api/grove/audit` | 5 | access |
| Grove.AuditTrail | `AuditTrailSettingsApiController` | `api/grove/audit/settings` | 2 | site-settings |
| Grove.BackgroundTasks | `BackgroundTasksApiController` | `api/grove/background-tasks` | 3 | services |
| Grove.ContentFields | `UserPickerApiController` | `api/grove/content-fields/users` | 1 | definitions |
| Grove.ContentPartLists | `ContentPartListsController` | `api/grove/content-part-lists` | 16 | services |
| Grove.ContentPartLists | `GlobalListsController` | `api/grove/global/lists` | 4 | yessql |
| Grove.Contents | `AuditTrailContentApiController` | `api/grove/audit/content/{eventId}` | 2 | content-manager, yessql |
| Grove.Demo | `ContentApiController` | `api/demo` | 1 | content-manager |
| Grove.Elasticsearch | `ElasticsearchApiController` | `api/elasticsearch` | 4 | queries |
| Grove.Email.Smtp | `SmtpSettingsApiController` | `api/grove/settings/email/smtp` | 2 | site-settings |
| Grove.Email | `EmailSettingsApiController` | `api/grove/settings/email` | 3 | site-settings |
| Grove.Layers | `LayersApiController` | `api/grove/layers` | 5 | services |
| Grove.Lucene | `LuceneApiController` | `api/lucene` | 4 | queries |
| Grove.Media.Scanning | `ScanningSettingsController` | `api/grove/media/scanning/settings` | 2 | services |
| Grove.Media | `FilesController` | `api/files` | 26 | access |
| Grove.Members | `MemberPortalController` | `api/grove/members/portal` | 6 | content-manager, site-settings, identity |
| Grove.Members | `MembersController` | `api/grove/members` | 20 | yessql, site-settings, identity, access |
| Grove.Notifications | `PortalNotificationsController` | `api/grove/notifications` | 5 | yessql |
| Grove.OpenId | `OpenIdAdminApi` | `api/grove/openid/applications` | 14 | identity, shells |
| Grove.OpenId | `OpenIdConsentApi` | `api/grove/openid/consent` | 2 | services |
| Grove.Parties | `PartyAssociationsController` | `api/grove/parties/associations` | 1 | services |
| Grove.Parties | `PartyContactsController` | `api/grove/parties/{partyId}` | 7 | content-manager |
| Grove.Parties | `PartyNamesController` | `api/grove/parties/{partyId}/names` | 4 | content-manager |
| Grove.Parties | `PartyPositionsController` | `api/grove/parties/{partyId}` | 5 | content-manager |
| Grove.Parties | `PartyRolesController` | `api/grove/parties` | 4 | content-manager, site-settings, access |
| Grove.Parties | `PartyTypesController` | `api/grove/parties` | 1 | services |
| Grove.Placements | `PlacementsApiController` | `api/grove/placements` | 3 | services |
| Grove.Queries | `QueryApiController` | `api/queries` | 2 | queries |
| Grove.ReCaptcha | `ReCaptchaApiController` | `api/grove/recaptcha` | 1 | services |
| Grove.Regions | `AddressingMapsController` | `api/grove/regions/addressing-maps` | 4 | yessql |
| Grove.Regions | `GeoImportController` | `api/grove/regions/geo/import` | 4 | yessql |
| Grove.Server | `AdminMenuLayoutExportController` | `api/grove/admin-menu-layout` | 1 | services |
| Grove.Server | `AdminMenusController` | `api/grove/admin-menus` | 21 | services |
| Grove.Server | `AppController` | `api/grove/app` | 1 | site-settings, identity |
| Grove.Server | `IconProvidersController` | `api/grove/icons/providers` | 3 | services |
| Grove.Server | `IconifyServerController` | `api/grove/iconify` | 4 | services |
| Grove.Server | `IconsController` | `api/grove/icons` | 1 | services |
| Grove.Server | `NavigationController` | `api/grove/navigation` | 5 | access |
| Grove.Server | `OptionPickerAttachmentsController` | `api/grove/option-picker/attachment` | 3 | definitions |
| Grove.Server | `OptionSourcesController` | `api/grove/option-sources` | 6 | definitions |
| Grove.Server | `RoutingController` | `api/grove/routing` | 1 | services |
| Grove.Server | `TenantIconsController` | `api/grove/icons/tenant` | 3 | services |
| Grove.Sharing | `SharingController` | `api/sharing` | 12 | access |
| Grove.Tenants | `TenantApiController` | `api/tenants` | 6 | shells |
| Grove.Users | `AccountApiController` | `api/grove/account` | 9 | site-settings, identity |
| Grove.Users | `UserSettingsApiController` | `api/grove/settings/users` | 6 | site-settings |
| Grove.Workflows | `ApprovalsController` | `api/grove/workflows/approvals` | 3 | workflow-engine |
| Grove.Workflows | `ConnectionOAuthController` | `api/grove/workflows/connections` | 2 | workflow-engine |
| Grove.Workflows | `ConnectorControllers` | `api/grove/workflows/connections, …/webhooks` | 7 | identity, access, workflow-engine |
| Grove.Workflows | `WorkflowTriggersController` | `api/grove/workflows/triggers` | 2 | workflow-engine |
| Grove.Workflows | `WorkflowDisplayActions` | `api/grove/workflows/actions` | 1 | access, workflow-engine |
| Grove.Workflows | `FieldDependenciesController` | `api/grove/workflows/definitions` | 1 | workflow-engine |
| Grove.Workflows | `HooksController` | `api/grove/workflows/hooks` | 4 | workflow-engine |
| Grove.Workflows | `WorkflowRegistryController` | `api/grove/workflows/registry` | 3 | workflow-engine |
| Grove.Workflows | `WorkflowDefinitionAccess` | `api/grove/workflows/definitions` | 2 | content-manager, yessql, access, workflow-engine |
| Grove.Workflows | `PendingController` | `api/grove/workflows/pending` | 1 | yessql, workflow-engine |

Not in the table: the vendored engine's management API (below) and the
designer.

### Minimal-API endpoints

| Module | Routes | Notes |
| --- | --- | --- |
| Grove.Media (`Endpoints/Api/*`, 17 files) | `api/media/*` (GetMediaItems, Upload, Move, Delete, folders, Tus info, localizations, ...) | the stock media library API the MVC media picker uses; the native file system is `api/files` (`FilesController`) |
| Grove.Media (`Files/FileServingEndpoint.cs`) | `{prefix}/{placementId}[/v/{version}]` | file serving with access checks (docs/media.md); protocol |
| Grove.Contents (`Endpoints/Api/*`) | `api/content`, `api/content/{id}` (GET, POST, DELETE) | the platform content API; retired by workflows.md step 8 |
| Grove.Users (`Endpoints/*Authenticator/SendCode.cs`) | `TwoFactor-Authenticator/EmailSendCode`, `SmsSendCode` | sign-in machinery (Access) |
| Grove.Notifications (`Endpoints/Management/MarkAsReadEndpoints.cs`) | `Notifications/MarkAsRead` | the legacy navbar's; replaced by `api/grove/notifications` |
| Grove.UrlRewriting | `url-rewriting/resort` | the MVC rules screen's reorder |
| Grove.Media.Scanning | verdict callback | machine (external scanner); protocol |
| Grove.Liquid | `Grove.Liquid/Scripts/liquid-intellisense.js` | a generated script for the editors |
| Grove.Facebook | `/Grove.Facebook/sdk/*.js` | generated scripts |
| Grove.Server (`Endpoints/BlazorFrameworkScriptEndpoints.cs`) | `/_framework/{fileName}` | the host's Blazor framework scripts |
| Grove.Server (`Api/DisplayApi.cs`) | `api/grove/site/page`, `pages/{id}/preview`, `content-items/{id}/editor`, `custom-settings/{type}/editor` | the host's display endpoints, a route group |

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
| `AdminMenuLayoutHub` | `/api/grove/admin-menu-layout` | Grove.Server (`Services/AdminMenuLayoutInvalidation.cs`) |
| `MediaHub` | `/hubs/media` | Grove.Media (`Hubs/MediaHub.cs`, `MediaHubApi`) |
| `WorkflowInstanceHub` | engine real-time | vendored engine |

Hubs need no MVC. The caller lifetime on hubs is already Grove.Access's
(`ConnectionCallerHubFilter`).

### Machine and protocol endpoints

| Endpoint | Module | Hosting today |
| --- | --- | --- |
| `/api/graphql` | Grove.Apis.GraphQL | middleware (`GraphQLMiddleware`) |
| `/connect/authorize`, `/connect/token`, `/connect/logout`, user info | Grove.OpenId | OpenIddict + MVC `AccessController` (views) and `UserInfoController` |
| health checks | Grove.HealthChecks | `MapHealthChecks` |
| `robots.txt` | Grove.Seo | `RobotsMiddleware` |
| media files, image processing, secure media | Grove.Media | middleware |
| RSS feeds | Grove.Feeds | MVC `FeedController` |
| sitemaps (`sitemap.xml`, index) | Grove.Sitemaps | MVC `SitemapController` |
| remote deployment import | Grove.Deployment.Remote | MVC `ImportRemoteInstanceController` (API key) |
| MetaWeblog / RSD | Grove.XmlRpc, Grove.Lists | MVC `MetaWeblogController`, `HomeController`, `RemotePublishingController` |

### MVC controllers with JSON or redirect actions (no views)

`ContentPickerAdminController`, `LocalizationSetContentPickerAdminController` (picker search),
`ContentCulturePickerController` (culture switch redirect), `ExportRemoteInstanceController`,
`ExportFileController`, `OrderController` (list ordering), `TagController` (taxonomy tags),
`AddToDeploymentPlanController`, `EmailConfirmationController`,
`AdminTwoFactorAuthenticationController`, Demo's. Each either becomes an API in its group below or
dies with the MVC screen that calls it ([views.md](views.md)).

## Grove.Data: verified clean

A scan of `Grove.Data`, `Grove.Data.Abstractions`, `Grove.Data.Global`, `Grove.Data.Types`,
`Grove.Data.YesSql` and `Grove.Data.YesSql.Abstractions` for controllers, endpoint mapping,
`IActionResult`, `HttpContext`, problem details, `Microsoft.AspNetCore.Mvc` and `api/grove`
finds **nothing**. What lives there is data-engine work and stays: documents, migrations, the
global store, the value types, and the scope compilation
(`Grove.Data.YesSql/Scoping/ScopeExpressions.cs`). The API pieces that live elsewhere today:

- the request access helper `IRequestAccess` (`Grove.Server.Abstractions/Api/RequestAccess.cs`),
  which registering modules use;
- `ProblemDetailsApiControllerExtensions` (`Grove.Abstractions`), `ApiProblemDetailsMiddleware`
  (`Grove.Infrastructure/ErrorHandling`), `DocumentSystemTextJsonOutputFormatter` and
  `MvcOptionsConfiguration` (`Grove.Infrastructure/Extensions`): API plumbing in the core
  libraries; they move to Grove.Server (§ Plumbing);
- `ApiAuthenticationHandler` (`Grove.Infrastructure/Security`): authentication, so it goes to
  Grove.Access, which owns the gate;
- the validation sink shape-engine.md puts in `Grove.Data.Abstractions` (`IValidationSink`) is
  validation, not API, so it belongs there. Grove.Server turns its errors into problem details
  (§ The error contract).

## Placement

Applied to every surface:

1. **Each module declares its own APIs** and answers them with its own services. Protocols a
   module implements (OpenIddict, GraphQL, feeds, sitemaps, file serving, scanner callback,
   health, robots, the workflow engine) are the same case.
2. **Every module registers its endpoints** into **Grove.Server's API registry** (§ The seam).
   Modules reference **Grove.Server.Abstractions** only, never Grove.Server, and Grove.Server
   references no module for its APIs, so there is no reference cycle. A disabled feature's
   registrations are not in the tenant's registry; a downstream module can override a
   registration.
3. **Grove.Server keeps only hosting plumbing**: the registries and their catalog, route
   groups, per-tenant gating, the antiforgery filter (validation asks Access), JSON options,
   the error contract, the OpenAPI document, and the host's own endpoints (the bootstrap
   aggregate `api/grove/app`, `api/grove/routing`, the Blazor framework scripts). It declares
   no domain API.
4. **Where each area goes:**

   | Surface | Module |
   | --- | --- |
   | Site settings, recipes, features, tenants, setup | `Grove.Settings`, `Grove.Recipes`, `Grove.Features`, `Grove.Tenants`, `Grove.Setup` (`Grove.Infrastructure` for what is shell-level) |
   | Content items | `Grove.Contents` |
   | Content definitions: types, parts, field visibility, definition locks, content groups | `Grove.ContentTypes` |
   | Users | `Grove.Users` |
   | Roles | `Grove.Roles` |
   | The access model: permission sets, groups, overrides | `Grove.Access` |
   | Sign-in, account, two-factor, antiforgery, login settings | the Access modules (`Grove.Users` for sign-in, account and two-factor; `Grove.Access` for antiforgery) |
   | Media | `Grove.Media` |
   | Other settings | each its module: admin settings, title bar and the admin theme preference `Grove.Admin`; debug `Grove.Settings`; security headers `Grove.Security` |
   | Workflow management | `Grove.Workflows` |
   | Everything else (menus, icons, localization, templates, indexes, queries, ...) | the module it serves ([server.md](../server.md) § 1) |

5. **Grove.Server.Abstractions** holds what a registering module needs: the registration
   interfaces (`Api/`), route conventions, `IRequestAccess`, the error contract types, the
   handler base (`ApiHandlers`), and the page registry's contracts (`Routing/`). Each moved
   API's models moved with it into its module.
6. **Not here:** database connections (Grove.Data, Grove.Data.YesSql) and search indexes
   (`Grove.Indexing` profiles, registered by their module like any other surface).

### Tracker

Owner is where the code ends up. Wave numbers are § Waves.

| ☐ | Item | Today | Target | Owner | Deps | Size | Wave |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ☑ | P1. Grove.Server.Abstractions project | — | `Api/`: `IApiContributor` / `ApiContributor`, `ApiRoutes`, `IRequestAccess`, `IApiExceptionMapper`, `ApiProblems` / `ApiResults`, `ApiHandlers`, `IDefinedApiSource`, `IDefinedApiInvalidator`, `IApiEndpointRegistry` | Grove.Server.Abstractions | — | M | 1 |
| ◐ | P2. The 47 Grove.Server controllers | Grove.Server, MVC-hosted | moved to their declaring modules as route groups ([server.md](../server.md) § 1); the display actions are the host's display endpoints (`Api/DisplayApi.cs`); `AppController` and `RoutingController` stay as host endpoints. Left: the admin menu and navigation (page registry), the option picker (server.md S2), the icons (a server-side icons module) | each declaring module | P1, P5 | L | 2-3 |
| ◐ | P3. Platform-module APIs already in their modules | Email, Email.Smtp, AuditTrail (2), BackgroundTasks, Layers, Placements, Notifications portal, Media.Scanning settings, Users settings, ContentFields user picker, Contents audit, Tenants `api/tenants`, ReCaptcha, Sharing, OpenId admin + consent | listed in the registry from their MVC action endpoints (`IApiEndpointRegistry.Controllers`) ☑; converted to route groups registered through the seam ☐ (P16); `api/tenants` merges with `TenantsApi` then | each module | P1, P5 | M | 2 |
| ◐ | P4. App-layer module APIs | Parties (6), Members (2), Regions (2), ContentPartLists (2), Workflows (10), Fruitful's three | listed in the registry from their MVC endpoints ☑; converted ☐ (P16); the modules reference Grove.Server.Abstractions, and Grove.Server only for the option picker and routing types until those leave | each module | P1, P5 | M | 3 |
| ☑ | P5. The seam | — | § The seam: `ApiEndpointMapper` maps the tenant's contributors into route groups; `ApiEndpointRegistry` lists them, the controllers and the defined entries | Grove.Server, Grove.Server.Abstractions | P1 | M | 1 |
| ◐ | P6. Access APIs | `api/grove/account` (Users), two-factor SendCode endpoints (Users), `EmailConfirmationController`, `AdminTwoFactorAuthenticationController` | `AuthApi`, `LoginSettingsApi`, `UsersApi` (with `LoginService`) in Grove.Users, `AntiforgeryApi` and `AccessModelApi` in Grove.Access ☑; the in-module account and two-factor surfaces with P12 and P16 ☐ | Grove.Users, Grove.Access | P5 | M | 3 |
| ☑ | P7. The workflow engine's management API | engine-hosted under `grove-workflows/api` | `WorkflowsManagementApi`, a declared contributor keeping the engine's path; minimal APIs like every contributor ([workflows.md](../workflows.md)) | Grove.Workflows | P5 | S | 3 |
| ◐ | P8. Hubs | Server 2, Media 1, engine 1 | `PermissionHubApi` (Grove.Access) and `MediaHubApi` (Grove.Media) ☑; `AdminMenuLayoutHub` goes with the admin menu (page registry) ☐; the engine's stays | each module | P5 | S | 3 |
| ◐ | P9. Media APIs | `api/files` (`FilesController`, 26 actions), `api/media/*` (17 minimal endpoints) | `api/grove/media*` is Grove.Media's (`MediaFilesApi`, `MediaOptionsApi`, `MediaProfilesApi`) ☑; `api/files` converted with P16 ☐; `api/media/*` dies with the MVC media library ([views.md](views.md)) once the native library and pickers use `api/files` ☐ | Grove.Media | P5, views.md | L | 3 |
| ☐ | P10. Setup API (only if views.md § Setup chooses a pre-database page) | MVC `SetupController` | `POST api/grove/setup`, anonymous, runs in the setup shell before any store | Grove.Setup | views.md § Setup | M | 4 |
| ☐ | P11. Protocol endpoints | GraphQL, OpenIddict, health, robots, media serving, feeds, sitemaps, remote deployment import, scanner callback, XmlRpc/RSD | stay in their modules; the MVC-hosted ones (feeds, sitemaps, remote import, OpenId user info, XmlRpc/RSD) become minimal endpoints in their module's startup | each module | H3 | M | 4 |
| ☐ | P12. MVC JSON/redirect actions | the 11 listed above | each to an API in its P2/P3/P6 group, or deleted with its MVC screen | per group | views.md | M | 3-4 |
| ☐ | P13. Retirements (workflows.md step 8) | `api/content` (Contents), `api/queries` (Queries), `api/lucene`, `api/elasticsearch`, `Notifications/MarkAsRead`, `url-rewriting/resort`, `api/demo` | deleted when their replacements land: content API (Grove.Contents, P2), query operations (workflows.md steps 5-7), search as a connection, the native notifications and rewriting pages | each declaring module | step 5-7, views.md | M | 4 |
| ☐ | P14. Generated surfaces (workflows.md step 7) | — | REST + OpenAPI over the operation registry under `api/grove/ops/*` (route fixed in P14), entered as defined entries in the Server's API registry beside the declared ones; a module's declared API may be implemented by a generated route, never replaced by a workflow endpoint; GraphQL and RPC later | Grove.Server (registry), Grove.Workflows | P15, workflows.md step 3 | XL | 5 |
| ☑ | P15. The defined layer | — | `DefinedApiEndpointDataSource`, one per tenant, reloaded after the scope that invalidated it; the workflow `HttpEndpoint` routes are `WorkflowHttpEndpointSource`'s entries served by the engine's `HttpWorkflowRequestHandler`; the engine's `IRouteTable`, `IRouteMatcher`, route table updater and `HttpWorkflowsMiddleware` are gone | Grove.Server, Grove.Workflows | P5 | M | 4 |
| ☐ | P16. Convert the in-module controllers | P3, P4 and Fruitful's, listed from their MVC endpoints | each a route group registered through the seam, in the change that next touches its area | each module | H3 | L | 3-5 |

## The seam

Every module that declares an API registers it; Grove.Server holds the registry and serves it.
The registration interface, in Grove.Server.Abstractions (`Api/IApiContributor.cs`):

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
  Grove.Server's `ConfigureAsync`), never stored. *Defined* entries are data: an
  `IDefinedApiSource` registered in the tenant (`services.AddDefinedApiSource<T>()`) returns
  `DefinedApiEntry`s (route pattern, methods, request delegate, metadata, order), served by the
  tenant's `DefinedApiEndpointDataSource`. `IDefinedApiInvalidator.Invalidate(source)` (a publish)
  reloads the sources after the current shell scope completes, so the reload reads committed
  data; a reload that yields the same routes leaves the endpoints alone, and one failing source
  leaves the others served. The workflow `HttpEndpoint` routes are the first defined source
  (P15); generated operation endpoints join them (P14). This mirrors the page registry's
  declared and data-defined sources (`Grove.Server.Abstractions/Routing`).
- **Per tenant.** Contributors and sources are registered in the tenant's container from the
  startups of enabled features, and the data source is a singleton of the tenant's container
  added to the tenant's own route builder: a disabled feature's endpoints are not in the
  tenant's route table, and one tenant's defined entries never route in another.
- **What the group carries** (applied once by Grove.Server): the prefix, the antiforgery
  endpoint filter (`ApiAntiforgeryEndpointFilter`: a state-changing call validates the
  `RequestVerificationToken` header through the tenant's `IAntiforgery`, which passes bearer
  callers; `.SkipApiAntiforgery()` exempts an endpoint), the exception mapping
  (`ApiExceptionEndpointFilter`, § The error contract), the OpenAPI tag (the contributor's name)
  and `ApiContributorMetadata`. JSON is the tenant's HTTP JSON options, with documents and
  entities written with the document serializer options (`ApiJsonOptionsConfiguration`).
- **The API registry.** `IApiEndpointRegistry` lists the declared contributors with their
  routes, the controllers still hosted by MVC (from their action endpoints under `api/`), and
  the defined entries. The OpenAPI document reads the endpoints through the endpoints explorer.
- **Explicit binding, built at mapping.** Every handler parameter names its binding source,
  and each contributor's endpoints are built as it maps them; one that cannot be built is left
  out and reported (§ Hosting).
- **Controllers during the transition.** An attribute-routed controller in a module stays
  until its area is converted (P16, § Hosting); its gating stays the MVC application-part
  gating, and the registry lists it.

## The error contract

One shape for every API error, so shape-engine.md's validation sink (U1) and the content and
settings APIs agree:

- `application/problem+json` (RFC 9457) with `type`, `title`, `status`, `detail`, and an
  `errors` map `{ key: [messages] }` for validation: the key is the member's wire name (the JSON
  name the client sent, `items[2].name` for nested members, `""` for the object), the same for a
  request DTO's rules (§ Validation), a handler's own checks (`ApiProblems.Validation`) and the
  sink's errors (keyed by field path `Part.Field` or by setting name).
- Mapped exceptions, registered by the module that owns the exception
  (`services.AddApiExceptionMapper<T>()`, `IApiExceptionMapper` in Grove.Server.Abstractions):
  `DefinitionLockedException` → 409 (`DefinitionLockedExceptionMapper`, Grove.ContentTypes),
  `WorkflowHookFailedException` → 409 with the slot and the attachment
  (`WorkflowHookFailedExceptionMapper`, Grove.Workflows); to come: `ContentSaveRefusedException`
  → 400 with `errors`, `ExchangeRateUnavailableException` → 409, access denials → 403 or 404
  (deny-as-not-found) from the decision.
- One set of mappers for every host: the route groups' `ApiExceptionEndpointFilter` and, for
  the controllers still hosted by MVC, `ApiExceptionMvcFilter` (goes with `AddControllers()`).
  `ApiProblemDetailsMiddleware` (`Grove.Infrastructure/ErrorHandling`, host level) writes the
  body of a bare 401 and 403.

| ☐ | Item | Owner | Deps | Size | Wave |
| --- | --- | --- | --- | --- | --- |
| ☑ | E1. Error contract types and `IApiExceptionMapper` | Grove.Server.Abstractions | P1 | S | 1 |
| ◐ | E2. The mappers on route groups and MVC; the two MVC exception filters deleted ☑. The problem-details middleware stays host level until the tenant pipeline can wrap the authorization middleware ☐ | Grove.Server | E1 | S | 2 |
| ☐ | E3. Validation sink errors (shape-engine.md U1) rendered as `errors` | Grove.Server | E1, shape-engine U1 | S | 2 |

## Validation

Request validation is **ASP.NET's native validation** (`services.AddValidation()`, per tenant,
in Grove.Server's startup) over **Grove's own ruleset**: the rule catalog FluentValidation
offers where DataAnnotations is thin, written as `ValidationAttribute`s so the same rules run in
ASP.NET's validation filter and in Blazor's `DataAnnotationsValidator` unchanged.

- **Home:** `Grove.DisplayManagement.Abstractions/Validation`, namespace `Grove.Validation`: the
  smallest assembly both the WebAssembly client (through Grove.Components) and every API module
  (through Grove.Server.Abstractions) reach, so a DTO shared with the client carries the same
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
  `IValidationMessageLocalizer` when the validation context offers one: Grove.Server backs it
  with the platform's string localizer (`StringLocalizerValidationMessages`), so a message
  localizes like any other string.
- **On the server.** The route groups carry ASP.NET's validation filter. The DTOs live in the
  modules, where the compile-time validation generator cannot see them, so Grove.Server resolves
  their rules by reflection (`ReflectionValidatableInfoResolver`: the properties carrying rules,
  the nested objects and collections the framework recurses into, the `IValidatableObject`).
  A failing request is answered with the framework's `HttpValidationProblemDetails`, adopted as
  is because its shape is the error contract's (`errors` map, wire-name keys): no mapping filter.
- **Tests:** `Grove.Server.Tests/ValidationRulesTests.cs` (each rule, both forms, the map's
  shape, the localizer) and `ApiConformanceTests.cs` (a request that breaks its rules is answered
  with the map through a mapped endpoint).

## Relation to other plans

- **[server.md](../server.md)** is the per-area inventory: each wrapper goes to the module that
  declares the surface (P2); the APIs already in modules stay there (P3). The option picker's
  API (`api/grove/option-sources`, `option-picker/attachment`) goes with the option picker to
  whichever module server.md S2 picks. The access gate pieces (the shell selector, the route
  gate) go to Grove.Access, where permission invalidation already is; `IRequestAccess`, the
  helper every API uses, is in Grove.Server.Abstractions (P1). The Blazor host (route table,
  `App.razor`, display endpoints) stays in Grove.Server.
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
The MVC module (`Grove.Mvc`, `Grove.Mvc.Abstractions`, `AddMvc`) goes with the last controller
(H4): `AddControllers()` stays only while the in-module controllers are converted (P16), because
two hosting models would mean two gating mechanisms, two antiforgery paths and two error paths.

- **Explicit binding.** Every handler parameter names its binding source: `[FromBody]`,
  `[FromRoute]`, `[FromQuery]`, `[FromHeader]`, `[FromForm]`, `[AsParameters]` or
  `[FromServices]`; only the framework's own types (`HttpContext`, `ClaimsPrincipal`,
  `CancellationToken`, `HttpRequest`/`HttpResponse`, streams, form collections) need nothing.
  Minimal APIs otherwise infer the source from the type, and a service type with a `TryParse`
  or `BindAsync` member is taken for a route value: the endpoint cannot be built, and the
  failure surfaces at the tenant's first request. The conformance test
  (`Grove.Server.Tests/ApiConformanceTests.cs`) maps every contributor Grove ships and fails on
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
  tenant's `IAntiforgery` (bearer callers pass); it replaces the global MVC filter.
- JSON: the tenant's HTTP JSON options, with the document converters for `IDocument` /
  `IEntity` (`ApiJsonOptionsConfiguration`).
- Errors: the error contract's mappers on the route groups (and on the remaining controllers
  through `ApiExceptionMvcFilter`), § The error contract.
- Validation: ASP.NET's native validation over Grove's ruleset, § Validation; `ModelState` is
  gone from the converted areas, and the validation sink (shape-engine.md U1) is the content
  manager's only error collector.
- OpenAPI: the endpoints explorer describes the route groups; Grove.OpenApi's documents read
  them, tagged per contributor.

| ☐ | Item | Owner | Deps | Size | Wave |
| --- | --- | --- | --- | --- | --- |
| ☑ | H1. Antiforgery endpoint filter (`ApiAntiforgeryEndpointFilter`) | Grove.Server | P1 | S | 1 |
| ☑ | H2. Grove JSON options for minimal endpoints (`ApiJsonOptionsConfiguration`) | Grove.Server | — | S | 1 |
| ◐ | H3. Convert areas to route groups: every area moved out of Grove.Server ☑; the in-module controllers (P16) ☐ | each declaring module | H1, H2, E2, P5 | XL | 2-3 |
| ☐ | H4. Delete `AddControllers()`, `Grove.Mvc`, `Grove.Mvc.Abstractions` | Grove.Server | H3, views.md wave 4, shape-engine.md wave 4 | M | 5 |

## Plumbing

Where each piece of MVC non-UI plumbing lives after the refactor:

| ☐ | Piece | Today | After | Owner | Deps | Size | Wave |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ☐ | Per-tenant feature gating of controllers | `Grove.Mvc/ShellFeatureApplicationPart.cs`, `ModularApplicationModelProvider.cs` | gone; the seam's startup gating | — | H3 | — | 5 |
| ◐ | Global antiforgery | `Grove.Mvc/Startup.cs:93` (`AutoValidateAntiforgeryTokenAttribute`) | the antiforgery endpoint filter on the registry's route groups ☑; the global MVC filter goes with the last controller ☐ | Grove.Server | H1 | S | 1 |
| ◐ | `IDocument` / `IEntity` JSON | `Grove.Infrastructure/Extensions/DocumentSystemTextJsonOutputFormatter.cs`, `MvcOptionsConfiguration.cs:18` | `ApiJsonOptionsConfiguration` in Grove.Server for the route groups ☑; the MVC formatter goes with the last controller ☐ | Grove.Server | H2 | S | 1 |
| ☑ | Exception filters | — | `IApiExceptionMapper` registrations (`DefinitionLockedExceptionMapper`, `WorkflowHookFailedExceptionMapper`); the filters are deleted | Grove.ContentTypes / Grove.Workflows | E2 | S | 2 |
| ☐ | Problem details | `Grove.Abstractions/ProblemDetailsApiControllerExtensions.cs`, `Grove.Infrastructure/ErrorHandling/ApiProblemDetailsMiddleware.cs` | Grove.Server (middleware, handler) and Grove.Server.Abstractions (types); the controller extensions deleted with the controllers | Grove.Server(.Abstractions) | E1 | S | 2 |
| ☐ | ModelState as the content manager's sink | `Grove.ContentManagement/DefaultContentManager.cs:408, 412, 480` via `IUpdateModelAccessor` (`Grove.Mvc.Abstractions`) | the validation sink (shape-engine.md U1, U2); the API returns its `errors` | Grove.Data.Abstractions / Grove.ContentManagement | shape-engine U1 | S | 2 |
| ☑ | Request validation | hand checks in handlers, `ModelState` | ASP.NET native validation over the ruleset (§ Validation); the hand checks the rules cover are gone | Grove.Server / Grove.DisplayManagement.Abstractions | — | M | 2 |
| ◐ | `LoginService` TempData / ActionContext | `Grove.Users/Services/LoginService.cs` (translates `ILoginFormEvent` vetoes from redirect results and TempData) | in Grove.Users ☑; the login events return a refusal (`LoginRefusal`) instead of an `IActionResult` + TempData, so no `ActionContext` is needed ☐ (`AuthApi` builds one for now) | Grove.Users | P6 | M | 3 |
| ☐ | `IUrlHelper` | 23 files (Navigation, Sitemaps, Seo handlers, Liquid filters, tag helpers, link field driver, ...) | `LinkGenerator` with a shell-aware helper (`IShellLinkGenerator`) in Grove.Abstractions; the Razor-only users (tag helpers, `UrlHelperExtensions`) go with the views | Grove.Abstractions | — | M | 3 |
| ☐ | `ShellRouteValuesAddressScheme` | `Grove.Abstractions/Routing/`, registered from `Grove.Mvc/Extensions/PlatformBuilderExtensions.cs` | stays in Grove.Abstractions (it is routing, used by `LinkGenerator` for Autoroute, HomeRoute, Sitemaps); its registration moves to Grove.Infrastructure | Grove.Infrastructure | — | S | 3 |
| ☐ | `FormValueRequiredMatcherPolicy` | `Grove.Abstractions/Routing/`, registered in `Grove.Mvc/Startup.cs:102` | gone (form-post submit-button routing for MVC screens) | — | views.md wave 4 | S | 5 |
| ☐ | `ShellFileVersionProvider` | `Grove.Mvc/ShellFileVersionProvider.cs` | `IFileVersionProvider` stays needed by `Grove.ResourceManagement` and Media's URL helpers (`MediaEndpointHelpers`, `ImageShortcodeProvider`); the shell-aware implementation moves to Grove.Infrastructure, registered without MVC | Grove.Infrastructure | — | S | 3 |
| ☐ | `ApiAuthenticationHandler` | `Grove.Infrastructure/Security/ApiAuthenticationHandler.cs` | Grove.Access (the gate owns authentication) | Grove.Access | — | S | 3 |
| ☐ | Area routing (`IAreaControllerRouteMapper`, `[Admin]` routes) | `Grove.Mvc/Startup.cs:154` | gone with the MVC screens | — | views.md wave 4 | — | 5 |

## Waves

| Wave | Content | Depends on | Exit |
| --- | --- | --- | --- |
| 0 | views.md's open questions (shared screens, setup) answered | user | answers recorded in views.md |
| 1 | P1, P5, E1, H1, H2 | — | Grove.Server.Abstractions exists; a contributor can register a gated route group |
| 2 | P2 (first areas to their declaring modules), P3 (registered in place), E2, E3; plumbing: exception filters, problem details, the content manager's sink; H3 for these areas | shape-engine.md U1 | the platform modules' APIs registered through the seam; one error contract |
| 3 | P2 (remaining areas), P4, P6, P7, P8, P9, P12; plumbing: `LoginService`, `IUrlHelper`, address scheme, file versions, `ApiAuthenticationHandler`; H3 for the remaining areas | views.md waves 1-3 (the MVC callers of the JSON actions and of `api/media/*`) | every declared API in the Server's API registry |
| 4 | P10 (if views.md § Setup chooses it), P11, P13, P15 | views.md wave 4, workflows.md steps 5-7 | no machine endpoint on MVC; retired surfaces gone; workflow endpoints are defined entries |
| 5 | H4 (delete `AddControllers()`, `Grove.Mvc*`), the remaining plumbing deletions; P14 starts | views.md wave 4, shape-engine.md wave 4 | see exit criterion |

Cross-workstream: views.md's ports each need their API in the matching wave here (wave 2 for
settings and admin APIs, wave 3 for the access screens' APIs, P6); shape-engine.md wave 2 needs
E1/E3 for the validation sink; shape-engine.md wave 4 and views.md wave 4 are what allow H4.

## Exit criterion

- Every API endpoint in `src/` is declared by its module and registered in Grove.Server's API
  registry (declared through `IApiContributor`, or as a defined entry), all listed in the
  per-tenant registry and the OpenAPI document.
- No API code in `Grove.Data*`; Grove.Server holds no API beyond the host's own endpoints.
- One error contract (problem details with `errors`), one antiforgery path, one JSON
  configuration.
- No `Controller` or `ControllerBase` class in `src/`, `AddMvc`/`AddControllers` gone,
  `Grove.Mvc` and `Grove.Mvc.Abstractions` deleted; the setup shell is the only exception if
  views.md § Setup keeps setup on MVC.
