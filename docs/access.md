# Access — who may do what, in one place

`Grove.Access` is the one system that decides who may do what: authentication, authorization,
permissions, roles, tenant and theme shells, OpenId, machine access. This document maps it:
its principles, how domains extend it, the parts as they stand, and what is still to change.
The operations model it serves (one request path, four pipelines) is
[workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery);
the member caller is described there too. The permission model (sets, roles, groups,
overrides, reach) is [permissions.md](permissions.md).

Who may do what, per kind of caller, for pages and for data and workflows (the reference for
tests and audits): [user-stories.md](user-stories.md).

## Principles

- **Access declares the access APIs; others register with Access** (the tenet in
  [architecture.md › Modules declare, Grove.Infrastructure.Server registers and serves](architecture.md#modules-declare-groveserver-registers-and-serves)).
  The sign-in, account, two-factor, login-settings and antiforgery APIs are declared by the
  access modules (Grove.Access.Users for account, sign-in, two-factor and login settings; Grove.Access
  for the antiforgery token) and registered in Grove.Infrastructure.Server's API endpoint registry
  ([MVC/api.md](MVC/api.md) P6). Grove.Infrastructure.Server hands each request to Access and applies the antiforgery filter, which asks
  Access to validate. Other modules register their access systems (page permissions, scope
  providers, resource mappers) with Access rather than checking for themselves. The access
  model's own API (permission sets, groups, overrides) is Access's too.
- **Access is its own module.** `Grove.Access.Abstractions` holds the contracts below
  `Grove.Data`; `Grove.Access` holds the caller builder, the decision, the scope compiler, the
  cache, the version and the auditor, always enabled. What remains of the platform's old
  pipeline is folded into it, never the other way round.
- **One request, one access calculation.** The caller, its decision inputs and its scope set
  are computed once at the gate for a request, and then **handed down** through everything
  that request causes on the server: the page render and its slots, the operations it
  dispatches, the child scopes, the deferred tasks and the after-commit work it queues, the
  workflow bursts it starts inline. Nothing in that chain rebuilds or re-derives the caller,
  and nothing reads rights from the principal. The hand-down ends with the request: a later
  request, a resumed workflow burst, a scheduled run or a queued job builds its own caller at
  its own entry point, never from a copy serialized by an earlier request (identity may be
  carried; rights never).
- **There is one decision, one caller and one scope.** Every surface asks the same decision
  through the platform's `IAuthorizationService` or directly; the caller is built once per
  request on the server from the identity and server-held state; row scope comes from the
  registered providers and nothing else. Anything that re-derives a right from cookie claims
  is a defect.
- **The gate owns authentication**, once per request, tenant first, then theme shell, then
  user, then permissions (workflows.md › Operations › Design decisions › Who authenticates).
  Nothing behind it authenticates again.
- **Identity in the cookie and in tokens; rights on the server.** Cookies and bearer tokens
  carry who the caller is. Roles, permissions, class, organization and ceiling are read from
  the store under the tenant's permission version, never from claims.
- **Opinionated, but every feature can be turned off.** Grove ships with most features
  enabled by default, unlike Orchard's pick-your-own model; every feature stays
  disable-able, and the access machinery works with any domain feature off: a feature
  registers its access extensions only while enabled, and nothing in `Grove.Access` names a
  domain module.
- **Access is extended, never rewritten.** The gate, the caller, the decision and the scope
  are one system that does all permission processing; domain modules keep their own access
  logic in their own projects and **register it** through the contracts in
  `Grove.Access.Abstractions`, so adding a domain never means editing the gate:
  - resource mappers, in two roles ([Mapper roles and priority](#mapper-roles-and-priority)):
    `IResourcePermissionMapper` (a module's own) and `IAuthoritativeResourcePermissionMapper`
    (replaces every other answer for what it claims);
  - `IAccessCeiling`: final denials no grant overrides;
  - `IScopeProvider`: which rows of the module's index tables a caller may list or search;
  - `ICallerContextContributor` / `ICallerRequestResolver`: what the module adds to the
    caller (an acting organization, business roles);
  - `IPermissionVersion` and the auditor: invalidation and the record.

  The access gate owns the pipeline, pages and data alike. Every domain (sharing, Members,
  Parties, and each module after them) plugs into it through these contracts and decides
  nothing on its own. Sharing is its own module, `Grove.Access.Sharing` and
  `Grove.Access.Sharing.Abstractions` ([sharing.md](sharing.md)), which any module uses to make its
  items shareable and which registers one authoritative mapper and its scope providers with
  Access.
- **A page's permission comes from the page registry.** Each page declares its permission once
  in its page registry entry ([architecture.md › One page registry](architecture.md#one-page-registry));
  the route gate and the admin menu read that same permission, resolved through the access
  model per tenant and per user ([admin-menu.md › The page registry](admin-menu.md#the-page-registry)).

## Mapper roles and priority

Who answers for a resource is decided by role and by the **module priority system** (the
manifest's `Priority` on a feature or module, read at run time through `IProviderPriority`
from the shell blueprint), never by the order modules registered their services in:

- **Authoritative** (`IAuthoritativeResourcePermissionMapper`): for a resource it claims, the
  only answer; no module mapper is consulted. Among authorities claiming one resource, the
  highest-priority feature's wins, so a module that is not built into Grove **replaces** an
  authority for exactly the resources it claims by declaring a higher priority; the lower one
  still answers for everything else. Two authorities claiming one resource **at the same
  priority** are a configuration conflict: the decision denies (finally), audits and logs it.
- **Module** (plain `IResourcePermissionMapper`): when no authority claims the resource, the
  highest-priority one that answers (registration order only between equal priorities).
- **No additive role.** A mapper able to grant past the authority would be the authority for
  that resource. A module that wants to **add** to a domain's decisions feeds the authority
  instead (for sharing, an `IShareEntrySource` whose entries and rules the engine resolves with
  its own under the same precedence, [sharing.md](sharing.md)), so a contribution wins only
  where the authority's rules let it. To take over, ship a higher-priority authority; to
  narrow, a ceiling.
- Tenant administrators pass before any mapper, so neither a conflict nor an authority stands
  in their way.

The same rule picks between modules inside a domain: two shareable kinds registered for one
name are decided by priority, and at the same priority the name is conflicted and allows
nothing ([sharing.md](sharing.md)). Tests: `test/Grove.Tests/Access/Conformance/MapperRolesTests.cs`.

## The system

### Authentication

| Scheme | Where | What it does |
| --- | --- | --- |
| Identity cookie (`orchauth_{tenant}`) | `Grove.Access.Users` | the default sign-in; `AddIdentity`, cookie paths from `UserOptions`; `UseAuthorization` at order 0 |
| The access gate | `Grove.Access` `IAccessGate` (`AccessGateService`), called by the shell selector; `AccessGateMiddleware` at order −150 for what the selector did not classify | the one authentication (cookie, or `Api` when an `Authorization` header is present), the side stamp, the caller; runs the request-handler schemes (OpenIddict's endpoints) |
| `Api` forwarder | `Grove.Infrastructure/Security/ApiAuthenticationHandler.cs` | forwards to the scheme in `ApiAuthorizationOptions` (default "Bearer"), then to each `AdditionalSchemes` entry (the remote deployment key); challenge writes 401 with `WWW-Authenticate: Bearer` |
| OpenId Validation | `Grove.Access.OpenId` | the only bearer-to-principal path: OpenIddict validation, local (data protection, optionally another tenant's server) or remote (discovery); re-points `Api` to itself |
| OpenId Server | `Grove.Access.OpenId` | issues tokens: code, password, refresh and client credentials; user flows carry the user's name identifier, the client-credentials flow carries the application's id as the name identifier and the application's roles, never Permission claims |
| OpenId Client, Management | `Grove.Access.OpenId` | external OIDC login; admin UI for applications and scopes |
| External providers | `Grove.Integrations.Microsoft`, `Grove.Integrations.Google`, `Grove.Integrations.Facebook`, `Grove.Integrations.GitHub`, `Grove.Integrations.Twitter` | land in `Identity.External`, finished by the Users external-authentication feature |
| Member portal login | `Grove.Access.Members` `MemberPortalController`, `MemberLoginSurfaceGate` | the portal's login, register and external-login; the `ILoginFormEvent` veto that keeps members off the tenant surface and staff off the portal |
| Grove JSON login | `LoginService`, `api/grove/auth` (Grove.Access.Users, `AuthApi`) | replays the stock login event sequence and signs the one cookie; an account with two-factor authentication passes the password step into the two-factor cookie and finishes with a code or a recovery code (`auth/two-factor`); `auth/external-login` starts an external sign-in |
| Sign-in form extensions | `ILoginExtensionProvider` (Grove.Access.Abstractions), served by `api/grove/auth/form/{form}` | what features add to the sign-in, registration and password recovery forms: links, a button per external authentication scheme (Grove.Access.Users), sections whose value the form sends as a header (ReCaptcha's `CaptchaChallenge`) |
| Self-service account APIs | Grove.Access.Users: `api/grove/account`, `api/grove/registration`, `api/grove/password`, `api/grove/two-factor` | the user's own account, registration, password recovery and two-factor authentication, behind the site's account pages ([shells-and-themes.md](shells-and-themes.md#account-and-protocol-pages)) |
| Workflows engine API | `Grove.Workflows` `ApiSecurityMiddleware`, order −151 | under `/grove-workflows/api`: the gated caller (none → 403), the decision, antiforgery, a transient claims identity with engine permission names, the Run list and the endpoint's policy; no authentication of its own |
| Secure media, GraphQL, media tus, OpenApi | `Grove.Media`, `Grove.Data.Protocols.GraphQL`, `Grove.Data.Protocols.OpenApi` | authorize the gate's principal; none authenticates again |
| Remote deployment key | `Grove.Deployment.Remote` `RemoteDeploymentKeyAuthenticationHandler` | `Authorization: RemoteDeployment {client}:{key}`, an additional `Api` scheme; the import endpoint (`RemoteImportApi`) requires authorization and asks `ImportRemoteInstances`, which the remote client's contributor grants its application caller; the exporting instance sends the header |

**The request path.** The shell selector (`ShellSelectionMiddleware`, a startup filter before
routing) classifies the request and calls the gate for it (`AdmitAsync` for pages with the side
it chose, `AdmitApiAsync` for API calls with the prefix side); the gate authenticates once,
publishes the result as the standard authentication feature (the member session reads its
ticket properties from it), stamps the side and builds the caller. `AccessGateMiddleware` at
the authentication order runs the request-handler schemes and gates whatever the selector left
(assets, infrastructure paths, hosts without the selector) as Site; static assets and
infrastructure paths get the anonymous caller (built through the caller state cache). Nothing
after it authenticates; controllers use plain `[Authorize]`.

`GatedAuthentication` publishes the ticket as `IAuthenticateResultFeature` and the principal as
`IHttpAuthenticationFeature`; its `User` setter retires the ticket (as the platform's
`AuthenticationFeatures` does), so a principal set after the gate (the workflow API gate's
per-request grant) survives the authorization middleware. Pinned by
`Grove.Workflows.Tests/ApiGateResponseTests`.

**A pending sign-in step.** A signed-in cookie session can owe a step before anything else: a
user whose roles require two-factor authentication and who has not set it up
(`TwoFactorSetupStep`, Grove.Access.Users, an `IPendingSignInStepProvider` of Grove.Access.Abstractions).
The gate asks the providers once per request and, while one answers, admits only the step's own
pages and APIs (the two-factor page and API, the email confirmation) and what every session keeps
(`PendingSignInSteps.AlwaysAllowed`: the shell runtime, the routing options and client strings,
the antiforgery token, signing in and out, the error pages): any other page is redirected to the
step's page, any other API call answered 403 with a problem body whose `reason` names the step
(`two-factor-setup-required`). A bearer caller has no such step.

**An API path never redirects.** `AccessGate.IsApiPath` is the one rule (`/api`,
`/grove-workflows`), read by the shell selector, `AccessGateMiddleware` and `ApiCookieResponses`,
a post-configuration of the identity cookie registered by `Grove.Access`: on an API path the
cookie's challenge answers 401 and its forbid 403, the status code pages opted out so
`ApiProblemDetailsMiddleware` writes the body; a page keeps the login and access-denied
redirects the Users module configures.

### Authorization

The platform's permission types are `Grove.Infrastructure.Abstractions/Security`
(`Permission`, `PermissionRequirement`, `IPermissionService`, `IPermissionProvider`).
`Grove.Security` is only the security-headers module. `AccessAuthorizationHandler` is the only
`IAuthorizationHandler`; it delegates every `PermissionRequirement` to the one decision and
records every denial. Per-resource rules are mappers and ceilings inside the decision,
registered by their modules; a candidate may re-ask the decision for another resource (a
folder's content item) or declare the resource public (`PermissionCandidate.Granted`):

| Mapper or ceiling | Module | Maps |
| --- | --- | --- |
| `ContentResourcePermissionMapper` | Contents | per-type and owner variations; a bare string only for a content permission (media paths and settings groups are strings too) |
| `RolePermissionMapper` | Users | per-role user-management variants, `EditOwnUser` |
| `UserAuditTrailPermissionMapper` | Users audit trail | own versus others' events |
| `SiteSettingsPermissionMapper` | Settings | `ManageGroupSettings` to per-group permissions |
| `CustomSettingsPermissionMapper` | CustomSettings | `ManageResourceSettings` to per-type |
| `LocalizeContentPermissionMapper` | ContentLocalization | `LocalizeContent` to `LocalizeOwnContent` by owner |
| `IndexingPermissionMapper` | Indexing | `QuerySearchIndex` to per-index |
| `ManageMediaFolderPermissionMapper`, `ViewMediaFolderPermissionMapper` | Media | per-folder, own, attached-field (re-asks `ViewContent` for the item) |
| `MemberResourcePermissionMapper` | Members | `ManageOrgMembers` against an organization, binding or organization member ([Members](#members)) |
| `PartyPermissionMapper` | Parties | own Person and an organization's people ([Parties](#parties)) |
| `OrganizationReachPermissionMapper` | Access | the access model's permissions at an organization's reach ([permissions.md](permissions.md)) |
| `WorkflowDefinitionAccessCeiling` (a ceiling) | Workflows | the definition's Edit, Run and View lists veto, never grant |

The decision resolves implied-by chains from the permission instance first and the registry
second (`AccessDecisionService.DecideAsync(caller, Permission, ...)`); the role editor's granted
and effective views ask the decision for a synthetic caller holding the role. Two policy
registrations exist (`MediaApi`, the media hub). Rules, Layers, Liquid `is_in_role`, the
approval service, the roles cache context and `AuthController.Me` read the caller; only
identity-level readers stay on the principal (the two-factor flows, the Liquid `has_claim`
filter, OpenIddict's own endpoints).

**The admin content list asks nothing up front.** The route gate (`AccessAdminPanel`) admits
the page; the list query carries the caller's view scope as a server-side predicate, so a holder
of only `ListContent_{type}` gets the list with that one type and nothing else
(`ContentItemsController.ListAsync`).

### The access machinery (`Grove.Access.Abstractions`, `Grove.Access`)

Contracts: `CallerContext` (tenant, side, user, organization, class, impersonator, super
user, roles, permissions, permission version, culture, scope signature), `CallerSide`
(Site, Admin, Member, System), `ICallerContextAccessor`, `ICallerContextFactory` with
`CallerRequest`, `CallerContextBuilder` and `ICallerContextContributor`, `IAccessRunner`,
`AccessHeaders`, `AccessDecision` and `IAccessDecision`, `IResourcePermissionMapper`,
`IAccessCeiling`, `ScopeRule`, `ScopeFilter`, `ScopeCondition` (equals, in, not in, the
comparisons `<` `<=` `>` `>=`, is null, and semi-joins: a column in, or not in, a set
`ScopeSelect` selects from another index table under its own filter; compiled by the YesSql
expression compiler, the structured compiler and the SQL rewriter, refused in memory),
`IScopeProvider`, `IScopeProviderSource` (providers whose targets are known at run time:
sharing's, one per shareable kind with an index table), `ScopeSet`, `IScopeSetProvider`,
`ScopedExecution`, `ScopeRefusedException`, `IAccessAuditor` and `AccessEvent`,
`IPermissionVersion`, `AccessGate` (the side item and header parsing), the page gate
(`RouteAccess`, `IRouteAuthorization`: the authorized routes, a path's check, and
`AllowsAnyAsync`, the per-item question the admin menu asks). The pages and their permissions
are the page registry's (`IPageRegistry`, `PageEntry`, `RouteTemplates` in
`Grove.Infrastructure.Server.Abstractions`).

Implementation (module "Access", always enabled, depends on Roles, Users, Settings):
`CallerContextFactory` (identity from the name identifier; roles from the user manager;
permissions from the projection ([permissions.md](permissions.md) › Projection) for Anonymous,
Authenticated and the user's roles; super user = admin role or the site's super user; then
contributors; state cached in `CallerStateCache` under the permission version),
`AccessDecisionService` (ceilings, super user, resource mappers, permissions with implied-by
chains), `AccessAuthorizationHandler` (the bridge from `IAuthorizationService`),
`ScopeSetProvider` (scoped: built once per request, never cached across requests; the scope
signature includes the user id), `PermissionVersionService` with `PermissionVersionBumper`,
`AccessAuditor` on the audit trail, `AccessRunner`, `SystemCallerForBackgroundTasks`,
`RouteAuthorizationService` (decides each route for the request's caller through the one
decision).

Scope providers: `ContentItemScopeProvider` (Contents), `OrganizationScopeProvider` (Parties),
`UserConnectionScopeProvider` (Members), `PartyScopeProvider` (Parties), sharing's sources;
compiled by `Grove.Data.YesSql` `ScopeExpressions`. Contributors and ceilings:
`MemberCallerContributor`, `MemberCallerRequestResolver`, `MemberClassCeiling` (Members),
`RemoteDeploymentCallerContributor` (Deployment.Remote), `WorkflowConnectionCallerContributor`,
`WorkflowDefinitionAccessCeiling` (Workflows), the ceilings of the permission model
([permissions.md](permissions.md) › Implementation). Consumers of the scope: Queries, the GraphQL
content filters, the content-items API, the pickers, the admin content lists. Only the host
references the implementation; every module that registers an extension references the
abstractions.

**The caller state cache** is a bounded memory cache (thirty minutes sliding, twelve hours
absolute), keyed on validated state only; an anonymous caller's key carries no organization.
The impersonator and the session's active organization are request-level facts supplied by
`ICallerRequestResolver` (`MemberCallerRequestResolver`) and never cached.

### Callers and entry points

- **The gate is the only builder** for a request. `ICallerContextFactory` is called from the
  gate and from the background entry points; `AccessAuthorizationHandler` builds one only
  when no gate ran (a host without the shell selector) and never a second one for a differing
  principal (it answers as anonymous). The workflow caller resolver's per-burst build is an
  entry point by design; nothing else calls the factory.
- **The caller is immutable for the request**, and so is its scope set.
- **Hand-down into child scopes.** `IInheritedShellScopeFeature` (Grove.Infrastructure.Abstractions) marks a
  scope feature that every scope created from the current one receives
  (`CreateChildScopeAsync`, the deferred tasks); the caller accessor keeps the caller as such a
  feature (`CallerFeature`), so child scopes, deferred tasks and after-commit work carry the
  caller that queued them, or the system caller when queued by a background entry point.
  Clearing the accessor clears the feature.
- **Hand-down into inline work.** Workflow bursts started inline by a request (hooks,
  HTTP-endpoint workflows, the approval decision) inherit the request's caller through the
  accessor (`WorkflowAccessGateMiddleware`); later bursts run as the run's own sealed actor
  ([workflows.md](workflows.md) § Running identity).
- **Background runs** enter through a synthetic `HttpContext` flagged `IsBackground`; the
  theme-shell selector skips them and the background entry point sets the system caller
  unconditionally. Only a definition published as system runs as system.
- **Recipe steps** run as the caller that started the recipe, or as the system actor when none
  did (setup).
- **Application callers** (`CallerClasses.Application`): a principal with no user record (a
  client-credentials token, a remote deployment key, a workflow connection) resolves to its
  roles' permissions through the factory and contributors; the audit records it like any
  caller. A disabled or deleted user's cookie or workflow actor holds no rights; only a
  non-identity credential is read as an application.
- **Anonymous actors.** Webhooks run as the connection's application caller (each connection
  carries roles; the signature check is its authentication); login and registration triggers
  act as the user the event names; anonymous runs are opt-in per definition and only for
  page-URL HTTP endpoints ([workflows.md](workflows.md) § Running identity). In every theme
  shell the login, sign-up, password-reset and error pages admit the anonymous caller as page
  permissions.

### Shells

The **tenant shell** is resolved by `ModularTenantContainerMiddleware` through the running
shell table (host and prefix, then host, then prefix, then the default) and opened as the
request's `ShellScope`; `ModularTenantRouterMiddleware` moves the prefix into `PathBase`.

**A tenant is chosen by the request's address, never by the user.** Each tenant declares its
hosts (`RequestUrlHost`, comma-separated, wildcard subdomains as `*.example.com`) and an optional
URL prefix; the shell table matches host with port and prefix, then host and prefix, then host
with port, then host, then prefix. Users, roles and data are per tenant, so one user name in two
tenants is two accounts, and auth cookies are scoped to the host name. The Default tenant
catches only requests no other tenant claims, and only while it declares no host: development
hosts give it their own host (`grove.localhost`, `fruitful.localhost`, `venti.localhost`), so a
request for any other name finds no tenant.

The **theme shell** is `ShellSelectionMiddleware` (a startup filter): infrastructure paths get
a path strip only; `{shell}/api` and `/api` go through `IAccessGate.AdmitApiAsync` (the shell
prefix decides the side; a valid `X-Shell` may only confirm it, a disagreeing one is 403, an
unknown value is 400; a cookie-authenticated call without the header and without a prefix side
is 403, else Site); pages go through `IAccessGate.AdmitAsync` (authenticate, stamp the side,
build the caller with `X-Org`); admin pages add the login redirect and the route permission
(`IRouteAuthorization` over the page registry: the most specific matching page decides, any
one of its permissions admits, no page or no declared permission denies); member pages add the login redirect only. A member-class user is refused on the admin
side and holds no tenant rights and no organization rows on the site side.
`RouteGateMatcherPolicy` keeps a shell from resolving another shell's endpoints; the member
prefix is `MemberOptions.MemberUrlPrefix`. A page's render mode (`[PageEntry(ClientOnly = true)]`)
is a hosting choice, not access.

### Users and claims

`DefaultUserClaimsPrincipalProviderFactory` runs the `IUserClaimsProvider`s at sign-in and
stamp refresh (roles, e-mail, two-factor, localization, user class, demo profile).
`RoleClaimsProvider` stamps roles for identity only, never Permission claims. The security stamp
serves sign-out and credential change at an explicit thirty-minute interval;
`OnRefreshingPrincipal` (`ConfigureSecurityStampOptions`) still copies old claims onto the
refreshed principal; that does not affect rights, since nothing reads them from claims.

### Invalidation

One channel: the permission version, bumped by rights writes only (role writes, a user
deleted, disabled or enabled, a role membership added or removed in `UserStore`, a users recipe
import, binding writes, super-user changes through the settings recipe step, workflow connection
roles, permission set writes and the access model's writes). A user created or updated does
not bump. An organization switch or an impersonation needs no bump: the caller state is keyed
by user and organization, so the switched-to state is built fresh. There is no SignalR nudge; a
client learns of a change on its next request. The conformance suite checks revocation
(`ARightRevokedBetweenTwoRequestsIsGoneOnTheSecond`, `ARoleRemovedFromAUserIsGoneOnTheNextRequest`).

## Domains on the extension system

### Members

`Grove.Access.Members` registers `MemberCallerContributor` (class, acting organization, binding roles,
impersonator), `MemberCallerRequestResolver`, `MemberClassCeiling` (the member ceiling list,
[user-stories.md](user-stories.md) § 4), `UserConnectionScopeProvider` and
`MemberResourcePermissionMapper`.

**One permission model for staff and members, two reaches** ([permissions.md](permissions.md) ›
The permission model):

- Two organization permissions, neither on the member ceiling: `ManageOwnOrgMember` (every
  member; their own Person, contact points and positions in the organizations they are bound
  to) and `ManageOrgMembers` (an organization's members and contacts); names in
  `OrganizationPermissionNames`. Staff hold them through tenant groups and reach every
  organization; a member holds them through an organization group and reaches that
  organization only. The same permission, a different reach: the reach is the caller's acting
  organization, never the side. `ManageMembers` stays staff-only (settings); the member class
  ceiling is a hard line and is never conditional.
- Staff are never members of an organization's groups, and staff reach is the tenant: a new
  customer needs no grant.
- `MembersApi` asks `ManageOrgMembers` through the decision against a
  `MemberOrganizationResource` (list, create), a `MemberBindingResource` (add, remove binding)
  or an `OrganizationMemberResource`. `MemberResourcePermissionMapper`: a caller acting in no
  organization holds it for every one, a caller acting in one only there; the root guard (the
  user already holds a member binding there) holds for bindings and for putting a user in an
  organization's groups at every reach. Member admin actions validate antiforgery.
- Member reads go through scope: `UserConnectionScopeProvider` scopes `UserConnectionIndex`
  (one row per user, organization and class); `MemberService.ListByOrganizationAsync` reads
  through it and shows a member only its organization's bindings. The organization system
  actor's lookup of member admins (`MemberCallerContributor`) builds a caller and stays
  unscoped.
- Grove.Access.Members holds organization-to-member logic only (bindings, the acting organization,
  the member caller). Row ownership by organization concerns staff as well as members and is
  Parties' ([Parties](#parties)).

### Parties

- `OwningOrganizationFieldSet`, `GroveOrganizationIndex`, `OrganizationScopedTypes` and
  `OrganizationScopeProvider` (the organization row scope, reading the acting organization
  Members supplies) live in `Grove.Parties/Server/Organizations` (namespace
  `Grove.Parties.Organizations`), registered by Parties; no abstractions project, since a
  module declaring scoped types references Parties anyway.
- Derived business roles: `PartyDerivedRoleProvider` counts an organization role only through
  the organization acted for and a person role only through the person
  ([parties.md](parties.md) › Core party records). Per-item grants are Sharing's; a rep's
  customers are a CRM person role plus associations.
- `PartyScopeProvider` scopes `LocationIndex` and `PartyPositionIndex` as a semi-join on the
  caller's content item scope; `LocationService` lists and looks up, and
  `PartyPositionsService.ListPeopleAsync` lists, through it. The tenant's home Location
  (`GetPrimaryAsync`, read by tax posting) is unscoped configuration.
- `PartyPermissionMapper` (feature priority 1, so it answers before the content mapper, whose
  candidates it keeps) adds `ManageOwnOrgMember` for the caller's own Person and
  `ManageOrgMembers` re-asked against the organization for the Organization and every Person
  holding a position there. Controllers ask `IAuthorizationService` per party item.
- The Parties pages are a kind page source (`PartiesPages`): All Parties and one page per
  tenant-kind party type, each gated by `ListContent`.

### Pages

The page gate is Access's: `RouteAuthorizationService` decides each route for the request's
caller from the page registry's entries, and the admin menu asks it per item, so a menu item is
shown exactly when its page would open. The shell selector and `AppController` (the manifest's
authorized routes) use the Access types.

## Backlog

- [x] **Route gate reads the page registry.** The route gate takes each page's permission from
  its page registry entry; the per-module route permission providers are gone
  ([architecture.md › One page registry](architecture.md#one-page-registry)).
- [ ] **MVC admin leftovers.** `AdminFilter` (`Grove.Infrastructure.Shells.Admin`) and the admin controllers'
  per-type content authorization (the MVC list in `Grove.Contents` `AdminController`) go with
  the MVC admin; the scope is the filter ([server.md](server.md)).
- [ ] **One shell stamp.** The selector stamps the route bucket and the side as two items;
  target: one item set once by the selector.
- [ ] **User class from the contributor only.** The user class is still a cookie claim
  (`UserClassClaimsProvider`) beside the contributor; target: the contributor.
- [ ] **Antiforgery and bearer callers.** Antiforgery on every write blocks bearer callers
  ([machine-actors.md](machine-actors.md)).
- [ ] **Self-service custom domains.** A tenant's own administrator adds and removes the
  tenant's domains and subdomains as a tenant setting, without host-level tenant administration:
  ownership is proved by a DNS TXT record before a domain is accepted, a certificate is issued
  and renewed for each verified domain (ACME), a domain claimed by another tenant is refused,
  and every change reloads the running shell table. Today hosts are set only on the host's
  Tenants page (`RequestUrlHost`).
- [ ] **Public media and content endpoints that start a flow.** None exists, so the anonymous
  exception for them has nothing to admit yet.

## Open questions

- **The activity gate fails open** for a workflow activity that declares no permission. Fail
  closed with an allow-list of control-flow activities?
- **Messaging activities** (e-mail, SMS, Twitter, Meta, notifications) run for any caller. They
  take no destination URL from the author (their providers' endpoints are tenant settings), so
  there is no SSRF concern; who may run them is open. (The engine's HTTP activities use the
  connectors' guarded handler, `ConnectorNetworkPolicy.CreateHandler`, so every connection,
  redirects included, reaches only public addresses unless the shell's `AllowPrivateNetworks`
  opens it.)
- **Parties mapper rules.** Who counts as an organization's contact and what they may do, and
  how the parties mapper relates to the content mapper's own-item variations it would replace
  for party items.
- **Application callers and the member ceiling.** Does an application token acting for an
  organization (a member's integration) take the member ceiling and the organization scope,
  and how is that organization named in the token?
- **Search scope.** Whether Lucene and Elasticsearch sources take the scope in the query
  (index-side filtering with the owner and organization columns indexed) or stay admin-only
  until they become connections ([queries.md](queries.md)).
