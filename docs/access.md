# Access — who may do what, in one place

**Status (2026-10-10): audit done; the machinery landed in its first form as the
`Crest.Access` module, which stays a module.** This document is the map of every system that decides who may do
what (authentication, authorization, permissions, roles, tenant and theme shells, OpenId,
machine access) as the code stands, the defects the audit found, the overlaps between the
old pipeline and the new machinery, and the task list that makes it one system. The
operations model it serves (one request path, four pipelines) is
[workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery);
the member caller is described there too.

Who may do what, per kind of caller, for pages and for data and workflows (the reference for
tests and audits): [user-stories.md](user-stories.md).

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
  Nothing behind it authenticates again (landed 2026-10-10).
- **Identity in the cookie and in tokens; rights on the server.** Cookies and bearer tokens
  carry who the caller is. Roles, permissions, class, organization and ceiling are read from
  the store under the tenant's permission version, never from claims.

- **Opinionated, but every feature can be turned off** (ruling 2026-10-11). Crest ships as a
  pre-built system with most features enabled by default, unlike Orchard's pick-your-own
  model; every feature still stays disable-able, and the access machinery works with any
  domain feature off: a feature registers its access extensions only while enabled, and
  nothing in `Crest.Access` names a domain module.
- **Access is extended, never rewritten** (ruling 2026-10-11). The gate, the caller, the
  decision and the scope are one system that does all permission processing; domain modules
  keep their own access logic in their own projects and **register it** through the
  contracts in `Crest.Access.Abstractions`, so adding a domain never means editing the gate:
  - resource mappers, in two roles (§ Mapper roles below): `IResourcePermissionMapper` (a
    module's own) and `IAuthoritativeResourcePermissionMapper` (replaces every other answer for
    what it claims);
  - `IAccessCeiling` - final denials no grant overrides;
  - `IScopeProvider` - which rows of the module's index tables a caller may list or search;
  - `ICallerContextContributor` / `ICallerRequestResolver` - what the module adds to the
    caller (an acting organization, business roles);
  - `IPermissionVersion` and the auditor - invalidation and the record.
  **The access gate owns the pipeline** (ruling 2026-10-11): pages and data alike. Every
  domain - media sharing, **Members**, **Parties**, and each module after them - plugs into it
  through these contracts and decides nothing on its own. That includes the page route table:
  its contract moves from `Crest.Server` (`ICrestRoutePermissionProvider`) into
  `Crest.Access.Abstractions` under a generic name, with modules registering their routes.
  The first such domain is **sharing**: its own module, `Crest.Sharing` and
  `Crest.Sharing.Abstractions` ([sharing.md](sharing.md), ruled 2026-10-11), which any module
  uses to make its items shareable (Media's files, Contents' items) and which registers one
  authoritative mapper and its scope providers with Access.

## The systems today

### Authentication

