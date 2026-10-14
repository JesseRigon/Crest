# MVC retirement › API: modules declare, Crest.Server registers and serves

Workstream (c) of the MVC retirement, done together with moving the APIs out of Crest.Server.
This document is the tracker for every **API surface** in `src/`: attribute-routed
controllers, minimal-API endpoints, the workflow engine's FastEndpoints, SignalR hubs and the
machine (protocol) endpoints, and for the MVC plumbing those surfaces still depend on. Siblings:

- [views.md](views.md) (workstream a): every view surface to a Blazor page. Each port there is a
  page plus an API call; the API side of each port is tracked here.
- [shape-engine.md](shape-engine.md) (workstream b): drivers, shape templates, `IHtmlDisplay`, the
  fallback. It depends on this document's **error contract** (§ The error contract, its U1) and
  on the content and settings editor APIs.
- [server.md](../server.md): the per-area inventory of what leaves Crest.Server and where it goes.
- [workflows.md › Operations](../workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery):
  steps 5-8 (move reads, move writes, generate surfaces, retire) are partly absorbed here.

Status: planning. Nothing here is built unless marked ☑. Sizes: **S** (hours), **M** (a day),
**L** (several days), **XL** (a subsystem). Every tracked item names an owner module, its
dependencies, a size and a wave.

## Principles

- **The MVC layer goes**, in three workstreams: (a) views to Blazor components, (b) the shape
  engine, (c) the API layer (this document).
