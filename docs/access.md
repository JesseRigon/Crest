# Access — who may do what, in one place

**Status (2026-10-10): audit done; the machinery landed in its first form as the
`Crest.Access` module, which stays a module.** This document is the map of every system that decides who may do
what (authentication, authorization, permissions, roles, tenant and theme shells, OpenId,
machine access) as the code stands, the defects the audit found, the overlaps between the
old pipeline and the new machinery, and the task list that makes it one system. The
operations model it serves (one request path, four pipelines) is
[workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery);
the member caller is described there too.

## Rulings

- **Access stays its own module** (ruling 2026-10-10, final; a same-day ruling to fold it
  into core was withdrawn once it was clear the module is landed code, not a plan).
  `Crest.Access.Abstractions` holds the contracts below `Crest.Data`; `Crest.Access` holds
  the caller builder, the decision, the scope compiler, the cache, the version and the
  auditor, always enabled. The old pipeline's remains are folded **into it**, never the
  other way round.
- **One request, one access calculation** (ruling 2026-10-10). The caller, its decision
  inputs and its scope set are computed once at the gate for a request, and then **handed
  down** through everything that request causes on the server: the page render and its
  slots, the operations it dispatches, the child scopes, the deferred tasks and the
  after-commit work it queues, the workflow bursts it starts inline. Nothing in that chain
  rebuilds or re-derives the caller, and nothing reads rights from the principal. The
  hand-down ends with the request: a later request, a resumed workflow burst, a scheduled
  run or a queued job builds its own caller at its own entry point, never from a copy
  serialized by an earlier request (identity may be carried; rights never).
- **There is one decision, one caller and one scope.** Every surface asks the same decision
  through the platform's `IAuthorizationService` or directly; the caller is built once per
  request on the server from the identity and server-held state; row scope comes from the
  registered providers and nothing else. Anything that re-derives a right from cookie claims
  is a defect to remove.
- **The gate owns authentication**, once per request, tenant first, then theme shell, then
  user, then permissions (workflows.md › Operations › Decisions › Who authenticates).
  Repeated `UseAuthentication` calls behind it go.
- **Identity in the cookie and in tokens; rights on the server.** Cookies and bearer tokens
  carry who the caller is. Roles, permissions, class, organization and ceiling are read from
  the store under the tenant's permission version, never from claims.

## The systems today

### Authentication