| Scheme | Where | What it does |
| --- | --- | --- |
| Identity cookie (`orchauth_{tenant}`) | `Crest.Users` | the default sign-in; `AddIdentity`, cookie paths from `UserOptions`; `UseAuthorization` at order 0 |
| The access gate | `Crest.Access` `IAccessGate` (`AccessGateService`), called by the shell selector; `AccessGateMiddleware` at order −150 for what the selector did not classify | the one authentication (cookie, or `Api` when an `Authorization` header is present), the side stamp, the caller; runs the request-handler schemes the platform's `UseAuthentication` used to run (OpenIddict's endpoints) |
| `Api` forwarder | `Crest.Infrastructure/Security/ApiAuthenticationHandler.cs` | forwards to the scheme in `ApiAuthorizationOptions` (default "Bearer"), then to each `AdditionalSchemes` entry (the remote deployment key); challenge writes 401 with `WWW-Authenticate: Bearer` |
| OpenId Validation | `Crest.OpenId` | the only bearer-to-principal path: OpenIddict validation, local (data protection, optionally another tenant's server) or remote (discovery); re-points `Api` to itself |
| OpenId Server | `Crest.OpenId` | issues tokens: code, password, refresh and client credentials; user flows carry the user's name identifier, the client-credentials flow carries `sub` = client id, the application's roles and their Permission claims, and **no name identifier** |
| OpenId Client, Management | `Crest.OpenId` | external OIDC login; admin UI for applications and scopes |
| External providers | `Crest.Microsoft.Authentication`, `Crest.Google`, `Crest.Facebook`, `Crest.GitHub`, `Crest.Twitter` | land in `Identity.External`, finished by the Users external-authentication feature |
| Member portal login | `Crest.Members` `MemberPortalController`, `MemberLoginSurfaceGate` | the portal's login, register and external-login; the `ILoginFormEvent` veto that keeps members off the tenant surface and staff off the portal |
| Crest JSON login | `Crest.Server` `LoginService`, `api/crest/auth` | replays the stock login event sequence and signs the one cookie |
| Workflows engine API | `Crest.Workflows` `ApiSecurityMiddleware`, order −151 | under `/crest-workflows/api`: the gated caller (none → 403), the decision, antiforgery, a transient claims identity with engine permission names, the Run list and the endpoint's policy; no authentication of its own |
| Secure media, GraphQL, media tus, OpenApi | `Crest.Media`, `Crest.Apis.GraphQL`, `Crest.OpenApi` | authorize the gate's principal; none authenticates again |
| Remote deployment key | `Crest.Deployment.Remote` `RemoteDeploymentKeyAuthenticationHandler` | `Authorization: RemoteDeployment {client}:{key}`, an additional `Api` scheme; the import runs as the remote client's application caller, which a contributor gives exactly `ImportRemoteInstances` |

The request path now: the shell selector (`ShellSelectionMiddleware`, a startup filter
before routing) classifies the request and calls the gate for it (`AdmitAsync` for pages
with the side it chose, `AdmitApiAsync` for API calls with the prefix side); the gate
authenticates once, publishes the result as the standard authentication feature (the
member session reads its ticket properties from it), stamps the side and builds the
caller. `AccessGateMiddleware` at the authentication order runs the request-handler
schemes and gates whatever the selector left (assets, infrastructure paths, hosts without
the selector) as Site. Nothing after it authenticates: the platform's `UseAuthentication`,
the workflows branch's, GraphQL's, secure media's, tus's, OpenApi's and the member
session's re-authentication are gone, and the `[Authorize(AuthenticationSchemes = "Api")]`
attributes are plain `[Authorize]`.

### Authorization

The platform's permission types are `Crest.Infrastructure.Abstractions/Security`
(`Permission`, `PermissionRequirement`, `IPermissionService`, `IPermissionProvider`).
`Crest.Security` is only the security-headers module. The stock handlers
(`PermissionHandler`, `RolesPermissionsHandler`, `SuperUserHandler`,
`ContentTypeAuthorizationHandler`) are gone; what decides a `PermissionRequirement` today is
`AccessAuthorizationHandler` delegating to the one decision; it is the only
`IAuthorizationHandler` (2026-10-10). What used to be nine resource handlers are mappers
and a ceiling inside the decision:

| Mapper or ceiling | Module | Maps |
| --- | --- | --- |
| `ContentResourcePermissionMapper` | Contents | per-type and owner variations; a bare string only for a content permission |
| `RolePermissionMapper` | Users | per-role user-management variants, `EditOwnUser` |
| `UserAuditTrailPermissionMapper` | Users audit trail | own versus others' events |
| `SiteSettingsPermissionMapper` | Settings | `ManageGroupSettings` to per-group permissions |
| `CustomSettingsPermissionMapper` | CustomSettings | `ManageResourceSettings` to per-type |
| `LocalizeContentPermissionMapper` | ContentLocalization | `LocalizeContent` to `LocalizeOwnContent` by owner |
| `IndexingPermissionMapper` | Indexing | `QuerySearchIndex` to per-index |
| `ManageMediaFolderPermissionMapper`, `ViewMediaFolderPermissionMapper` | Media | per-folder, own, attached-field (re-asks `ViewContent` for the item); the secure cache marker moved to the secure-media middleware |
| `WorkflowDefinitionAccessCeiling` (a ceiling) | Workflows | the definition's Edit and Run lists veto, never grant |

Two policy registrations exist (`MediaApi`, the media hub). The claims-based implied-by check
(`DefaultPermissionGrantingService`) is gone (2026-10-13); the role editor asks the decision. Rules, Layers, Liquid `is_in_role`, the approval service and the role cache provider
still read **cookie role claims**.

### The access machinery (`Crest.Access.Abstractions`, `Crest.Access`)

Contracts: `CallerContext` (tenant, side, user, organization, class, impersonator, super
user, roles, permissions, permission version, culture, scope signature), `CallerSide`
(Site, Admin, Member, System), `ICallerContextAccessor`, `ICallerContextFactory` with
`CallerRequest`, `CallerContextBuilder` and `ICallerContextContributor`, `IAccessRunner`,
`AccessHeaders`, `AccessDecision` and `IAccessDecision`, `IResourcePermissionMapper`,
`IAccessCeiling`, `ScopeRule`, `ScopeFilter`, `ScopeCondition` (equals, in, not in, the
comparisons `<` `<=` `>` `>=`, is null, and semi-joins: a column in, or not in, a set
`ScopeSelect` selects from another index table under its own filter; compiled by the YesSql
expression compiler, the structured compiler and the SQL rewriter, refused in memory, 2026-10-11),
`IScopeProvider`, `IScopeProviderSource` (providers whose targets are known at run time:
sharing's, one per shareable kind with an index table), `ScopeSet`,
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
Contributors and ceilings: `MemberCallerContributor`, `MemberClassCeiling` (Members),
`RemoteDeploymentCallerContributor` (Deployment.Remote), `WorkflowDefinitionAccessCeiling`
(Workflows). Mappers: the table under Authorization.
Consumers of the scope: Queries, the GraphQL content filters, the content-items API, the
pickers, the admin content lists. Only Crest.Server references the implementation; nineteen
projects reference the abstractions (the mapper modules joined them 2026-10-10).

### Shells

The **tenant shell** is resolved by `ModularTenantContainerMiddleware` through the running
shell table (host and prefix, then host, then prefix, then the default) and opened as the
request's `ShellScope`; `ModularTenantRouterMiddleware` moves the prefix into `PathBase`.
Deferred tasks and child scopes inherit the caller of the scope that queued them
(`IInheritedShellScopeFeature`). Background runs enter through a
synthetic `HttpContext` flagged `IsBackground`, whose short-circuit middleware sits at
`int.MinValue`, which is after the startup filters.

The **theme shell** is `ShellSelectionMiddleware` (Crest.Server, a startup filter):
infrastructure paths get a path strip only; `{shell}/api` and `/api` go through
`IAccessGate.AdmitApiAsync` (a valid `X-Shell` wins, an unknown value is 400, a cookie-authenticated call
without the header and without a prefix side is 403, else the prefix side or Site); pages
go through `IAccessGate.AdmitAsync` (authenticate, stamp the side, build the caller with `X-Org`); admin
pages add the login redirect and the route permission (Access's `IRouteAuthorization` over
every module's `IRoutePermissionProvider`, first template wins, no match denies); member pages add
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

## Defects found (2026-10-11): workflows, writes and the caller

A second audit, of the workflow wiring and the data paths against the access machinery.
Fixed the same day (compiled, tests written, not run):

1. **A caller leaked from one run into the next.** Clearing the accessor left the scope's
   caller feature in place, so a scope running several bursts answered the next with the
   previous caller. Clearing now clears the feature.
2. **The run's actor could be forged** through the API's dispatch input, an author's
   `DispatchWorkflow` input, an instance import or a resume's input. The actor is sealed and
   bound to its instance ([workflows.md](workflows.md) § Running identity).
3. **The system caller leaked into ordinary flows** (a flow triggered by a background task
   ran as the tenant super user). Only a definition published as system runs as system.
4. **A resume ran as whoever resumed it** (an approver, an HTTP caller). Every burst after the
   first runs as the run's own actor.
5. **Most data activities asked nothing.** The content activities, the user activities
   (create, assign and unassign roles, list by role), the tenant activities and the party
   activities now ask the decision against the item, role or user
   (`IAccessDecision.RequireAsync`/`AllowsAsync`); a read the caller may not make is "not
   found". Tenant activities are unit boundaries.
6. **The ownership guard passed with no signed-in user.** It decides for the gated caller and
   refuses a change with none; recipe steps now run as the caller that started the recipe, or
   as the system actor when none did (setup).
7. **A disabled user kept their rights**, and a deleted user's cookie or workflow actor became
   an "application" caller holding the roles its claims listed (the Administrator role made it
   a super user). Neither keeps any rights now; only a non-identity credential (a token, a
   remote deployment key) is read as an application.
8. **The engine API** kept engine permission claims already on the principal, allowed the GET
   form of execute, put activity tests under Run, and checked the Run list only when starting a
   flow. Claims are stripped before the grant, GET execute is 405, tests take Edit, and
   changing an instance takes Run on its definition.
9. **`ContentItemsController` published without `PublishContent`** and created against the
   base `EditContent` only; the user option source returned every user's email and phone to
   any picker user. Both decide per item, type and column now.
10. **`ValidateUser` read role claims** the cookie no longer carries; it reads the caller.

Still open from this audit:

- [x] **Anonymous actors.** Ruled 2026-10-11:
  - **Webhooks** run as the connection's **application caller**: each connection carries a
    machine identity with roles assigned on the connection (a billing integration may edit
    orders); the signature check stays as its authentication. Never anonymous.
  - **Login and registration triggers** capture the user the event names (the user just
    signed in, the user just created), not the anonymous request.
  - **Anonymous runs are opt-in per definition** ("accepts anonymous runs", set with
    `ManageShippedWorkflows` like run-as-system) and **only for page-URL HTTP endpoints**: a
    public page, and the public endpoints that serve specific public media and content. A
    backend API endpoint never accepts an anonymous run, except those public media and content
    endpoints for items marked public. A privileged step in an anonymous flow means publishing
    it as system. Every other anonymous run is refused with the reason.
  - **Pages are the request path's rule, not a workflow's**: in every theme shell (site, admin,
    member) the login, sign-up, password-reset and error pages admit the anonymous caller as
    page permissions.
  - **What landed (2026-10-13).** A connection carries `Roles` (`WorkflowConnection.Roles`, the
    connections API and page); `WorkflowWebhooksController` builds the connection's caller
    (`WorkflowConnectionCaller`, name identifier `connection:{key}`, an application) and starts
    the run as it; `WorkflowConnectionCallerContributor` reads the roles from the connection on
    every build, so a later burst holds what the connection holds then. Adding a role to a
    connection takes `AssignRoleToUsers` for that role; a role change or a deleted connection
    with roles bumps the permission version. The definition property
    `Crest.AcceptsAnonymousRuns` (`WorkflowsConstants.AcceptsAnonymousRunsProperty`, mirrored on
    `WorkflowDefinitionPart`) takes `ManageShippedWorkflows` to publish, like run-as-system;
    `WorkflowAccessGateMiddleware` refuses every anonymous burst with the reason
    (`WorkflowCallerResolver.AnonymousRefusal`) unless the definition accepts anonymous runs and,
    on the first burst, the run starts at an `HttpEndpoint` trigger. `IWorkflowTriggerPublisher.PublishForUserAsync`
    makes the flows of `user.logged-in`, `user.created` and `user.confirmed` act as the user the
    event names when the request is anonymous (an administrator creating a user stays the
    actor); the stimulus queue builds that caller after commit. No public media or content
    endpoint starts a flow today, so their exception has nothing to admit yet.
- [ ] **The activity gate fails open** for an activity that declares no permission. Fail closed
  with an allow-list of control-flow activities? Asked 2026-10-11: no preference given; to discuss.
- [ ] **Messaging activities** (e-mail, SMS, Twitter, Meta, notifications) and the engine's
  HTTP activities run for any caller; the HTTP ones have no SSRF guard and are not unit
  boundaries (`DownloadHttpFile` is now in the atomicity analyzer's list).
- [x] **Engine API instance reads** (list, journal, variables, export) take View only, with no
  per-definition narrowing; bulk instance actions, bookmark resume, task completion and event
  triggers are not checked against the Run list. *Closed 2026-10-13:* the View list and
  `EngineInstanceAccess` (workflows.md › Ownership and permissions, "a third list, View").
  Still open from this line: antiforgery on every write blocks bearer callers
  (machine-actors.md).
- [ ] **The member class ceiling denies nothing**: `MemberClassCeiling` is registered but
  `MemberPermissionCeilingOptions` is never configured, so a member role granted a tenant
  permission would hold it. Which permissions to ceiling is a ruling
  ([user-stories.md](user-stories.md) § 4).
- [ ] **`ApprovalService.CanDecideAsync`** compares role names instead of asking the decision.
- [ ] **`UsersController`** lists every user under one `ListUsers` check (with the Server
  extraction, [server.md](server.md)).
- **What landed (2026-10-13).** The engine's HTTP activities (Send HTTP request, Download
  HTTP file) use the connectors' guarded handler (`ConnectorNetworkPolicy.CreateHandler`, the
  shell's `AllowPrivateNetworks` opening it), so every connection, a redirect's included, reaches
  only public addresses. The messaging activities take no destination URL from the author (their
  providers' endpoints are tenant settings), so they have nothing to guard; who may run them is
  still the open item above. `UsersController` lists only the users the caller may list:
  everyone under the base `ListUsers`, else the users of the roles `ListUsers` is granted on
  (`ListUsersInRole_{role}`). Not built: the per-definition narrowing of instance reads.
  Ruled 2026-10-13: a third list, `Crest.Access.View`, narrows them; an empty View list means
  a tenant-wide definition readable by every `ViewCrestWorkflows` holder (workflows.md ›
  Ownership and permissions). Built the same day.

## Domains onto the extension system (2026-10-11)

What each domain still decides on its own, and what it registers instead. Each moves by hand,
module by module (abstractions placed per file, not by script).

**Members** (`Crest.Members`)
- Already registered: `MemberCallerContributor` (class, acting organization, binding roles,
  impersonator), `MemberCallerRequestResolver`, `MemberClassCeiling` (configured with no
  permissions - ceiling list is a ruling, [user-stories.md](user-stories.md) § 4).
- To move: `MembersController` asks a global `ManageMembers` for every organization's members,
  bindings, impersonation and conversion; it becomes `ManageMembers` asked **against the
  organization or binding** as the resource, decided by a members `IResourcePermissionMapper`
  that applies the member-admin template and the hierarchy root guard (who), so a member admin
  manages their own organization and nobody else's.
- To move: member reads (`MemberService`, `MemberAccess` lookups) go through scope - an
  `IScopeProvider` on `UserConnectionIndex` - instead of permission-only checks.
- To move here: `OrganizationScopeProvider` (in `Crest.Server` today), the organization row
  scope, which reads the caller's acting organization Members supplies (ties to
  [server.md](server.md) ruling S3).
- **What landed (2026-10-13).** `MembersController` asks `ManageMembers` against a
  `MemberOrganizationResource` (list, create) or a `MemberBindingResource` (add, remove binding)
  through the decision; `MemberResourcePermissionMapper` answers staff with the tenant-wide
  permission and a caller acting in an organization only for its own, a binding only when the
  user already holds one there (the root guard). `UserConnectionScopeProvider` scopes
  `UserConnectionIndex` (until 2026-10-13 `MemberOrgBindingIndex`; one row per (user,
  organization, class) since class became a set); `MemberService.ListByOrganizationAsync` reads through it and shows a
  member only its organization's bindings. `OrganizationScopeProvider` moved to Members;
  `OrganizationScopedTypes`, the part and the index stay in Server until S3 is ruled. Left: the
  member class ceiling lists `ManageMembers`, and a ceiling runs before any mapper, so a member
  admin is still refused on their own organization. The organization system actor's
  lookup of member admins (`MemberCallerContributor`) builds a caller and stays unscoped.
- **Ruling (2026-10-13): one permission model for staff and members, two reaches.** The
  model (sets, roles, groups, per-user overrides, the projection index, the editors) is in
  [permissions.md](permissions.md) § 3. What it means here:
  - Two organization permissions, neither on the member ceiling: `ManageOwnOrgMember` (every
    member; their own Person, contact points and positions in the organizations they are
    bound to - the Parties mapper grants view and edit of the caller's own Person through it)
    and `ManageOrgMembers` (an organization's members and contacts). Staff hold them through
    tenant groups and reach every organization; a member holds them through an organization
    group and reaches that organization only. The same permission, a different reach - no
    mapper reads the side. `ManageMembers` stays staff-only; the member class ceiling stays a
    hard line and is never conditional.
  - Staff are never members of an organization's groups (those are the organization's own,
    visible to its admins), and staff reach is the tenant: a new customer needs no grant.
    Narrowing a rep to particular organizations is a scope concern for later (Fruitful
    territories), not this store.
  - `MembersController` asks `ManageOrgMembers` against the organization or binding
    resource; the mapper grants it for the caller's own organization on the member side.
  - Ruled 2026-10-14: derived roles stay derived and feed the same index - business roles
    derive from the organization's roles, person roles from the person ([parties.md](parties.md)
    › Core party records). `CrestAssignmentPart`, its index, `AssignmentService` and
    `AssignmentScopeProvider` are retired (unused; per-item grants are Sharing's, a rep's
    customers are a CRM person role plus associations). `CrestOrganizationPart`, its
    index, `OrganizationScopedTypes` and `OrganizationScopeProvider` (back from Members) move
    out of Crest.Server into **Crest.Parties**: row ownership by organization concerns staff as
    well as members, and Crest.Members holds organization-to-member logic only (bindings, the
    acting organization, the member caller). Closes server.md S3.
  - **What landed (2026-10-13).** `ManageOwnOrgMember` and `ManageOrgMembers` (Members'
    permissions, names in `OrganizationPermissionNames`), not on the member ceiling.
    `MemberResourcePermissionMapper` maps `ManageOrgMembers` against an organization, a binding
    or an `OrganizationMemberResource`: a caller acting in no organization holds it for every
    one, a caller acting in one only there; the root guard (a member binding there) holds for
    bindings, and for putting a user in an organization's groups at every reach. The reach is
    the caller's acting organization, never the side. `MembersController` asks
    `ManageOrgMembers`; `ManageMembers` stays staff-only (settings). `PartyPermissionMapper`
    (Parties, feature priority 1 so it answers before the content mapper, whose candidates it
    keeps) adds `ManageOwnOrgMember` for the caller's own Person and `ManageOrgMembers` re-asked
    against the organization for the Organization and every Person holding a position there.
    The model itself: permissions.md § "What landed". The three proposals above stay
    unbuilt until confirmed.

**Parties** (`Crest.Parties`)
- Already: controllers ask `IAuthorizationService` per party item (routed to the decision).
- To move: `ILocationService` and `IPartyPositionsService` read without scope; they go through a
  parties `IScopeProvider` (and the organization scope for organization-owned parties).
- To move: party-specific rules (a person's own record, an organization's contacts) become a
  parties `IResourcePermissionMapper` rather than plain content permissions in each controller.
- To move: `PartiesRoutePermissionProvider` registers through the Access route contract.

**Pages** (`Crest.Server` today)
- `ICrestRoutePermissionProvider`, `CrestRoutePermission` and `CrestRouteAuthorizationService`
  move to Access (contract in `Crest.Access.Abstractions`, service in `Crest.Access`) under
  generic names; Server, Workflows and Parties register their routes; `AdminFilter` goes with
  the MVC admin.
- `AssignmentScopeProvider` (Server) moves to the module that owns assignments
  ([server.md](server.md) ruling S3).
- **What landed (2026-10-13).** The page gate is Access's: `IRoutePermissionProvider`,
  `RoutePermission` (template and permission name), `RouteAccess`, `IRouteAuthorization` and
  `RouteTemplates.Matches` in `Crest.Access.Abstractions`; `RouteAuthorizationService` in
  `Crest.Access` decides each route for the request's caller through the one decision (no
  longer from the principal). Server (`RoutePermissionProvider`), Workflows, Parties and
  host modules register their routes; the shell selector, `AppController`, `App.razor`
  and the route component table use the Access types. `IWebAssemblyRouteProvider` (a
  render-mode choice, not access) stays in Server. Left: `AssignmentScopeProvider` waits for
  S3 (the assignment part's home is open), and `AdminFilter` goes with the MVC admin, which
  still has its controllers.

**Parties, what landed (2026-10-13).** `PartyScopeProvider` scopes `LocationIndex` and
`PartyPositionIndex` as a semi-join on the caller's content item scope (content, assignment and
organization providers together); `LocationService` lists and looks up, and
`PartyPositionsService.ListPeopleAsync` lists, through it. The tenant's home Location
(`GetPrimaryAsync`, read by tax posting) stays unscoped configuration. `PartiesRoutePermissionProvider`
registers through the Access route contract. Not built: the parties resource mapper. The
ruling names its subjects (a person's own record, an organization's contacts) but not its
rules: which permission a person holds on the Person linked to their account, who counts as
an organization's contact and what they may do, and how it relates to the content mapper's
own-item variations it would replace for party items. That is a ruling to make.

### Mapper roles and priority (ruled 2026-10-11)

Who answers for a resource is decided by role and by the **module priority system** (the
manifest's `Priority` on a feature or module, read at run time through `IProviderPriority`
from the shell blueprint), never by the order modules registered their services in:

- **Authoritative** (`IAuthoritativeResourcePermissionMapper`): for a resource it claims, the
  only answer; no module mapper is consulted. Among authorities claiming one resource, the
  highest-priority feature's wins, so a module that is not built into Crest **replaces** an
  authority for exactly the resources it claims by declaring a higher priority; the lower one
  still answers for everything else. Two authorities claiming one resource **at the same
  priority** are a configuration conflict: the decision denies (finally), audits and logs it.
- **Module** (plain `IResourcePermissionMapper`): when no authority claims the resource, the
  highest-priority one that answers (registration order only between equal priorities).
- **No additive role** (ruled 2026-10-11): a mapper able to grant past the authority would be
  the authority for that resource. A module that wants to **add** to a domain's decisions feeds
  the authority instead - for sharing, an `IShareEntrySource` whose entries and rules the engine
  resolves with its own under the same precedence ([sharing.md](sharing.md)) - so a
  contribution wins only where the authority's rules let it. To take over, ship a
  higher-priority authority; to narrow, a ceiling.
- Tenant administrators pass before any mapper, so neither a conflict nor an authority stands
  in their way.

The same rule picks between modules inside a domain: two shareable kinds registered for one
name are decided by priority, and at the same priority the name is conflicted and allows
nothing ([sharing.md](sharing.md)). Tests: `test/Crest.Tests/Access/Conformance/MapperRolesTests.cs`.

## Overlaps to remove

| Overlap | Today | One system |
| --- | --- | --- |
| Permissions | ~~Permission claims in the cookie and in tokens, and the role manager read by the factory~~ (done 2026-10-10) | the factory only; `RoleClaimsProvider` stamps roles for identity, no Permission claims; tokens carry identity and roles |
| Implied-by | ~~`DefaultPermissionGrantingService` (instance) and `AccessDecisionService` (registry)~~ (done 2026-10-13) | the decision, resolving from the instance first and the registry second; the role editor asks the decision |
| Super user | the factory, the workflow definition handler, the definition resource | the caller's flag only |
| Definition access lists | handler and resource | the resource's `Admits`, called by the decision through a mapper |
| Resource to permission | ~~`IResourcePermissionMapper` (content) and eight resource handlers~~ (done 2026-10-10) | one mapper per resource kind, registered by its module (content, localization, custom settings, indexing, media manage and view, settings groups, user audit trail, roles and users); a candidate may re-ask for another resource; the definition lists are a ceiling; the handler list ends at `AccessAuthorizationHandler` |
| Invalidation | permission version, security stamp, SignalR nudge | the version, bumped by every rights write; the stamp for sign-out and credentials; the nudge fed from the version |
| Shell stamps | side item and route bucket item | one item set once by the selector |
| Authentication | ~~the gate, tenant `UseAuthentication`, the workflows branch, GraphQL, secure media, tus, member session~~ (done 2026-10-10) | the gate only |
| User class | cookie claim and contributor | the contributor |
| Roles | ~~cookie claims (Rules, Layers, Liquid, approvals, `Me`) and the caller~~ (done 2026-10-10) | the caller; the condition evaluators, Liquid, approvals, the roles cache context and `Me` read it |
| Content filters | caller scope plus the old per-type authorization in admin controllers | the scope |
| Route gates | Access's `IRouteAuthorization` (Blazor) and `AdminFilter` (MVC) | one route permission table; `AdminFilter` goes with the MVC admin |

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
- [x] A caller for the remote deployment import and a bump on super-user change landed
  2026-10-10 (§ 5, § 6). Still open: version bumps only for rights writes (user
  create/update/delete bump today); implied-by from the instance.
- [x] **What landed (2026-10-13).** Version bumps only for rights writes: role writes, a user
  deleted, disabled or enabled, a role membership added or removed (`UserStore`), a users recipe
  import, connection roles; a user created or updated no longer bumps. The decision resolves
  implied-by from the permission instance first and the registry second
  (`AccessDecisionService.DecideAsync(caller, Permission, ...)`, used by
  `AccessAuthorizationHandler` for every requirement); the role editor's granted view asks the
  decision, and `IPermissionGrantingService` with `DefaultPermissionGrantingService` is gone.
  The revocation check (`ARightRevokedBetweenTwoRequestsIsGoneOnTheSecond`,
  `ARoleRemovedFromAUserIsGoneOnTheNextRequest`) is in the suite, to run with the tests.

### 2. One calculation per request, handed down

- [x] **The gate is the only builder.** `ICallerContextFactory` is called from the gate
  (`IAccessGate`) and from the background entry points. `AccessAuthorizationHandler`
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
- [x] **Hand-down into inline work.** Verified 2026-10-10: workflow bursts started inline
  by a request (hooks, HTTP-endpoint workflows, the approval decision, which now resumes
  as the caller) inherit the request's caller through the accessor
  (`WorkflowAccessGateMiddleware`), and the actor snapshot is written on the first burst
  for later ones, which build their own.
- [x] **No hand-down across requests.** By construction since 2026-10-10: the cookie and
  tokens carry identity and roles only (no Permission claims), the actor snapshot is
  identity only, and every entry point builds a fresh caller under the current permission
  version. Still open: the conformance suite's check that a right revoked between two
  requests is gone on the second.
- [x] **One place reads the principal.** Landed 2026-10-10 for rights: the decision, the
  scope, Rules, Layers, Liquid `is_in_role`, approvals, the roles cache context and `Me`
  read the caller. Identity-level readers stay on the principal by design: the two-factor
  flows, the Liquid `has_claim` filter, OpenIddict's own endpoints.

### 3. One gate

- [x] Landed 2026-10-10: the gate is `IAccessGate` in `Crest.Access`, called by the
  theme-shell selector at the moment it needs the answer (it keeps classification, path
  shifting and the route table) and by `AccessGateMiddleware` at the authentication order
  for every request the selector did not classify; it authenticates once (cookie or `Api`
  by request kind), publishes the result as the authentication feature, stamps the shell,
  builds the caller and denies a disagreeing marker. The platform's `UseAuthentication`,
  the workflows branch's own authentication, GraphQL's, secure media's, tus's, OpenApi's
  and the member session's re-authentication are gone; the middleware runs the
  request-handler schemes (OpenIddict) in their place.
- [x] **The gate's authentication feature follows the platform's contract** (fixed
  2026-10-13). `GatedAuthentication` publishes the ticket as `IAuthenticateResultFeature`
  and the principal as `IHttpAuthenticationFeature`; its `User` setter kept the ticket, where
  the platform's `AuthenticationFeatures` retires it. The authorization middleware re-reads
  the ticket through the feature and writes it back, principal included, so a principal set
  after the gate - the workflow API gate's per-request grant - was replaced by the ticket's
  before the endpoint's policy ran: every engine API call at the tenant root failed its
  policy, and the cookie's forbid turned the 403 into a redirect to `/Error/403`. The setter
  now retires the ticket; a ticket set afterwards carries its principal. Pinned by
  `Crest.Workflows.Tests/ApiGateResponseTests` against the real authorization middleware.
- [x] **An API path never redirects** (2026-10-13). `AccessGate.IsApiPath` is the one rule
  (`/api`, `/crest-workflows`), read by the shell selector, `AccessGateMiddleware` and
  `ApiCookieResponses`, a post-configuration of the identity cookie registered by
  `Crest.Access`: on an API path the cookie's challenge answers 401 and its forbid 403, the
  status code pages opted out so `ApiProblemDetailsMiddleware` writes the body; a page keeps
  the login and access-denied redirects the Users module configures.
- [x] Static assets and infrastructure paths get the anonymous caller (built through the
  caller state cache, a hit after the first) rather than none.
- [x] Deferred tasks and child scopes inherit the caller of the scope that queued them, or
  the system caller when queued by a background entry point (`IInheritedShellScopeFeature`,
  `SystemCallerForBackgroundTasks`).

### 4. One decision

- [x] Landed 2026-10-10: the eight resource handlers are `IResourcePermissionMapper`s
  registered by their modules (localization, custom settings, indexing, media manage and
  view, settings groups, user audit trail, roles and users); the contract is async and a
  candidate may re-ask the decision for another resource (a folder's content item) or
  declare the resource public (`PermissionCandidate.Granted`); the workflow definition
  lists became `WorkflowDefinitionAccessCeiling`; the handler list ends at
  `AccessAuthorizationHandler`. The content mapper claims a string resource only for a
  content permission, since media paths and settings groups are strings too.
- [x] `RoleClaimsProvider` stamps roles only; client-credentials tokens carry the
  application's roles and no Permission claims (landed 2026-10-10).
- [x] Rules, Layers, Liquid `is_in_role`, the approval service, the roles cache context and
  `AuthController.Me` read the caller (landed 2026-10-10).
- [ ] The admin controllers' per-type content authorization goes; the scope is the filter
  (with the Crest.Server extraction, [server.md](server.md)).
  **Ruled (2026-10-13): no up-front permission check on the admin content list.** The
  route gate (`AccessAdminPanel`) admits the page; the list query carries the caller's view
  scope as a server-side predicate (the scope-set provider's rule, never a client filter), so
  a holder of only `ListContent_{type}` gets the list with that one type and nothing else.
  The stand-in `ListContent` check goes; the MVC list (`Crest.Contents` `AdminController`)
  goes with the MVC admin. **Landed 2026-10-13**: `ContentItemsController.ListAsync` asks
  nothing up front; its query carries the view scope.
- [x] `IAccessDecision` records every denial itself (landed 2026-10-10); reads per the
  audit ruling stay the operation's.

### 5. Machine callers

- [x] The application caller class (`CallerClasses.Application`): a principal with no user
  record resolves to its roles' permissions through the factory and contributors; the
  audit records it like any caller (landed 2026-10-10).
- [x] The remote deployment key is an `Api`-scheme credential (landed 2026-10-10):
  `RemoteDeploymentKeyAuthenticationHandler`, one of the forwarder's `AdditionalSchemes`,
  reads `Authorization: RemoteDeployment {client}:{key}`; the import controller is
  `[Authorize]` and asks `ImportRemoteInstances` (a new, security-critical permission the
  remote client's contributor grants its caller); the exporting instance sends the header,
  never the form field.
- [x] The `[Authorize(AuthenticationSchemes = "Api")]` attributes are plain `[Authorize]`
  (landed 2026-10-10).

### 6. Invalidation

- [x] One channel (landed 2026-10-10): the permission version, bumped by role, user,
  binding and super-user writes (the settings recipe step); the security stamp stays for
  sign-out and credential change at an explicit thirty-minute interval. An organization
  switch and an impersonation need no bump: the caller state is keyed by user and
  organization, so the switched-to state is built fresh. There is no SignalR nudge today;
  a client learns of a change on its next request.

## Decisions needed

- [ ] **Application callers and the member ceiling.** Does an application token acting for
  an organization (a member's integration) take the member ceiling and the organization
  scope, and how is that organization named in the token?
- [x] **The anonymous caller for assets.** Ruled 2026-10-10: built, through the caller
  state cache (one build per tenant, side and permission version; a hit afterwards).
- [ ] **Search scope.** Whether Lucene and Elasticsearch sources take the scope in the query
  (index-side filtering with the owner and organization columns indexed) or stay admin-only
  until they become connections (queries.md).