- **Modules declare, Crest.Server registers and serves**
  ([architecture.md](../architecture.md#modules-declare-crestserver-registers-and-serves)).
  Every API is declared by the module whose surface it is and registered in Crest.Server's API
  registry; Crest.Server keeps only the hosting plumbing (§ Placement).
- **`Crest.Data*` does not deal with APIs.** It is the type manager, the validation engine and
  the database connector (the host database and external databases as SQL connections);
  outgoing connections to other external systems are the protocol modules'
  ([queries.md › Connections](../queries.md#connections-the-protocol-modules-and-the-data-layer)). The API layer calls the data engine (content
  manager, definitions, YesSql sessions through `ScopedExecution`, the query pipeline) and the
  connection system; nothing in the data engine returns HTTP results (§ Crest.Data: verified
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
| Attribute-routed `ControllerBase` APIs | 92 classes, 486 actions | Crest.Server 47; 25 other modules 45 |
| Minimal-API endpoint files | 30 (28 routes + 2 file-serving) | Media 18, Contents 3, Users 2, Facebook 1, Liquid 1, Notifications 1, UrlRewriting 1, Media.Scanning 1, Media file serving 1, Crest.Server 1 |
| FastEndpoints (vendored engine) | 72 endpoint classes | `Crest.Workflows.Api` 68, `.Resilience` 3, `.Expressions.JavaScript` 1, under `crest-workflows/api` |
| SignalR hubs | 4 | Crest.Server 2 (permissions, admin-menu layout), Media 1, engine 1 (workflow instances) |
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
| Crest.AuditTrail | `AuditTrailApiController` | `api/crest/audit` | 5 | access |
| Crest.AuditTrail | `AuditTrailSettingsApiController` | `api/crest/audit/settings` | 2 | site-settings |
| Crest.BackgroundTasks | `BackgroundTasksApiController` | `api/crest/background-tasks` | 3 | services |
| Crest.ContentFields | `UserPickerApiController` | `api/crest/content-fields/users` | 1 | definitions |
| Crest.ContentPartLists | `ContentPartListsController` | `api/crest/content-part-lists` | 16 | services |
| Crest.ContentPartLists | `GlobalListsController` | `api/crest/global/lists` | 4 | yessql |
| Crest.Contents | `AuditTrailContentApiController` | `api/crest/audit/content/{eventId}` | 2 | content-manager, yessql |
| Crest.Demo | `ContentApiController` | `api/demo` | 1 | content-manager |
| Crest.Elasticsearch | `ElasticsearchApiController` | `api/elasticsearch` | 4 | queries |
| Crest.Email.Smtp | `SmtpSettingsApiController` | `api/crest/settings/email/smtp` | 2 | site-settings |
| Crest.Email | `EmailSettingsApiController` | `api/crest/settings/email` | 3 | site-settings |
| Crest.Layers | `LayersApiController` | `api/crest/layers` | 5 | services |
| Crest.Lucene | `LuceneApiController` | `api/lucene` | 4 | queries |
| Crest.Media.Scanning | `ScanningSettingsController` | `api/crest/media/scanning/settings` | 2 | services |
| Crest.Media | `FilesController` | `api/files` | 26 | access |
| Crest.Members | `MemberPortalController` | `api/crest/members/portal` | 6 | content-manager, site-settings, identity |
| Crest.Members | `MembersController` | `api/crest/members` | 20 | yessql, site-settings, identity, access |
| Crest.Notifications | `PortalNotificationsController` | `api/crest/notifications` | 5 | yessql |
| Crest.OpenId | `OpenIdAdminApi` | `api/crest/openid/applications` | 14 | identity, shells |
| Crest.OpenId | `OpenIdConsentApi` | `api/crest/openid/consent` | 2 | services |
| Crest.Parties | `PartyAssociationsController` | `api/crest/parties/associations` | 1 | services |
| Crest.Parties | `PartyContactsController` | `api/crest/parties/{partyId}` | 7 | content-manager |
| Crest.Parties | `PartyNamesController` | `api/crest/parties/{partyId}/names` | 4 | content-manager |
| Crest.Parties | `PartyPositionsController` | `api/crest/parties/{partyId}` | 5 | content-manager |
| Crest.Parties | `PartyRolesController` | `api/crest/parties` | 4 | content-manager, site-settings, access |
| Crest.Parties | `PartyTypesController` | `api/crest/parties` | 1 | services |
| Crest.Placements | `PlacementsApiController` | `api/crest/placements` | 3 | services |
| Crest.Queries | `QueryApiController` | `api/queries` | 2 | queries |
| Crest.ReCaptcha | `ReCaptchaApiController` | `api/crest/recaptcha` | 1 | services |
| Crest.Regions | `AddressingMapsController` | `api/crest/regions/addressing-maps` | 4 | yessql |
| Crest.Regions | `GeoImportController` | `api/crest/regions/geo/import` | 4 | yessql |
| Crest.Server | `AccessModelController` | `api/crest` | 21 | identity, access |
| Crest.Server | `AdminMenuLayoutExportController` | `api/crest/admin-menu-layout` | 1 | services |
| Crest.Server | `AdminMenusController` | `api/crest/admin-menus` | 21 | services |
| Crest.Server | `AdminSettingsController` | `api/crest/admin-settings` | 2 | site-settings |
| Crest.Server | `AntiforgeryController` | `api/crest/antiforgery` | 1 | services |
| Crest.Server | `AppController` | `api/crest/app` | 1 | site-settings, identity |
| Crest.Server | `AuthController` | `api/crest/auth` | 3 | identity |
| Crest.Server | `BrandingController` | `api/crest/site/branding-settings` | 3 | site-settings, access, file-store |
| Crest.Server | `ComponentOverridesController` | `api/crest/templates/overrides` | 5 | services |
| Crest.Server | `ComponentsController` | `api/crest/components` | 2 | services |
| Crest.Server | `ContentDefinitionsController` | `api/crest/content-types` | 14 | definitions |
| Crest.Server | `ContentGroupsController` | `api/crest/content-groups` | 8 | services |
| Crest.Server | `ContentItemsController` | `api/crest/content-items` | 15 | content-manager, yessql, access |
| Crest.Server | `ContentTypesController` | `api/crest/content-types` | 9 | definitions |
| Crest.Server | `CustomSettingsController` | `api/crest/custom-settings` | 3 | content-manager, definitions, site-settings |
| Crest.Server | `DebugSettingsController` | `api/crest/settings/debugging` | 2 | site-settings, access |
| Crest.Server | `DeploymentController` | `api/crest/deployment` | 21 | yessql, access |
| Crest.Server | `DesignedPagesController` | `api/crest/pages` | 7 | services |
| Crest.Server | `FeaturesController` | `api/crest/features` | 3 | services |
| Crest.Server | `FieldVisibilityController` | `api/crest/field-visibility` | 2 | definitions |
| Crest.Server | `IconProvidersController` | `api/crest/icons/providers` | 3 | services |
| Crest.Server | `IconifyServerController` | `api/crest/iconify` | 4 | services |
| Crest.Server | `IconsController` | `api/crest/icons` | 1 | services |
| Crest.Server | `IndexesController` | `api/crest/indexes` | 9 | access |
| Crest.Server | `LocalizationController` | `api/crest/localization` | 5 | site-settings, identity |
| Crest.Server | `LoginSettingsController` | `api/crest/settings/login` | 2 | site-settings |
| Crest.Server | `MediaController` | `api/crest/media` | 4 | file-store |
| Crest.Server | `MediaOptionsController` | `api/crest/media/options` | 1 | services |
| Crest.Server | `MediaProfilesController` | `api/crest/media/profiles` | 3 | services |
| Crest.Server | `NavigationController` | `api/crest/navigation` | 5 | access |
| Crest.Server | `OptionPickerAttachmentsController` | `api/crest/option-picker/attachment` | 3 | definitions |
| Crest.Server | `OptionSourcesController` | `api/crest/option-sources` | 6 | definitions |
| Crest.Server | `QueriesController` | `api/crest/queries` | 6 | access, queries |
| Crest.Server | `RecipesController` | `api/crest/recipes` | 2 | access |
| Crest.Server | `RolesController` | `api/crest/roles` | 1 | identity |
| Crest.Server | `RoutingController` | `api/crest/routing` | 1 | services |
| Crest.Server | `SecurityHeadersController` | `api/crest/security-headers` | 2 | site-settings |
| Crest.Server | `SiteController` | `api/crest/site` | 5 | site-settings |
| Crest.Server | `StandardMenusController` | `api/crest/menus` | 11 | content-manager, definitions |
| Crest.Server | `TemplatesController` | `api/crest/templates` | 8 | content-manager, access |
| Crest.Server | `TenantIconsController` | `api/crest/icons/tenant` | 3 | services |
| Crest.Server | `TenantsController` | `api/crest/tenants` | 11 | access, shells |
| Crest.Server | `ThemeController` | `api/crest/theme` | 2 | site-settings |
| Crest.Server | `ThemesController` | `api/crest/themes` | 6 | services |
| Crest.Server | `TitleBarSettingsController` | `api/crest/title-bar-settings` | 2 | services |
| Crest.Server | `TranslationsController` | `api/crest/translations` | 2 | documents |
| Crest.Server | `UsersController` | `api/crest/users` | 12 | yessql, identity |
| Crest.Sharing | `SharingController` | `api/sharing` | 12 | access |
| Crest.Tenants | `TenantApiController` | `api/tenants` | 6 | shells |
| Crest.Users | `AccountApiController` | `api/crest/account` | 9 | site-settings, identity |
| Crest.Users | `UserSettingsApiController` | `api/crest/settings/users` | 6 | site-settings |
| Crest.Workflows | `ApprovalsController` | `api/crest/workflows/approvals` | 3 | workflow-engine |
| Crest.Workflows | `ConnectionOAuthController` | `api/crest/workflows/connections` | 2 | workflow-engine |
| Crest.Workflows | `ConnectorControllers` | `api/crest/workflows/connections, …/webhooks` | 7 | identity, access, workflow-engine |
| Crest.Workflows | `WorkflowTriggersController` | `api/crest/workflows/triggers` | 2 | workflow-engine |
| Crest.Workflows | `WorkflowDisplayActions` | `api/crest/workflows/actions` | 1 | access, workflow-engine |
| Crest.Workflows | `FieldDependenciesController` | `api/crest/workflows/definitions` | 1 | workflow-engine |
| Crest.Workflows | `HooksController` | `api/crest/workflows/hooks` | 4 | workflow-engine |
| Crest.Workflows | `WorkflowRegistryController` | `api/crest/workflows/registry` | 3 | workflow-engine |
| Crest.Workflows | `WorkflowDefinitionAccess` | `api/crest/workflows/definitions` | 2 | content-manager, yessql, access, workflow-engine |
| Crest.Workflows | `PendingController` | `api/crest/workflows/pending` | 1 | yessql, workflow-engine |

Not in the table: the vendored engine's own MVC-free endpoints (FastEndpoints, below) and the
designer.

### Minimal-API endpoints

| Module | Routes | Notes |
| --- | --- | --- |
| Crest.Media (`Endpoints/Api/*`, 17 files) | `api/media/*` (GetMediaItems, Upload, Move, Delete, folders, Tus info, localizations, ...) | the stock media library API the MVC media picker uses; the native file system is `api/files` (`FilesController`) |
| Crest.Media (`Files/FileServingEndpoint.cs`) | `{prefix}/{placementId}[/v/{version}]` | file serving with access checks (docs/media.md); protocol |
| Crest.Contents (`Endpoints/Api/*`) | `api/content`, `api/content/{id}` (GET, POST, DELETE) | the platform content API; retired by workflows.md step 8 |
| Crest.Users (`Endpoints/*Authenticator/SendCode.cs`) | `TwoFactor-Authenticator/EmailSendCode`, `SmsSendCode` | sign-in machinery (Access) |
| Crest.Notifications (`Endpoints/Management/MarkAsReadEndpoints.cs`) | `Notifications/MarkAsRead` | the legacy navbar's; replaced by `api/crest/notifications` |
| Crest.UrlRewriting | `url-rewriting/resort` | the MVC rules screen's reorder |
| Crest.Media.Scanning | verdict callback | machine (external scanner); protocol |
| Crest.Liquid | `Crest.Liquid/Scripts/liquid-intellisense.js` | a generated script for the editors |
| Crest.Facebook | `/Crest.Facebook/sdk/*.js` | generated scripts |
| Crest.Server (`Endpoints/BlazorFrameworkScriptEndpoints.cs`) | `/_framework/{fileName}` | the host's Blazor framework scripts |

### Workflow engine (FastEndpoints)

72 endpoint classes in the vendored engine, mapped by `routes.MapWorkflowsApi()`
(`Crest.Workflows/Server/Features/CoreStartup.cs:305`) under `crest-workflows/api`, behind the
engine's `ApiSecurityMiddleware` (`Crest.Workflows/Server/Security/ApiSecurityMiddleware.cs:43`),
which maps each endpoint to a Crest permission. FastEndpoints does not use MVC. They are the
workflow management API: declared by Crest.Workflows and registered in the Server's API registry
like any module's (P7), so the OpenAPI document and the gate see one list. The engine's
`HttpEndpoint` activity routes are a different thing: workflow-defined endpoints, which become
defined entries in the registry (P15).

### SignalR hubs

| Hub | Route | Module |
| --- | --- | --- |
| `PermissionHub` | `/api/crest/permissions` | Crest.Server (`Services/PermissionInvalidation.cs`) |
| `AdminMenuLayoutHub` | `/api/crest/admin-menu-layout` | Crest.Server (`Services/AdminMenuLayoutInvalidation.cs`) |
| `MediaHub` | `/hubs/media` | Crest.Media (`Hubs/MediaHub.cs`) |
| `WorkflowInstanceHub` | engine real-time | vendored engine |

Hubs need no MVC. The caller lifetime on hubs is already Crest.Access's
(`ConnectionCallerHubFilter`).

### Machine and protocol endpoints

| Endpoint | Module | Hosting today |
| --- | --- | --- |
| `/api/graphql` | Crest.Apis.GraphQL | middleware (`GraphQLMiddleware`) |
| `/connect/authorize`, `/connect/token`, `/connect/logout`, user info | Crest.OpenId | OpenIddict + MVC `AccessController` (views) and `UserInfoController` |
| health checks | Crest.HealthChecks | `MapHealthChecks` |
| `robots.txt` | Crest.Seo | `RobotsMiddleware` |
| media files, image processing, secure media | Crest.Media | middleware |
| RSS feeds | Crest.Feeds | MVC `FeedController` |
| sitemaps (`sitemap.xml`, index) | Crest.Sitemaps | MVC `SitemapController` |
| remote deployment import | Crest.Deployment.Remote | MVC `ImportRemoteInstanceController` (API key) |
| MetaWeblog / RSD | Crest.XmlRpc, Crest.Lists | MVC `MetaWeblogController`, `HomeController`, `RemotePublishingController` |

### MVC controllers with JSON or redirect actions (no views)

`ContentPickerAdminController`, `LocalizationSetContentPickerAdminController` (picker search),
`ContentCulturePickerController` (culture switch redirect), `ExportRemoteInstanceController`,
`ExportFileController`, `OrderController` (list ordering), `TagController` (taxonomy tags),
`AddToDeploymentPlanController`, `EmailConfirmationController`,
`AdminTwoFactorAuthenticationController`, Demo's. Each either becomes an API in its group below or
dies with the MVC screen that calls it ([views.md](views.md)).

## Crest.Data: verified clean

A scan of `Crest.Data`, `Crest.Data.Abstractions`, `Crest.Data.Global`, `Crest.Data.Types`,
`Crest.Data.YesSql` and `Crest.Data.YesSql.Abstractions` for controllers, endpoint mapping,
`IActionResult`, `HttpContext`, problem details, `Microsoft.AspNetCore.Mvc` and `api/crest`
finds **nothing**. What lives there is data-engine work and stays: documents, migrations, the
global store, the value types, and the scope compilation
(`Crest.Data.YesSql/Scoping/ScopeExpressions.cs`). The API pieces that live elsewhere today:

- the request access helper `IRequestAccess` (`Crest.Server/Services/RequestAccess.cs`): moves
  to Crest.Server.Abstractions so registering modules can use it;
- `ProblemDetailsApiControllerExtensions` (`Crest.Abstractions`), `ApiProblemDetailsMiddleware`
  (`Crest.Infrastructure/ErrorHandling`), `DocumentSystemTextJsonOutputFormatter` and
  `MvcOptionsConfiguration` (`Crest.Infrastructure/Extensions`): API plumbing in the core
  libraries; they move to Crest.Server (§ Plumbing);
- `ApiAuthenticationHandler` (`Crest.Infrastructure/Security`): authentication, so it goes to
  Crest.Access, which owns the gate;
- the validation sink shape-engine.md puts in `Crest.Data.Abstractions` (`IValidationSink`) is
  validation, not API, so it belongs there. Crest.Server turns its errors into problem details
  (§ The error contract).

## Placement

Applied to every surface:

1. **Each module declares its own APIs** and answers them with its own services. Protocols a
   module implements (OpenIddict, GraphQL, feeds, sitemaps, file serving, scanner callback,
   health, robots, the workflow engine) are the same case.
2. **Every module registers its endpoints** into **Crest.Server's API registry** (§ The seam).
   Modules reference **Crest.Server.Abstractions** only, never Crest.Server, and Crest.Server
   references no module for its APIs, so there is no reference cycle. A disabled feature's
   registrations are not in the tenant's registry; a downstream module can override a
   registration.
3. **Crest.Server keeps only hosting plumbing**: the registries and their catalog, route
   groups, per-tenant gating, the antiforgery filter (validation asks Access), JSON options,
   the error contract, the OpenAPI document, and the host's own endpoints (the bootstrap
   aggregate `api/crest/app`, `api/crest/routing`, the Blazor framework scripts). It declares
   no domain API.
4. **Where each area goes:**

   | Surface | Module |
   | --- | --- |
   | Site settings, recipes, features, tenants, setup | `Crest.Settings`, `Crest.Recipes`, `Crest.Features`, `Crest.Tenants`, `Crest.Setup` (`Crest.Infrastructure` for what is shell-level) |
   | Content items | `Crest.Contents` |
   | Content definitions: types, parts, field visibility, definition locks, content groups | `Crest.ContentTypes` |
   | Users | `Crest.Users` |
   | Roles | `Crest.Roles` |
   | The access model: permission sets, groups, overrides | `Crest.Access` |
   | Sign-in, account, two-factor, antiforgery, login settings | the Access modules (`Crest.Users` for sign-in, account and two-factor; `Crest.Access` for antiforgery) |
   | Media | `Crest.Media` |
   | Other settings | each its module: admin settings, title bar and the admin theme preference `Crest.Admin`; debug `Crest.Settings`; security headers `Crest.Security` |
   | Workflow management | `Crest.Workflows` |
   | Everything else (menus, icons, localization, templates, indexes, queries, ...) | the module it serves ([server.md](../server.md) § 1) |

5. **Crest.Server.Abstractions** (new) holds what a registering module needs: the registration
   interfaces, route conventions, `IRequestAccess`, the error contract types, and the common
   DTOs modules reuse today from Crest.Server (until each folds into its declaring module).
6. **Not here:** database connections (Crest.Data, Crest.Data.YesSql) and search indexes
   (`Crest.Indexing` profiles, registered by their module like any other surface).

### Tracker

Owner is where the code ends up. Wave numbers are § Waves.

| ☐ | Item | Today | Target | Owner | Deps | Size | Wave |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ☐ | P1. Crest.Server.Abstractions project | — | create: `IApiContributor` (registration), `ApiRouteGroups`, `IRequestAccess`, error contract, shared DTOs | Crest.Server.Abstractions | — | M | 1 |
| ☐ | P2. The 47 Crest.Server controllers | Crest.Server, MVC-hosted | every area moves to its declaring module per § Placement and [server.md](../server.md) § 1, and registers through the seam: tenants, features, site settings, recipes and setup to their modules; content items to `Crest.Contents`; content types, groups, field visibility and definition locks to `Crest.ContentTypes`; `AccessModelController` to `Crest.Access`; users and roles to `Crest.Users` / `Crest.Roles`; `AuthController`, `LoginSettingsController` and `AntiforgeryController` to Access (P6); media to `Crest.Media` (P9); indexes to `Crest.Indexing`; debug settings to `Crest.Settings`; menus, icons, localization, templates, themes and the rest to their modules. Only `AppController` and `RoutingController` stay, as host endpoints | each declaring module | P1, P5 | L | 2-3 |
| ☐ | P3. Platform-module APIs already in their modules | Email, Email.Smtp, AuditTrail (2), BackgroundTasks, Layers, Placements, Notifications portal, Media.Scanning settings, Users settings, ContentFields user picker, Contents audit, Tenants `api/tenants`, ReCaptcha, Sharing, OpenId admin + consent | stay in their modules and register through the seam under their feature gates; `api/tenants` merges with the tenants API Crest.Tenants takes over (P2) | each module | P1, P5 | M | 2 |
| ☐ | P4. App-layer module APIs | Parties (6), Members (2), Regions (2), ContentPartLists (2), Workflows (10) | stay in the module and register through the seam; the module references Crest.Server.Abstractions instead of Crest.Server, and the shared models move from Crest.Server to Crest.Server.Abstractions (or to the module that declares them) | each module | P1, P5 | M | 3 |
| ☐ | P5. The seam | — | § The seam: registration, gating, the API registry (declared layer) | Crest.Server, Crest.Server.Abstractions | P1 | M | 1 |
| ☐ | P6. Access APIs | `api/crest/account` (Users), `api/crest/auth`, `api/crest/settings/login` and `api/crest/antiforgery` (Crest.Server), two-factor SendCode endpoints (Users), `EmailConfirmationController`, `AdminTwoFactorAuthenticationController` | declared by the Access modules: Crest.Users for account, sign-in, two-factor and login settings (`AuthController` and `LoginSettingsController` move out of Crest.Server into Crest.Users), the antiforgery token API to Crest.Access; registered through the seam; other modules register their access systems with Access | Crest.Users, Crest.Access | P5 | M | 3 |
| ☐ | P7. The workflow engine's FastEndpoints | engine-hosted under `crest-workflows/api` | the workflow management API, declared by Crest.Workflows and registered as declared entries in the Server's API registry (OpenAPI, gate) | Crest.Workflows | P5 | S | 3 |
| ☐ | P8. Hubs | Server 2, Media 1, engine 1 | each with its declaring module, registered through the seam: `PermissionHub` with permission invalidation (Access), `AdminMenuLayoutHub` with the admin-menu layout, `MediaHub` stays in Crest.Media, engine's stays | each module | P5 | S | 3 |
| ☐ | P9. Media APIs | `api/files` (`FilesController`, 26 actions), `api/media/*` (17 minimal endpoints), `api/crest/media*` in Crest.Server | `api/files` stays in Crest.Media, `api/crest/media*` moves from Crest.Server into Crest.Media; registered through the seam; `api/media/*` dies with the MVC media library ([views.md](views.md)) once the native library and pickers use `api/files` | Crest.Media | P5, views.md | L | 3 |
| ☐ | P10. Setup API (only if views.md § Setup chooses a pre-database page) | MVC `SetupController` | `POST api/crest/setup`, anonymous, runs in the setup shell before any store | Crest.Setup | views.md § Setup | M | 4 |
| ☐ | P11. Protocol endpoints | GraphQL, OpenIddict, health, robots, media serving, feeds, sitemaps, remote deployment import, scanner callback, XmlRpc/RSD | stay in their modules; the MVC-hosted ones (feeds, sitemaps, remote import, OpenId user info, XmlRpc/RSD) become minimal endpoints in their module's startup | each module | H3 | M | 4 |
| ☐ | P12. MVC JSON/redirect actions | the 11 listed above | each to an API in its P2/P3/P6 group, or deleted with its MVC screen | per group | views.md | M | 3-4 |
| ☐ | P13. Retirements (workflows.md step 8) | `api/content` (Contents), `api/queries` (Queries), `api/lucene`, `api/elasticsearch`, `Notifications/MarkAsRead`, `url-rewriting/resort`, `api/demo` | deleted when their replacements land: content API (Crest.Contents, P2), query operations (workflows.md steps 5-7), search as a connection, the native notifications and rewriting pages | each declaring module | step 5-7, views.md | M | 4 |
| ☐ | P14. Generated surfaces (workflows.md step 7) | — | REST + OpenAPI over the operation registry under `api/crest/ops/*` (route fixed in P14), entered as defined entries in the Server's API registry beside the declared ones; a module's declared API may be implemented by a generated route, never replaced by a workflow endpoint; GraphQL and RPC later | Crest.Server (registry), Crest.Workflows | P15, workflows.md step 3 | XL | 5 |
| ☐ | P15. The defined layer | the engine's private route table for `HttpEndpoint` activities (`IRouteTable`, `src/Crest.Workflows/engine/src/modules/Crest.Workflows.Http`) | the API registry's defined layer: entries stored as data, served through a dynamic endpoint data source invalidated on publish; the workflow `HttpEndpoint` routes become defined entries and the private table goes | Crest.Server, Crest.Workflows | P5 | M | 4 |

## The seam

Every module that declares an API (P2-P4, P6-P9, P11) registers it; Crest.Server holds the
registry and serves it. The registration interface, in Crest.Server.Abstractions:

```csharp
public interface IApiContributor
{
    /// <summary>The feature whose enablement turns these endpoints on, in this tenant.</summary>
    string FeatureId { get; }

    /// <summary>Maps the contributor's endpoints into the group the server hands it
    /// (prefix, antiforgery, JSON options, error contract and OpenAPI tags already applied).</summary>
    void Map(IEndpointRouteBuilder group);
}
```

- **Two layers.** *Declared* entries come from module code (`IApiContributor`): built into the
  tenant's endpoint table when the shell builds, following feature enablement, never stored in
  the database. *Defined* entries are data (workflow `HttpEndpoint` routes, generated operation
  endpoints): changeable at runtime, served through a dynamic endpoint data source that is
  invalidated on publish (P15). This mirrors the page registry's code-scanned and data-defined
  providers in `src/Crest.Server/Routing`.
- **Per-tenant gating.** A module's `StartupBase.Configure(IApplicationBuilder,
  IEndpointRouteBuilder, IServiceProvider)` runs in each tenant's pipeline only when its feature
  is enabled, so a minimal endpoint exists only in tenants that enabled it. Contributors are
  registered from such startups (`services.AddApiContributor<PartiesApi>()`), and Crest.Server
  maps the contributors registered in **this** tenant's container in its own `Configure`: an
  endpoint of a disabled feature is not in the tenant's route table at all.
- **What the group carries** (applied once by Crest.Server): the `api/crest` prefix (protocols
  keep their own paths), the antiforgery endpoint filter for cookie-authenticated calls
  (§ Plumbing), the Crest JSON options (documents and entities as today), the exception mapping
  (§ The error contract), the access helper, OpenAPI tags per contributor, and the gate's route
  metadata.
- **The API registry.** Crest.Server keeps the registered contributors, route groups and defined
  entries per tenant (working name `IApiEndpointRegistry`). The OpenAPI document, the gate and
  the conformance suite read it. A downstream module overrides a registration through it.
- **Controllers during the transition.** A contributor may still be an attribute-routed
  controller in its module until its area is converted (§ Hosting); its gating stays the MVC
  application-part gating until then.

## The error contract

One shape for every API error, so shape-engine.md's validation sink (U1) and the content and
settings APIs agree:

- `application/problem+json` (RFC 9457) with `type`, `title`, `status`, `detail`, and an
  `errors` map `{ key: [messages] }` for validation (the sink's errors, keyed by field path
  `Part.Field` or by setting name).
- Mapped exceptions, registered with the server (`IApiExceptionMapper` in
  Crest.Server.Abstractions): `DefinitionLockedException` → 409 (today
  `DefinitionLockExceptionFilter`), `WorkflowHookFailedException` → 409 (today
  `WorkflowHookFailedExceptionFilter`), `ContentSaveRefusedException` → 400 with `errors`,
  `ExchangeRateUnavailableException` → 409, access denials → 403 or 404 (deny-as-not-found)
  from the decision.
- One implementation: an `IExceptionHandler` plus the existing `ApiProblemDetailsMiddleware`,
  moved into Crest.Server; it works for controllers, minimal endpoints and FastEndpoints alike.

| ☐ | Item | Owner | Deps | Size | Wave |
| --- | --- | --- | --- | --- | --- |
| ☐ | E1. Error contract types and `IApiExceptionMapper` | Crest.Server.Abstractions | P1 | S | 1 |
| ☐ | E2. The exception handler and problem-details middleware in Crest.Server; the two MVC exception filters deleted | Crest.Server | E1 | S | 2 |
| ☐ | E3. Validation sink errors (shape-engine.md U1) rendered as `errors` | Crest.Server | E1, shape-engine U1 | S | 2 |

## Relation to other plans

- **[server.md](../server.md)** is the per-area inventory: each wrapper goes to the module that
  declares the surface (P2); the APIs already in modules stay there (P3). The option picker's
  API (`api/crest/option-sources`, `option-picker/attachment`) goes with the option picker to
  whichever module server.md S2 picks. The access gate pieces (middleware, route permission
  table, permission invalidation, `RequestAccess`'s decision side) go to Crest.Access;
  `IRequestAccess` the helper goes to Crest.Server.Abstractions (P1) because every API uses it.
  The Blazor host (route table, `App.razor`, display endpoints) stays in Crest.Server.
- **[workflows.md › Operations](../workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)**:
  - Steps 5 and 6 (move the reads and the writes onto the pipelines) are executed inside each
    declaring module's area (P2-P4): when an area is converted to route groups, its reads go
    through the query pipeline and its writes through the action pipelines in the same change.
    This plan does not wait for them, and an area converted after its step 5/6 work needs no
    second pass.
  - Step 7 (generate the surfaces) is P14: generated routes are defined entries in the Server's
    API registry, where they meet the routes modules declare.
  - Step 8 (retire) is P13 for `api/content`, `api/queries` and the search APIs. Retiring the
    hand-written `api/crest/*` controllers "as each generated route lands" means: the declaring
    module keeps the API, and only its implementation becomes a generated route over its
    operations.
  - The table "The surfaces today, and their fate" there stays authoritative for fate; this
    document adds where each surface is hosted.

## Hosting

Every API moves to **minimal APIs**, and the MVC module (`Crest.Mvc`,
`Crest.Mvc.Abstractions`, `AddMvc`) is deleted. Staged: the views half goes first (views.md,
shape-engine.md); `AddControllers()` stays while P2-P4 regroup the areas; each area is converted
to a route group in the change that moves it, so it is touched once; `AddControllers()` and
`Crest.Mvc` are deleted when the last area is converted. Not a permanent `AddControllers()`
half, because two hosting models mean two gating mechanisms, two antiforgery paths and two
error paths, and `ModelState` and the application-part machinery would stay reachable.

- Gating: no work beyond the seam: minimal endpoints are mapped per tenant from feature-gated
  startups (§ The seam), which is already how Media's and Contents' endpoints are gated.
- Antiforgery: an endpoint filter on the registry's route groups, asking Access to validate the
  `RequestVerificationToken` header for cookie-authenticated, state-changing calls (the gate
  already distinguishes cookie from `Api` scheme); replaces the global MVC filter.
- JSON: `ConfigureHttpJsonOptions` with the same converters `DocumentSystemTextJsonOutputFormatter`
  applies to `IDocument` / `IEntity`.
- Errors: the error contract's `IExceptionHandler` (E2) replaces both filters, and covers
  minimal endpoints and FastEndpoints too.
- ModelState: gone; the validation sink (shape-engine.md U1) is the only error collector.
- OpenAPI: Microsoft.AspNetCore.OpenApi describes minimal endpoints; Swashbuckle's ApiExplorer
  works with them through `AddEndpointsApiExplorer`.
- Size: 92 controllers and 486 actions rewritten as route groups (mechanical: one route group
  per controller, a handler per action, the same DTOs), 11 MVC JSON actions, the login
  service's MVC types (§ Plumbing), the URL helpers, converted area by area with the area's API
  tests.

| ☐ | Item | Owner | Deps | Size | Wave |
| --- | --- | --- | --- | --- | --- |
| ☐ | H1. Antiforgery endpoint filter | Crest.Server | P1 | S | 1 |
| ☐ | H2. Crest JSON options for minimal endpoints | Crest.Server | — | S | 1 |
| ☐ | H3. Convert areas to route groups, with P2-P4 | each declaring module | H1, H2, E2, P5 | XL | 2-3 |
| ☐ | H4. Delete `AddControllers()`, `Crest.Mvc`, `Crest.Mvc.Abstractions` | Crest.Server | H3, views.md wave 4, shape-engine.md wave 4 | M | 5 |

## Plumbing

Where each piece of MVC non-UI plumbing lives after the refactor:

| ☐ | Piece | Today | After | Owner | Deps | Size | Wave |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ☐ | Per-tenant feature gating of controllers | `Crest.Mvc/ShellFeatureApplicationPart.cs`, `ModularApplicationModelProvider.cs` | gone; the seam's startup gating | — | H3 | — | 5 |
| ☐ | Global antiforgery | `Crest.Mvc/Startup.cs:93` (`AutoValidateAntiforgeryTokenAttribute`) | antiforgery endpoint filter on the registry's route groups | Crest.Server | H1 | S | 1 |
| ☐ | `IDocument` / `IEntity` JSON | `Crest.Infrastructure/Extensions/DocumentSystemTextJsonOutputFormatter.cs`, `MvcOptionsConfiguration.cs:18` | `ConfigureHttpJsonOptions` in Crest.Server | Crest.Server | H2 | S | 1 |
| ☐ | Exception filters | `Crest.Server/Filters/DefinitionLockExceptionFilter.cs`, `Crest.Workflows/Server/Hooks/WorkflowHookFailedExceptionFilter.cs` | `IApiExceptionMapper` registrations; filters deleted | Crest.Server / Crest.Workflows | E2 | S | 2 |
| ☐ | Problem details | `Crest.Abstractions/ProblemDetailsApiControllerExtensions.cs`, `Crest.Infrastructure/ErrorHandling/ApiProblemDetailsMiddleware.cs` | Crest.Server (middleware, handler) and Crest.Server.Abstractions (types); the controller extensions deleted with the controllers | Crest.Server(.Abstractions) | E1 | S | 2 |
| ☐ | ModelState as the content manager's sink | `Crest.ContentManagement/DefaultContentManager.cs:408, 412, 480` via `IUpdateModelAccessor` (`Crest.Mvc.Abstractions`) | the validation sink (shape-engine.md U1, U2); the API returns its `errors` | Crest.Data.Abstractions / Crest.ContentManagement | shape-engine U1 | S | 2 |
| ☐ | `LoginService` TempData / ActionContext | `Crest.Server/Services/LoginService.cs:34, 43, 128-130` (translates `ILoginFormEvent` vetoes from redirect results and TempData) | moves to Crest.Users (Access); the login events return a refusal (`LoginRefusal`) instead of an `IActionResult` + TempData, so no `ActionContext` is needed | Crest.Users | P6 | M | 3 |
| ☐ | `IUrlHelper` | 23 files (Navigation, Sitemaps, Seo handlers, Liquid filters, tag helpers, link field driver, ...) | `LinkGenerator` with a shell-aware helper (`IShellLinkGenerator`) in Crest.Abstractions; the Razor-only users (tag helpers, `UrlHelperExtensions`) go with the views | Crest.Abstractions | — | M | 3 |
| ☐ | `ShellRouteValuesAddressScheme` | `Crest.Abstractions/Routing/`, registered from `Crest.Mvc/Extensions/PlatformBuilderExtensions.cs` | stays in Crest.Abstractions (it is routing, used by `LinkGenerator` for Autoroute, HomeRoute, Sitemaps); its registration moves to Crest.Infrastructure | Crest.Infrastructure | — | S | 3 |
| ☐ | `FormValueRequiredMatcherPolicy` | `Crest.Abstractions/Routing/`, registered in `Crest.Mvc/Startup.cs:102` | gone (form-post submit-button routing for MVC screens) | — | views.md wave 4 | S | 5 |
| ☐ | `ShellFileVersionProvider` | `Crest.Mvc/ShellFileVersionProvider.cs` | `IFileVersionProvider` stays needed by `Crest.ResourceManagement` and Media's URL helpers (`MediaEndpointHelpers`, `ImageShortcodeProvider`); the shell-aware implementation moves to Crest.Infrastructure, registered without MVC | Crest.Infrastructure | — | S | 3 |
| ☐ | `ApiAuthenticationHandler` | `Crest.Infrastructure/Security/ApiAuthenticationHandler.cs` | Crest.Access (the gate owns authentication) | Crest.Access | — | S | 3 |
| ☐ | Area routing (`IAreaControllerRouteMapper`, `[Admin]` routes) | `Crest.Mvc/Startup.cs:154` | gone with the MVC screens | — | views.md wave 4 | — | 5 |

## Waves

| Wave | Content | Depends on | Exit |
| --- | --- | --- | --- |
| 0 | views.md's open questions (shared screens, setup) answered | user | answers recorded in views.md |
| 1 | P1, P5, E1, H1, H2 | — | Crest.Server.Abstractions exists; a contributor can register a gated route group |
| 2 | P2 (first areas to their declaring modules), P3 (registered in place), E2, E3; plumbing: exception filters, problem details, the content manager's sink; H3 for these areas | shape-engine.md U1 | the platform modules' APIs registered through the seam; one error contract |
| 3 | P2 (remaining areas), P4, P6, P7, P8, P9, P12; plumbing: `LoginService`, `IUrlHelper`, address scheme, file versions, `ApiAuthenticationHandler`; H3 for the remaining areas | views.md waves 1-3 (the MVC callers of the JSON actions and of `api/media/*`) | every declared API in the Server's API registry |
| 4 | P10 (if views.md § Setup chooses it), P11, P13, P15 | views.md wave 4, workflows.md steps 5-7 | no machine endpoint on MVC; retired surfaces gone; workflow endpoints are defined entries |
| 5 | H4 (delete `AddControllers()`, `Crest.Mvc*`), the remaining plumbing deletions; P14 starts | views.md wave 4, shape-engine.md wave 4 | see exit criterion |

Cross-workstream: views.md's ports each need their API in the matching wave here (wave 2 for
settings and admin APIs, wave 3 for the access screens' APIs, P6); shape-engine.md wave 2 needs
E1/E3 for the validation sink; shape-engine.md wave 4 and views.md wave 4 are what allow H4.

## Exit criterion

- Every API endpoint in `src/` is declared by its module and registered in Crest.Server's API
  registry (declared through `IApiContributor`, or as a defined entry), all listed in the
  per-tenant registry and the OpenAPI document.
- No API code in `Crest.Data*`; Crest.Server holds no API beyond the host's own endpoints.
- One error contract (problem details with `errors`), one antiforgery path, one JSON
  configuration.
- No `Controller` or `ControllerBase` class in `src/`, `AddMvc`/`AddControllers` gone,
  `Crest.Mvc` and `Crest.Mvc.Abstractions` deleted; the setup shell is the only exception if
  views.md § Setup keeps setup on MVC.