| Scheme | Where | What it does |
| --- | --- | --- |
| Identity cookie (`orchauth_{tenant}`) | `Crest.Users` | the default sign-in; `AddIdentity`, cookie paths from `UserOptions`; `UseAuthorization` at order 0 |
| Tenant `UseAuthentication` | `Crest.Infrastructure` module extensions, order −150 | the platform's own authentication middleware, after routing |
| `Api` forwarder | `Crest.Infrastructure/Security/ApiAuthenticationHandler.cs` | forwards to the scheme in `ApiAuthorizationOptions` (default "Bearer"); no result if that scheme is absent; challenge writes 401 with `WWW-Authenticate: Bearer` |
| OpenId Validation | `Crest.OpenId` | the only bearer-to-principal path: OpenIddict validation, local (data protection, optionally another tenant's server) or remote (discovery); re-points `Api` to itself |
| OpenId Server | `Crest.OpenId` | issues tokens: code, password, refresh and client credentials; user flows carry the user's name identifier, the client-credentials flow carries `sub` = client id, the application's roles and their Permission claims, and **no name identifier** |
| OpenId Client, Management | `Crest.OpenId` | external OIDC login; admin UI for applications and scopes |
| External providers | `Crest.Microsoft.Authentication`, `Crest.Google`, `Crest.Facebook`, `Crest.GitHub`, `Crest.Twitter` | land in `Identity.External`, finished by the Users external-authentication feature |
| Member portal login | `Crest.Members` `MemberPortalController`, `MemberLoginSurfaceGate` | the portal's login, register and external-login; the `ILoginFormEvent` veto that keeps members off the tenant surface and staff off the portal |
| Crest JSON login | `Crest.Server` `CrestLoginService`, `api/crest/auth` | replays the stock login event sequence and signs the one cookie |
| Workflows engine API | `Crest.Workflows` `CrestWorkflowsApiSecurityMiddleware`, order −151 | its own `UseAuthentication` branch under `/crest-workflows/api`, then the decision, antiforgery, a transient claims identity with engine permission names, the Run list and the endpoint's policy |
| Secure media, GraphQL, media tus | `Crest.Media`, `Crest.Apis.GraphQL` | each authenticates `Api` again before its own check |

The request path today: `BlazorAdminThemeMiddleware` (a startup filter before routing)
classifies the request, authenticates once (`Api` when an `Authorization` header is present,
else the cookie), stamps the side and builds the caller; then routing; then the workflows
branch, the tenant `UseAuthentication`, the module startups, `UseAuthorization`, secure
media and GraphQL each authenticate again. A request with both a cookie and a bearer token
is authenticated as the token by the gate and as the cookie by `UseAuthentication`, so the
handler rebuilds a caller for a different user.

### Authorization

The platform's permission types are `Crest.Infrastructure.Abstractions/Security`
(`Permission`, `PermissionRequirement`, `IPermissionService`, `IPermissionProvider`).
`Crest.Security` is only the security-headers module. The stock handlers
(`PermissionHandler`, `RolesPermissionsHandler`, `SuperUserHandler`,
`ContentTypeAuthorizationHandler`) are gone; what decides a `PermissionRequirement` today is
`AccessAuthorizationHandler` delegating to the one decision, beside nine resource handlers
that still map a permission to a finer one for their resource kind:

| Handler | Module | Maps |
| --- | --- | --- |
| `RoleAuthorizationHandler` | Users | per-role user-management variants, `EditOwnUser`; re-enters `IAuthorizationService` |
| `ViewUserAuditTrailEventsHandler` | Users audit trail | own versus others' events |
| `SiteSettingsAuthorizationHandler` | Settings | `ManageGroupSettings` to per-group permissions |
| `CustomSettingsAuthorizationHandler` | CustomSettings | `ManageResourceSettings` to per-type (an unregistered permission instance) |
| `LocalizeContentAuthorizationHandler` | ContentLocalization | `LocalizeContent` to `LocalizeOwnContent` by owner |
| `IndexingAuthorizationHandler` | Indexing | `QuerySearchIndex` to per-index |
| `ManageMediaFolderAuthorizationHandler`, `ViewMediaFolderAuthorizationHandler` | Media | per-folder, own, attached-field; the view handler fails sticky and probes an anonymous principal for cache marking |
| `WorkflowDefinitionAccessHandler` | Workflows | the definition's Edit and Run lists, with its own super-user and Administrator checks |

Two policy registrations exist (`MediaApi`, the media hub). `DefaultPermissionGrantingService`
(the claims-based implied-by check) survives with one consumer, the role editor's granted
view. Rules, Layers, Liquid `is_in_role`, the approval service and the role cache provider
still read **cookie role claims**.

### The access machinery (`Crest.Access.Abstractions`, `Crest.Access`)

Contracts: `CallerContext` (tenant, side, user, organization, class, impersonator, super
user, roles, permissions, permission version, culture, scope signature), `CallerSide`
(Site, Admin, Member, System), `ICallerContextAccessor`, `ICallerContextFactory` with
`CallerRequest`, `CallerContextBuilder` and `ICallerContextContributor`, `IAccessRunner`,
`AccessHeaders`, `AccessDecision` and `IAccessDecision`, `IResourcePermissionMapper`,
`IAccessCeiling`, `ScopeRule`, `ScopeFilter`, `ScopeCondition`, `IScopeProvider`, `ScopeSet`,
`IScopeSetProvider`, `ScopedExecution`, `ScopeRefusedException`, `IAccessAuditor` and
`AccessEvent`, `IPermissionVersion`, `AccessGate` (the side item and header parsing).

Implementation (module "Access", always enabled, depends on Roles, Users, Settings):
`CallerContextFactory` (identity from the name identifier; roles from the user manager;
permissions from the role manager for Anonymous, Authenticated and the user's roles; super
user = admin role or the site's super user; then contributors; state cached in
`CallerStateCache` under the permission version), `AccessDecisionService` (ceilings, super
user, resource mappers, permissions with implied-by chains), `AccessAuthorizationHandler`
(the bridge from `IAuthorizationService`), `ScopeSetProvider` (compiled and cached per scope
signature), `PermissionVersionService` with `PermissionVersionBumper` (role and user
events), `AccessAuditor` on the audit trail, `AccessRunner`, `SystemCallerForBackgroundTasks`.
Scope providers: `ContentItemScopeProvider` (Contents), `AssignmentScopeProvider` and
`OrganizationScopeProvider` (Server), compiled by `Crest.Data.YesSql` `ScopeExpressions`.
Contributors and ceilings: `MemberCallerContributor`, `MemberClassCeiling` (Members).
Consumers of the scope: Queries, the GraphQL content filters, the content-items API, the
pickers, the admin content lists. Only Crest.Server references the implementation; seven
projects reference the abstractions.

### Shells

The **tenant shell** is resolved by `ModularTenantContainerMiddleware` through the running
shell table (host and prefix, then host, then prefix, then the default) and opened as the
request's `ShellScope`; `ModularTenantRouterMiddleware` moves the prefix into `PathBase`.
Deferred tasks run in a fresh scope with no caller. Background runs enter through a
synthetic `HttpContext` flagged `IsBackground`, whose short-circuit middleware sits at
`int.MinValue`, which is after the startup filters.

The **theme shell** is `BlazorAdminThemeMiddleware` (Crest.Server, a startup filter):
infrastructure paths get a path strip only; `{shell}/api` and `/api` go through
`GateApiAsync` (a valid `X-Shell` wins, an unknown value is 400, a cookie-authenticated call
without the header and without a prefix side is 403, else the prefix side or Site); pages
go through `GateAsync` (authenticate, stamp the side, build the caller with `X-Org`); admin
pages add the login redirect and the route permission (`CrestRouteAuthorizationService` over
`ICrestRoutePermissionProvider`s, first template wins, no match denies); member pages add
the login redirect only. It stamps the route bucket and the side as two separate items.
`RouteGateMatcherPolicy` keeps a shell from resolving another shell's endpoints; the member
prefix is `MemberOptions.MemberUrlPrefix`. Static assets are not gated and get no caller.

### Users and claims

`DefaultUserClaimsPrincipalProviderFactory` runs six `IUserClaimsProvider`s at sign-in and
stamp refresh (roles and Permission claims, e-mail, two-factor, localization, user class,
demo profile). The security-stamp interval is the ASP.NET default of 30 minutes, and
`OnRefreshingPrincipal` copies every old claim onto the refreshed principal, so a removed
role survives in the cookie. Invalidation runs on three channels that do not agree: the
permission version (role and user events), the security stamp (role changes, disabling,
conversion) and the SignalR nudge (role update only).

### Machine access

Eight files use `[Authorize(AuthenticationSchemes = "Api")]` (content endpoints, queries,
Lucene, Elasticsearch, tenants, demo). GraphQL authenticates `Api` by hand. Crest.Server's
controllers rely on the gate's principal. There is no inbound API-key handler; the remote
deployment import compares a client name and key itself, outside antiforgery and outside
any permission, and then imports a recipe as an anonymous site caller.

## Defects found (2026-10-10)

Fix these before anything moves; each is a few lines and a test.

1. **Cross-user scope leak.** The scope set is cached under the caller's scope signature,
   which omits the user id, while the content provider filters `Owner == UserId` and the
   assignment provider filters the caller's assignments. Two users with the same roles share
   one set for ten minutes. Add the user id to the signature (or key the cache on it) and
   drop the data-dependent ids (organization-owned and assigned items) from cached rules in
   favour of joins.
2. **Stale caller state.** `CallerStateCache` is a singleton with no eviction, keyed on the
   raw `X-Org` value, and holds the session's active organization and the impersonator,
   which change without a version bump. Key it on validated state only, evict on size and
   time, and bump the version on organization switch, impersonation start and stop,
   binding writes and super-user changes.
3. **Unbounded growth from `X-Org`.** An anonymous client adds a cache entry per distinct
   header value. Validate the organization before it reaches the key.
4. **Revoked memberships keep working.** Binding writes save through the session and raise
   no event, so the version never bumps. Raise one.
5. **The client picks the side.** `X-Shell` wins over the shell prefix, and nothing refuses
   a member on the admin or site side, where the organization scope is `All`. The prefix
   decides; the header may only confirm; a member-only user on a non-member side is denied.
6. **Client-credentials tokens act as anonymous.** They carry no name identifier, so the
   factory builds the Anonymous caller. The application becomes a caller kind with its own
   roles (read from the application, not from claims).
7. **The role editor's effective view is wrong.** It builds a fake principal without a name
   identifier. It should ask the decision for a synthetic caller holding the role.
8. **Workflow definition access decided twice**, once from cookie claims and the site's
   super user compared against a user name. One implementation, on the caller.
9. **Background tasks can run as anonymous.** The gate runs before the `IsBackground`
   short-circuit and sets an anonymous caller that `??=` then keeps. The system caller is
   set by the entry point, unconditionally.
10. **Member admin actions ignore antiforgery** (`MembersController`: impersonate,
    bindings, active organization, conversion).

Smaller: every user update bumps the tenant-wide version (lockout counters included);
implied-by chains are resolved by registered name, so a runtime permission instance loses
its chain; deferred shell tasks and remote import have no caller, so any scoped query
there throws; search sources are unscoped; the authorization handler does not audit; the
stamp refresh keeps stale claims.

## Overlaps to remove

| Overlap | Today | One system |
| --- | --- | --- |
| Permissions | Permission claims in the cookie and in tokens, and the role manager read by the factory | the factory only; `RoleClaimsProvider` stamps roles for identity, no Permission claims; tokens carry identity |
| Implied-by | `DefaultPermissionGrantingService` (instance) and `AccessDecisionService` (registry) | the decision, resolving from the instance first and the registry second; the role editor asks the decision |
| Super user | the factory, the workflow definition handler, the definition resource | the caller's flag only |
| Definition access lists | handler and resource | the resource's `Admits`, called by the decision through a mapper |
| Resource to permission | `IResourcePermissionMapper` (content) and eight resource handlers | one mapper per resource kind, registered by its module; the handlers go; direct callers of the decision get the same mapping |
| Invalidation | permission version, security stamp, SignalR nudge | the version, bumped by every rights write; the stamp for sign-out and credentials; the nudge fed from the version |
| Shell stamps | side item and route bucket item | one item set once by the selector |
| Authentication | the gate, tenant `UseAuthentication`, the workflows branch, GraphQL, secure media, tus, member session | the gate only |
| User class | cookie claim and contributor | the contributor |
| Roles | cookie claims (Rules, Layers, Liquid, approvals, `Me`) and the caller | the caller; the condition evaluators and Liquid read it |
| Content filters | caller scope plus the old per-type authorization in admin controllers | the scope |
| Route gates | `CrestRouteAuthorizationService` (Blazor) and `AdminFilter` (MVC) | one route permission table; `AdminFilter` goes with the MVC admin |

## Tasks

### 1. Fix the defects

- [x] (1) Landed 2026-10-10: the scope signature includes the user id, and the scope set is
  built once per request in the scoped provider; no scope is cached across requests.
- [x] (2, 3, 4) Landed 2026-10-10: the caller state cache is a bounded memory cache (thirty
  minutes sliding, twelve hours absolute); an anonymous caller's key carries no
  organization; the impersonator and the session's organization are request-level facts
  supplied by `ICallerRequestResolver` (`MemberCallerRequestResolver`) and never cached;
  binding writes bump the permission version. Still open: a bump on super-user change.
- [x] (5) Landed 2026-10-10: the shell prefix decides an API call's side and `X-Shell` may
  only confirm it (disagreement is 403); a member-class user is refused on the admin side
  and holds no tenant rights and no organization rows on the site side.
- [x] (6) Landed 2026-10-10: client-credentials tokens carry the application's id as the
  name identifier, and the factory builds an application caller from the token's roles.
- [x] (7) Landed 2026-10-10: the role editor's effective view asks the decision for a
  synthetic caller holding the role.
- [x] (8) Landed 2026-10-10: the workflow definition handler answers with `Admits` on the
  request's caller; the super user is the caller's flag.
- [x] (9) Landed 2026-10-10: the theme-shell selector skips background runs and the
  background entry point sets the system caller unconditionally.
- [x] (10) Landed 2026-10-10: the member admin actions validate antiforgery.
- [x] The authorization handler no longer builds a second caller for a differing principal;
  it answers as anonymous (the hand-down rule).
- [x] The authorization handler audits every denial (landed 2026-10-10); deferred tasks
  carry their caller (section 2).
- [ ] Version bumps only for rights writes; implied-by from the instance; a caller for the
  remote deployment import; a bump on super-user change.

### 2. One calculation per request, handed down

- [x] **The gate is the only builder.** `ICallerContextFactory` is called from the gate
  (`GateAsync`/`GateApiAsync`) and from the background entry points. `AccessAuthorizationHandler`
  builds one only when no gate ran (a host without the shell selector) and never a second
  one for a differing principal (landed 2026-10-10). Still open: the workflow caller
  resolver's per-burst build is an entry point by design; nothing else may call the factory.
- [x] **The caller is immutable for the request** and so is its scope set: the scoped
  `IScopeSetProvider` builds it once per request and keeps it for the request only; the
  cross-request scope cache went with the leak it caused (landed 2026-10-10).
- [x] **Hand-down into child scopes.** Landed 2026-10-10: `IInheritedShellScopeFeature`
  (Crest.Abstractions) marks a scope feature that every scope created from the current one
  receives (`CreateChildScopeAsync`, the deferred tasks); the caller accessor keeps the
  caller as such a feature (`CallerFeature`) and answers from it in an inheriting scope, so
  child scopes, deferred tasks and after-commit work (which goes through child scopes) carry
  the caller that queued them, the system caller included.
- [ ] **Hand-down into inline work.** Workflow bursts started inline by a request (hooks,
  HTTP-endpoint workflows, the approval decision) inherit the request's caller
  (`WorkflowAccessGateMiddleware` already prefers the inherited caller); the actor
  snapshot is written for later bursts, which build their own.
- [ ] **No hand-down across requests.** The client's session copy, the actor snapshot in
  a workflow instance, a job payload and a token carry identity only; the entry point that
  receives them builds a fresh caller under the current permission version. The
  conformance suite checks that a right revoked between two requests is gone on the
  second.
- [ ] **One place reads the principal.** Only the gate reads `HttpContext.User`; the
  decision, the scope, Rules, Layers, Liquid, approvals and `Me` read the caller.

### 3. One gate

- [ ] The gate moves from the theme-shell middleware into the platform's request path, after
  the tenant router and the `IsBackground` short-circuit, as the one place that
  authenticates (cookie or `Api` by request kind), stamps the shell once, builds the caller
  and denies a disagreeing marker; the theme-shell selector keeps classification, path
  shifting and the route table. `UseAuthentication` at −150, the workflows branch's own
  authentication, GraphQL's, secure media's, tus's and the member session's re-authentication
  go.
- [ ] Static assets and infrastructure paths get the anonymous caller rather than none.
- [ ] Deferred tasks and child scopes inherit the caller of the scope that queued them, or
  the system caller when queued by a background entry point.

### 4. One decision

- [ ] The eight resource handlers become `IResourcePermissionMapper`s registered by their
  modules; the handler list ends at `AccessAuthorizationHandler`.
- [ ] `RoleClaimsProvider` stops stamping Permission claims; OpenIddict tokens carry identity
  and application id only; the refreshed principal carries only the claims the providers
  produce now.
- [ ] Rules, Layers, Liquid `is_in_role`, the approval service, the role cache context and
  `CrestAuthController.Me` read the caller.
- [ ] The admin controllers' per-type content authorization goes; the scope is the filter.
- [ ] `IAccessDecision` audits denials everywhere, not only in workflow activities; reads per
  the audit ruling.

### 5. Machine callers

- [ ] An application caller kind: client-credentials tokens resolve to the application's
  roles and permissions through the factory; the audit records the application.
- [ ] The remote deployment key becomes an `Api`-scheme credential (an opaque-key handler
  behind the forwarder, [machine-actors.md](machine-actors.md)); its import runs as that
  caller behind `ImportRemoteInstances`, with antiforgery irrelevant because it is a bearer
  call.
- [ ] The `[Authorize(AuthenticationSchemes = "Api")]` attributes go once the gate
  authenticates `Api` for every API request.

### 6. Invalidation

- [ ] One channel: the permission version, bumped by role, user-role, binding, policy,
  organization-switch, impersonation and super-user writes; the SignalR nudge sends the
  version; the security stamp stays for sign-out and credential change, at an explicit
  interval.

## Decisions needed

- [ ] **Application callers and the member ceiling.** Does an application token acting for
  an organization (a member's integration) take the member ceiling and the organization
  scope, and how is that organization named in the token?
- [ ] **The anonymous caller for assets.** Whether static asset requests should build the
  anonymous caller (cost per request) or carry a marker that scoped code treats as
  anonymous without a build.
- [ ] **Search scope.** Whether Lucene and Elasticsearch sources take the scope in the query
  (index-side filtering with the owner and organization columns indexed) or stay admin-only
  until they become connections (queries.md).
