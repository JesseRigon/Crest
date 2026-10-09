# Members — user classes, hierarchies, org bindings, member portals

`Crest.Members` is the organization-bound side of a tenant's users: the user class that
separates staff from members, the per-tenant user hierarchy, the org bindings that tie a
member to the organizations they act for, the member portal's login and session, the
class permission ceiling, impersonation and class conversion. This document describes
what is built and why it has the shape it has. Design that is not built yet — the super
tenant, the three-class naming, the binding record, the content-side layering, data
separation by organization, tenant SSO — is in [members.md](members.md).
The party model it builds on is [parties.md](parties.md).

## Terminology

- **Member** — a user who belongs to an ORGANIZATION within a tenant (a
  customer's or vendor's person). The class name for what was earlier sketched
  as "customer users" / "org users".
- **Membership** — the member's relationship record. Tiers and perks on top of it are a
  downstream subscription module's, not Members' (see below).
- **Member portal** — the per-organization portal surface members sign in
  through. A vendor org has a vendor-flavored member portal on tenant A; a
  customer org has a customer-flavored one on tenant B; same class of thing.

## Members and subscriptions are two systems

Members is access control: org-users, their org bindings and roles, the hierarchy store,
the permission ceiling, impersonation and the portal session. The loyalty/subscription
system — subscriptions, seats and entitlements, tier and perk definitions — is not part
of `Crest.Members`; it is a separate downstream module built on it. Crest has no group
concept (ruling 2026-10-05, Decisions needed › Grouping).

The two remain independent facts about a person: holding a seat in a subscription
says nothing about org-user access, and being an org-user with customer-portal
access implies no seat. What they SHARE is the resolution context — an org-user's
active-org session — which is why they kept getting modelled as one system.

Members raises a member's organization-binding lifecycle through
`IMemberLifecycleHandler` (`MemberBoundAsync` / `MemberUnboundAsync`, resolved in a fresh
scope after the binding write commits) and knows nothing more about its consumers.

- [ ] **Access-policy seam (ruling 2026-10-05).** A downstream module may affect a member's
  access without Members knowing why: Crest consults registered access policies at
  portal sign-in, organization switch and session load, and a policy may refuse portal
  access for that organization or narrow the member's permissions there. A subscription
  module uses it so an unpaid membership loses access; what it loses is that module's and
  the tenant's business, not Crest's.

  **When policies and the principal are evaluated (ruling 2026-10-06): one cache.** Each
  session holds one cached state per (user, tenant, side, organization) — the built
  principal and the policies' verdict — and the online and offline views are the same cache,
  updated together.
  - **The session expires as a whole after a set length — a tenant setting the tenant admin
    edits, default 7 days**; then the user signs in again.
  - **While online, the cache refreshes on an interval** during those 7 days, and Crest's own
    binding and role writes and the policy modules bump a version token that refreshes it at
    once. The interval is configurable. "Session load" means the next refresh.
  - **Offline**, the client works from the last state it received until the session expires.
    Nothing is offline today; this keeps the door open. **Offline security (ruling
    2026-10-06):** permissions held on a device can never be secured against its owner, so
    they guide the offline UI only and are never authority.
    - The server is the only authority: offline actions are queued as intents, and on
      reconnect the server re-authorizes each against the user's current permissions,
      rejecting or flagging what is no longer allowed.
    - The offline copy is a server-signed permission snapshot (permissions, organization,
      issued, expiring with the session) bound to a registered device; it cannot be altered undetected,
      the signature is checked on sync, and a lost device is revoked.
    - The snapshot and offline data are encrypted at rest with a non-exportable key
      (WebCrypto in the browser, the OS keychain in a native shell), protecting them from
      others on the device.
    - Reads are the real exposure: the server decides by permission, at download time, what
      goes offline, and sends only what is needed, encrypted and expiring. Offline actions
      are audited on sync, flagged as offline.
  - A refusing policy removes access to that organization on the member side only; it never
    signs the user out of the staff side.

## The two classes of user in a tenant

Both classes are ORDINARY ORCHARD USERS in the tenant's own user store — same
shell context, same user-management surfaces, same roles/permissions machinery,
same hierarchy system (ruling: "organization users should be treated the same
as tenant users in terms of hierarchy, user management, and permissions
functions in the shell context").

| | Tenant users (staff) | Members |
| --- | --- | --- |
| Account lives in | the tenant's user store | the tenant's user store (same) |
| Managed by | tenant admins | tenant admins + the org's MEMBER ADMIN (portal-side, org-subtree-scoped) |
| Hierarchy | per-tenant hierarchy store | same store; a member's subtree hangs under their organization |
| Permissions | Orchard roles/permissions | Orchard roles/permissions (portal-shaped roles) |
| Login surface | tenant admin/site login | the MEMBER PORTAL only — never the tenant login |
| Tenant SSO (Default-as-IdP, later) | eligible | **excluded, by ruling** |
| External IdPs (Google, GitHub, …) | per tenant settings | per tenant settings — same mechanism, allowed |
| Data scope | tenant-wide per role (+ hierarchy) | their ORGANIZATION's records only (+ hierarchy within it) |

**Class marker — a property on the user record**, never a
role. Roles are what tenant admins grant and revoke daily; the class decides
which login surfaces accept the account at all, so it must not be grantable —
a mis-granted "member" role would lock staff out, a removed one would let a
member into the staff login. The property is stamped at account creation by
the creating flow (staff-creation stamps staff; member-portal provisioning
stamps member) and indexed via a custom `IndexProvider<User>` map index
(UserId + Class — the same mechanism as stock `UserIndexProvider`), so admin
user lists filter by class with a real SQL WHERE, reliably.

**Class conversion**: the property IS updatable — but only
through a dedicated, permission-gated administrative action (its own
permission, never the role editor), so a member hired onto staff converts
keeping their account, credentials and external logins; the conversion is a
natural audit event. Conversion adds or removes a class in either direction; adding staff
keeps the org bindings, removing member ends them (ruling 2026-10-05, One account, both
sides).

**Class permission ceiling**: some permissions are NEVER
valid for the member class — tenant settings editing is the canonical example
— even when a superadmin assigns a role carrying them. A code-declared
registry (modules contribute their staff-only permissions alongside their
normal `IPermissionProvider`) backs an authorization handler that FAILS the
check for a member-class principal asking for a ceilinged permission; in
ASP.NET authorization an explicit fail overrides every success, so the grant
simply cannot take effect. Role-editor UX may additionally warn or hide, but
the ceiling is enforced at authorization time, not by UI politeness.

Enforcement of the class itself is a login-channel gate, not merely a
permission — a member authenticating against the tenant login must be refused
even with valid credentials. Staff reach a member portal only via
IMPERSONATION (below), never by logging in as the member — impersonation
starts from an authenticated staff session and never passes through a login,
so the class gate does not interfere with it.

## Hierarchies

**The hierarchy is relational, not a tree of slots (ruling 2026-10-05).** It is a set of
manager → report relationships between users, not an entity users are placed into: a user
can have two managers, a manager many reports, and no two users' patterns have to match.
It answers ownership and visibility — who sees whose records — and nothing else;
permissions stay a separate system (roles).

**Relationships are typed.** *Reports-to* is the hierarchy: it grants a manager visibility
of their reports' records, and it stays within one root (staff, or one organization).
Other types are **associations**, and they may cross between staff and an organization: a
large customer organization can have several sales reps assigned, each connected to
different contacts there. The tenant holds all of it, but each rep sees only what their
own connections give them, not the whole organization, and the organization's people do
not see the reps' side either. An association grants no visibility of the other party's
data by default — a rep does not see their contact's data just by being connected —
unlike a direct report. "Teams" are this hierarchy; there is no separate teams table and
no group concept. The single-parent design below
(one node per user, materialized path) is what is built and is superseded: see Still to
build › Relational hierarchy.

**Hierarchies live in a custom DB TABLE in the
tenant's store** (physically per-tenant already via TablePrefix) — not a
separate database file, and not edges scattered across content items. Orchard
stores no user hierarchy natively (roles are flat; the only stock tree is
taxonomy TERMS, which is content categorization) — this is ours entirely.

**Shared vs separate stores: ONE shared table**, a forest with
one root for tenant staff and one root per organization. Decisive argument: the
org-A-vs-org-B boundary INSIDE a members-only table is exactly as sensitive as
the staff-vs-member boundary, so the mandatory root-scoped write/read guard has
to exist regardless — a second table duplicates schema/service/queries without
adding protection the guard does not already provide. The shared design also
keeps the scope resolver class-blind (it only asks subtree(user)) and reuses
one tree-management code path for staff admins and member admins alike. The
"no crossover" policy is enforced by the guard: every query and write carries
the root/organization id. (Separate stores' one real advantage — a member-admin
bug physically cannot touch staff rows — is bought by that same guard.)

Implementation: adjacency (parent id) for
integrity plus a materialized-path column for subtree queries — subtree is a
path-prefix match, because YesSql's query layer cannot express recursive SQL.

What the hierarchy store must answer (the scope resolver's queries):
- subtree(user) — "me and everyone under me", the expansion step in front of
  the existing fail-closed scope machinery (6d/6e assignment scoping): a
  manager sees the records their subtree may see.
- chain(user) — path to root, for escalation/approval routing later.
- Membership of a node: user id + parent + the organization (or tenant root)
  the subtree belongs to.

The subtree expansion is not wired into the scope machinery yet — see
[members.md](members.md).

## Members and organizations

- A member links to a **Person**, and the Person's org position links to the
  **Organization** — the party model ([parties.md](parties.md)) carries the
  relationship; the user record carries authentication and roles. The
  user↔Person link is built on both sides: `Person.PortalUser` is a
  real `UserPickerField` (indexed via `OrchardCore.ContentFields.Indexing.SQL.
  UserPicker`, so "which person is this account" is a query —
  `IPartyUserLinkService.FindPersonIdForUserAsync`), and
  `Crest.Members.CreateMemberAsync` writes it through
  `IPartyUserLinkService.LinkPortalUserAsync` whenever a `PersonId` is supplied.
- **Member admin**: each organization's member portal has
  a member admin — DEFAULTING TO THE FIRST MEMBER ADDED for that org. Their
  scope, precisely: manage their org's MEMBERS (hierarchy and sub-users),
  member DATA and CREDENTIALS, and the ORG SETTINGS that future modules'
  permission systems expose to them (e.g. autopayment scheduling, payment
  types / wallet connections). Explicitly NOT theirs: tenant features,
  extensions, tenant settings, users outside their org, data outside their
  org. Enforcement is two-layered: the hierarchy store's root guard bounds the
  WHO, and ordinary Orchard permissions (tenant-governed, below) bound the
  WHAT — the member admin holds a role the tenant defines, not a parallel
  authority.

## Permissions — tenant-governed, on Orchard's own pipelines

Role permissions are managed by TENANTS, never by organizations: what each
role can or cannot do is defined in the tenant's role management, and an org's
member admin merely holds roles the tenant shaped. Orgs choose people, tenants
choose powers.

The member system integrates on OrchardCore's permission system's established
patterns rather than beside them:

- Every module contributes its permissions via standard `IPermissionProvider`
  implementations (with stereotype defaults), so member-portal abilities
  appear in the tenant's ordinary role editor like any other permission —
  including future org-settings permissions (autopayment scheduling, wallet
  connections), which their owning modules declare when they exist.
- Authorization flows through `IAuthorizationService` and Orchard's
  authorization handlers; org scoping and hierarchy expansion NARROW what a
  granted permission reaches (the existing fail-closed scope machinery), they
  never grant. Permission says "may manage members"; scope says "of THIS org's
  subtree".
- Per-org roles ride the same rails: the active org binding contributes its
  role claims into the request principal the way Orchard's role system does,
  so every downstream permission check works unchanged.

## Staff support access = impersonation

Staff may enter a member portal for support, but via an impersonation session,
not by authenticating as the member. Attribution splits deliberately in two:

- **Functional attribution**: everything behaves as the member — a generated
  invoice's CreatedBy field is the impersonated member, scoping is the
  member's org scope, the portal renders as that member would see it.
- **Audit attribution**: the auditing system records every action as the STAFF
  user — explicitly "created by <staff user> impersonating <org member>" —
  never as the member alone.

Recording is [audit.md](audit.md). The requirement here is that the impersonation
session carries BOTH identities, so the audit layer can attribute every action
to the staff user without any write path knowing about impersonation.

## Multi-org members

One member ACCOUNT can hold multiple org bindings, with DIFFERENT ROLES PER
ORG; the member's UI and effective permissions change with the org they are
acting in. Members get an ORG SWITCHER component to change the active org.

Design consequence: Orchard roles are per-user,
not per-user-per-org, so the org binding record carries the org-scoped roles
and the Members module resolves effective permissions from (user, ACTIVE
org binding) —
the natural shape is contributing the active binding's role claims into the
request principal the same way Orchard's own role system does, so downstream
IAuthorizationService checks keep working unchanged. The hierarchy store
places the member once PER BINDING (a node under each org's root).

## Implementation directive

Study OrchardCore's own user,
permission, and role management implementations first and MIMIC them wherever
they fit — services, stores, events, admin patterns — so the whole system
reads as a seamless EXTENSION of Orchard, not an addition beside it (this
applies to the member system and to any Crest seam it rides —
see Ownership). But no square pegs
in round holes: where Orchard's shape genuinely does not fit (per-org roles,
the hierarchy store), diverge deliberately and document why.

## Identity and login flows

- **Member portal login**: per-organization portal surface; local tenant
  accounts and/or external identity providers (Google, GitHub, …) exactly as
  the tenant configures them — the stock OrchardCore.Users external
  authentication features, with auto-provisioning creating the member-class
  user on first sign-in (class + org binding assigned by the portal's
  registration flow, never by the raw external callback).
- The login-channel gate: which surface may authenticate which class is
  enforced by the Members module through Orchard's auth pipeline per tenant
  (the member portal endpoint
  only issues sessions for member-class users; the tenant login only for
  staff-class), built into the platform's own authentication rather than a second
  authentication system beside it.

## Ownership: Crest, as `Crest.Members`

Members is part of the application layer
([application-layer.md](architecture.md)): every business
with a public face has members on the other side of it, and the member portal
is how a product reaches them. Members builds on `Crest.Parties` (a member is
a Person with a portal user; an org binding points at an Organization) and on
Orchard's user, role and permission pipelines; nothing in it is specific to a
line of business. Commerce is the one thing it does not own: Members declares
the binding lifecycle seam (`IMemberLifecycleHandler`), and downstream modules keyed by
(member, organization) — subscriptions, billing — act on it.

## What is stock

Verified against the vendored OrchardCore source:

- STOCK: per-tenant isolation (own shell, DI, YesSql store, per-tenant user
  documents — no global user table); flat roles/permissions; external login
  providers with auto-provisioning and scriptable role mapping; OpenID Server +
  Client + Validation features for the later tenant SSO.

## How it is built (against the vendored source)

Everything below cites verified mechanics — file paths and seams confirmed in
the platform source (`src/`, then the OrchardCore fork).

### A. Class marker foundation

- The class is a POCO in `User.Properties` via the stock entity-aspect pattern
  (`EntityExtensions.GetOrCreate<T>/Alter<T>/TryGet<T>`, keyed by type name —
  OrchardCore.Entities). NOT the Custom User Settings feature: verified those
  are unqueryable (`CustomUserSettingsService` enumerates every user in
  memory).
- Stamped in `IUserEventHandler.CreatingAsync` — fires in `UserStore.CreateAsync`
  BEFORE `SaveAsync`, so the stamp persists in the same write and the index
  maps it on first save; `UserCreateContext.Cancel` even gives a creation veto.
  Registered `AddScoped<IUserEventHandler, …>` (precedent:
  `UserDisabledEventHandler`).
- Filtering: our own `IndexProvider<User>` (`UserClassIndex`: UserId + Class),
  registered `AddIndexProvider<T>` — must be singleton-safe (the
  `ILookupNormalizer` note in Users/Startup.cs:81); out-of-module precedent:
  `UserByRoleNameIndexProvider` registered from RolesStartup. There is NO
  YesSql index rebuild path — backfill is a data migration doing a paged
  load-and-resave of users (stock precedent: Users/Migrations.cs UpdateFrom).
- A `class` claim is added at principal creation via `IUserClaimsProvider`
  (collected by `DefaultUserClaimsPrincipalProviderFactory`), so the ceiling
  handler and login gates read the principal, not the store, per check.

### B. Login-channel gate

- `ILoginFormEvent.ValidatingLoginAsync(IUser)` is THE veto seam — the only
  member that can refuse an otherwise-valid credential, and it fires at all
  four sign-in points (AccountController.Login + the three external-login
  paths), so one handler covers local and external logins. Stock precedents to
  copy: `DisabledUserLoginFormEvent`, `UserModerationLoginFormEvent`.
  (`IsLockedOutAsync` is notification-only — cannot veto; verified.)
- The gate reads the class POCO: member at the tenant login surface → refuse;
  the member portal's login flow conversely only signs in member-class users.
  `RoleLoginSettings` is the closest stock analogue (role-conditional login
  policy) and worth mirroring for settings shape.

**Crest's JSON login fires the same seam.** Crest's JSON login
(`CrestLoginService`) fires the standard `ILoginFormEvent` sequence exactly
like the stock MVC AccountController (LoggingIn → AuthenticateAsync →
ValidatingLogin veto → SignIn → LoggedIn, with LoggingInFailed on every failure
path) - so moderation, email-confirmation, audit-trail login recording and the
member gate all apply to Crest logins through the ONE standard seam. A veto's
MVC IActionResult is TRANSLATED, not executed: redirect-shaped results yield
their target path (`~/` normalized away) and the handler's TempData `error_*`
messages fill the 401 payload (`{errors, redirect}`); untranslatable results
degrade to a generic refusal - refusal is always the fail-safe direction. 2FA
behavior is unchanged (AuthenticateAsync's built-in checks run exactly as
before). The ILoginFormEvent gate covers every tenant login surface by itself.

### C. Member portal sessions — one cookie, not two

- RULING-GRADE finding: `SignInManager` is hard-wired to
  `IdentityConstants.ApplicationScheme`; a second cookie scheme would mean
  reimplementing two-factor, lockout, security-stamp and external login.
  Portal sessions therefore ride the SAME per-tenant Identity cookie
  (`orchauth_<tenant>`, Path deliberately unset — preserve that) and are
  distinguished by claims + AuthenticationProperties.
- `AuthenticationProperties.Items` round-trips (in-tree precedent:
  ExternalAuthenticationsController reading RememberMe back on a later
  request) and lives server-side when `CacheTicketStore` is active — the home
  for `active-org` and `impersonator` session state. Attaching items at
  sign-in needs `SignInManager.SignInAsync(user, properties, method)` (the
  password path doesn't take properties), or set later in OnValidatePrincipal
  with `ShouldRenew = true`.

### D. Active organization and per-org roles

- Per-request enrichment: a custom `CookieAuthenticationEvents.OnValidatePrincipal`
  registered via `IConfigureNamedOptions<CookieAuthenticationOptions>` for the
  application scheme (mirroring stock `CookieAuthenticationOptionsConfigure`),
  CHAINED to `SecurityStampValidator.ValidatePrincipalAsync` — replacing it
  would silently disable stamp validation. No in-tree precedent exists (and
  Orchard registers no `IClaimsTransformation` either — both seams are free);
  OnValidatePrincipal wins because it can read/write Properties and
  `ReplacePrincipal`.
- Org switch = update the active-org item + `RefreshSignInAsync` (stock uses
  it after profile-affecting changes). Caveat verified: stamp refresh copies
  old claims ADDITIVELY (`ConfigureSecurityStampOptions.OnRefreshingPrincipal`)
  — stale org claims can survive, which is one more reason enforcement never
  trusts claims curation (the ceiling handler is authoritative).
- Per-org roles are NOT thousands of Orchard Role rows: `RolesDocument` is a
  single cached per-tenant blob (verified — no partitioning, no index), so org
  volume would bloat it. Instead the tenant defines a SMALL set of ordinary
  member role templates (Member, MemberAdmin, …) in the normal role editor —
  tenant governs powers — and the ORG BINDING record says which template the
  member holds in which org. The enrichment step contributes the ACTIVE
  binding's role + permission claims exactly the way `RoleClaimsProvider`
  does at sign-in (role-name claim + the role's `"Permission"` claims), so
  every downstream check works unchanged.
- Verified consequence to design around: role→permission expansion is a
  SIGN-IN-TIME SNAPSHOT into the cookie — editing a role's permissions does
  not affect signed-in users until principal refresh. Acceptable for staff
  (stock behavior); the per-request enrichment makes the ACTIVE-org claims
  fresher than stock, not staler.

### E. The class permission ceiling

- Shape (verified compositional): a RAW `IAuthorizationHandler` mirroring
  `SuperUserHandler`, iterating `context.Requirements.OfType<PermissionRequirement>()`,
  calling `context.Fail()` when the principal carries the member class claim
  and the permission name is in the ceiling set. NO `HasSucceeded` guard —
  this handler must run despite prior successes.
- Why Fail() is the only working design (all verified): stock Orchard NEVER
  calls Fail (all handlers additive); admins bypass permission claims entirely
  (`SuperUserHandler` succeeds all requirements on the admin ROLE NAME, and
  `RoleClaimsProvider` gives admins ZERO permission claims) — so claims
  filtering cannot ceiling an admin-role member; `HasFailed` is sticky and
  evaluation is `!HasFailed && HasSucceeded`, and `InvokeHandlersAfterFailure`
  stays default-true, so our Fail beats SuperUserHandler regardless of order.
- Ceiling set: `HashSet<string>(OrdinalIgnoreCase)` of permission NAMES
  (Permission equality is reference-based — never set-of-instances), CLOSED
  over dynamic expansions: `ContentTypeAuthorizationHandler` and
  `RoleAuthorizationHandler` re-enter `AuthorizeAsync` with derived
  permissions (`Publish_{Type}`, per-role user-management variants), so
  ceiling the coarse template alone is insufficient — the registry must
  expand templates the way `ContentTypePermissionsHelper` does.
- Resolve the admin role via `ISystemRoleProvider.GetAdminRole()` — the name
  is tenant-configurable, never hard-code "Administrator". Anonymous/
  Authenticated pseudo-role claims are applied LIVE per request by
  `RolesPermissionsHandler` (not from the cookie) — covered, since the
  ceiling acts at authorization time.
- Bonus verified: the role editor's effective-permissions preview builds a
  fake principal and calls the real IAuthorizationService — our ceiling will
  correctly gray ceilinged permissions there for free.

### F. Hierarchy store

- A plain (non-index) table via `SchemaBuilder.CreateTableAsync` +
  `AlterTableAsync(...).CreateIndex(...)` in a data migration — canonical
  model: RecordIndexingTaskMigrations. Columns: NodeId, UserId, ParentId,
  RootKind (staff|org), OrgId (null for staff), Path (materialized,
  prefix-indexed), plus ordering.
- Query/write through `IDbConnectionAccessor` with `ISqlDialect` quoting and
  `TablePrefix`/`Schema` composition — canonical model: IndexingTaskManager
  (own connection + own transaction, OUTSIDE the ambient ISession; dialect
  branching precedent for LIKE semantics: AuditTrail's migration).
- The service exposes subtree(user)/chain(user)/move/attach with the
  root-scope guard on EVERY call (root kind + org id are mandatory
  parameters); multi-org members get one node per binding.

### G. Member provisioning and external identity

- Portal registration wraps the stock flows: `UserService.RegisterAsync`
  fires `IRegistrationFormEvents` and `IUserEventHandler` (our CreatingAsync
  stamp applies); for external sign-ins, the portal's external-login start stamps the
  organization on the external cookie's properties and the same CreatingAsync stamp
  reads it back (`MemberPortalLoginContext`, below) — class + binding stamped by OUR
  portal flow, never by the raw callback. Verified: auto-provisioning without a form only
  happens when the tenant sets NoPassword+NoEmail+NoUsername; otherwise the
  RegisterExternalLogin form mediates — the portal supplies its own.
- Per-tenant providers (Google/GitHub/Microsoft/…) are stock feature modules
  configured via SITE SETTINGS (`GitHubAuthenticationSettings` pattern) —
  members get them wherever the tenant enables them, no new work.

### H. Class conversion action

- A dedicated endpoint + its own permission: rewrites the class POCO and
  resaves (index updates on save), ends org bindings, moves/retires the
  hierarchy node, and calls `UpdateSecurityStampAsync` — verified to be the
  mechanism that invalidates existing sessions, forcing re-login on the
  correct surface. Audit event when auditing exists.

### I. Impersonation (verified: NO stock impersonation exists — zero hits)

- Build on: `IUserService.CreatePrincipalAsync(member)` +
  `SignInManager.SignInAsync(member, properties)` with the STAFF identity in
  `AuthenticationProperties.Items` (server-side via CacheTicketStore);
  enrichment adds an `impersonator` claim so both identities ride the session
  (the dual-attribution prerequisite). Exit = re-issue the staff session from
  the stored identity. Recording lands on the AuditTrail seam when adopted:
  `IAuditTrailManager.RecordEventAsync` takes caller-supplied UserId/UserName
  (staff) with CorrelationId free for the member/org — the module exists in
  stock with exactly the right shape.

## What the module contains

`Crest.Members` (Server/Domain/member-wasm + tests):

- Class foundation: `CrestUserClass` aspect + `UserClassIndex` (+ backfill migration),
  `UserClassStampHandler` (CreatingAsync), `UserClassClaimsProvider`,
  `MemberLoginSurfaceGate` (ValidatingLoginAsync, redirect via UserOptions).
- Ceiling: `MemberPermissionCeilingOptions` (names + prefixes, baseline covers
  tenant machinery + dynamic variants) + `MemberPermissionCeilingHandler`
  (raw handler, context.Fail, no HasSucceeded guard) - unit-tested incl. the
  blanket-Succeed (SuperUserHandler) scenario.
- Hierarchy: `CrestUserHierarchy` table (adjacency + materialized path,
  root-guarded service `UserHierarchyService` over IDbConnectionAccessor with
  dialect quoting; `HierarchyPathMath` pure + unit-tested).
- Bindings and session: `CrestMemberInfo` bindings aspect + `MemberOrgBindingIndex` (row
  per binding), `MemberService` (first-member-of-org becomes member admin),
  `MemberSessionService` (active-org in AuthenticationProperties; enrichment
  claims stripped before any re-issue), `MemberCookieEventsConfiguration`
  (chained OnValidatePrincipal contributing active-org + template role claims
  per request).
- Provisioning and portal: member creation/binding endpoints + member role templates ensured
  by migration (deferred, idempotent). Portal surfaces:
  - Crest seam (party-agnostic): a `@page` component carrying `[AllowAnonymous]`
    yields `RouteComponentEntry.AllowsAnonymous`; `BlazorAdminThemeMiddleware`
    skips the login redirect and route authorization for it and `AdminRoutes`
    renders it shell-less. The JSON login flow lives in
    `CrestLoginService` (same ILoginFormEvent sequence, veto translation,
    caller-supplied AuthenticationProperties) so any surface can sign in on the
    ONE cookie (§C) without re-implementing the sequence.
  - `MemberPortalLoginContext` (scoped): the portal endpoints mark the request;
    an external-login callback carries the mark on the EXTERNAL cookie's
    properties (`MemberSessionKeys.PortalOrganization`, set by the portal's
    external-login start). `MemberLoginSurfaceGate` reads it: member refused on
    the tenant surface, staff refused on the portal, member without a binding to
    the named org refused. `UserClassStampHandler` reads it too: a user created
    by Orchard's own `RegisterAsync` (portal registration) or external-login
    auto-registration during a portal request is stamped member + org binding
    (+ deferred hierarchy node) via `MemberStampService`, shared with
    `MemberService.CreateMemberAsync`; everything else stays staff.
  - `api/crest/members/portal`: `login` (org optional; first binding becomes
    the active org), `register` (creates the Person, wraps `RegisterAsync` so
    the tenant's RegistrationSettings - moderation, email confirmation - apply;
    links person↔user; re-issues the cookie with the active org),
    `external-providers`, `external-login` (GET; the stock challenge with the
    org stamped on the external properties; Orchard's own callback finishes it).
  - Pages: the portal login, registration, member home and account pages live in the
    member shell (`Crest.MemberTheme`, routes in `MemberRoutePaths`, shell-relative
    under the tenant's member prefix); `Crest.Members`' `member-wasm` library supplies the
    shell's member seams (`MemberAuthenticationService`, `MemberShellContext`). See
    [shells-and-themes.md](shells-and-themes.md).
- Impersonation and conversion: `MemberImpersonationService` (member principal + staff identity in
  auth properties; enrichment surfaces the impersonator claim; stop restores
  staff) + `UserClassConversionService` (member→staff only, per the open
  question; ends bindings, removes org nodes, bumps the security stamp).
- API: `api/crest/members` - me, active-org, list-by-org, create, bindings
  add/remove, impersonate/start+stop, convert-to-staff.

Ordering rules the build found:

- Inline vocabulary seeding in Parties' CreateAsync aborted FIRST-TIME
  provisioning (early module, index tables not yet created) - it is deferred.
- The hierarchy store's raw write transaction deadlocked SQLite against the
  ambient session's uncommitted lock - hierarchy writes run as deferred
  tasks (post-commit), the same ordering rule as content seeding.

## Still to build

The member system as built — user classes, hierarchies, org bindings, the member portal's
login and session, the permission ceiling, impersonation and conversion — is described in
[docs/members.md](members.md). This plan holds what is not built yet.

### The three user classes (ruling 2026-09-16)

Naming, because "member" was carrying two unrelated meanings and the ambiguity is
what produced the duplicated-fact problem the binding rule below prevents:

| Class | Scope | Signs in to | Administered by |
| --- | --- | --- | --- |
| **platform-user** | supertenant | the Default tenant | the platform operator |
| **tenant-user** | one tenant | the tenant admin portal | tenant admins |
| **org-user** | one organization within one tenant | that org's member portal | that org's org admin (within the ceiling) |

All three are ordinary Orchard users in their tenant's own store — same shell,
same user-management surfaces, same roles/permissions machinery (the standing
ruling in [docs/members.md](members.md)). The class is a marker, not a separate identity system.

**Within one tenant there are exactly TWO portal user classes**: tenant-users
(the admin portal) and org-users (the customer, vendor, … portals). platform-user
is the supertenant class and belongs to the Default tenant, which is deferred by
ruling (see the super tenant item under Later), so it never appears in an ordinary
tenant's user store. Every statement in this document about "portal users" means
those two.

### One account, both sides (ruling 2026-10-05)

Supersedes "one class per user" and "conversion ends the org bindings" above. A user can
hold **staff and member at once**, under one account and one credential; which side they
sign in to decides what they see.

- [ ] **Class becomes a set — one index of connections** (ruling 2026-10-06). A user's
  connections are rows keyed by the composite **`(UserId, OrgId, Class)`**, unique, with no
  nulls: staff belong to the tenant's own organization, so staff is `(user, tenant org,
  staff)`, and acting for an organization as a member is `(user, org, member)`. Membership
  derives from the bindings (the record of truth); the index is a rebuildable projection of
  the user record. One index answers "is staff", "may act for org X", "members of org X" and
  the switcher's organization list. Being both staff and a member of the tenant's own
  organization needs the tenant setting (default: blocked). **No user exists outside an
  organization**: the tenant and its organizations exist before any user, so a member with
  no organization is a failure mode — registration places the member in an organization or
  refuses. The class property holds tenant-user, org-user or both. Conversion becomes adding or removing a class through
  one permission-gated action that replaces today's `convert-to-staff` endpoint (ruling
  2026-10-06), bumping the security stamp either way; adding staff no longer ends org bindings, and removing
  member does. The tenant's own organization still has no members
  ([parties.md](parties.md) › Organizations).
- [ ] **How a request carries its side and organization** (ruling 2026-10-06). Authorization
  handlers see only the principal, API routes are shared by both sides, and Orchard's own
  endpoints never pass through Crest's middleware — so the side is resolved where the
  sign-in cookie is validated, before any permission check:
  - **Pages** carry the side in the path (the admin prefix or the member prefix), and member
    pages carry the **organization in the path as a slug**: `/{member prefix}/{org slug}/…`.
    Bookmarks and links open the right organization, two tabs on two organizations are
    independent, and the switcher simply navigates to the other organization's URL.
  - **API calls** carry the shell in an `X-Shell` header (`admin` or `member`) and, on the
    member side, the organization in an `X-Org` header — both sent by each shell's
    HttpClient.
  - **A cookie-authenticated request without its `X-Shell` marker is malformed and denied**, never
    defaulted to a side. A member-side request whose organization the user holds no binding
    to is denied. A member with several organizations and none chosen goes to an
    organization picker.
  - From the side and organization the server builds that request's principal: on the
    staff side the user's tenant roles and no organization claims; on the member side only
    the binding's roles, with the class ceiling applied — the staff role and permission
    claims a dual user carries are stripped (after Orchard's security-stamp refresh, which
    copies claims), so the admin role cannot reach the portal. Side entry (holds staff; has a
    binding to this organization) is checked here on every request, not only at sign-in.
  - The active organization is no longer written into the cookie, so switching never
    re-signs it (no write race) and tabs never affect each other.
- [ ] **The side comes from the request, not the user.** Admin paths act as staff — the
  user's tenant roles, tenant-wide. Member-portal paths act as member — the active org
  binding's roles, the class permission ceiling, data scoped to that organization. One
  session (the one per-tenant cookie) serves both sides, even in two tabs at once.
- [ ] **The ceiling keys on the side.** The class permission ceiling fails a ceilinged
  permission on a member-side request, never on the user as such, so a dual user keeps
  their staff permissions in the admin. This is the security-critical change.
- [ ] **The login gate checks the side.** The admin login accepts a user holding staff; the
  member portal's login accepts a user with at least one org binding.
- [ ] **Tenants in the switcher are the accounts added on this device** (ruling 2026-10-06).
  Like Google or Zoho: the user signs in to one tenant, then "adds an account" by signing in
  to another, and can switch between them. The list of added accounts is kept **on the
  device** (client-side), not on the server, so a tenant the user has signed out of stays in
  the menu until removed, and no server data links accounts across tenants. The server
  never matches accounts across tenants — today's code, which lists a tenant when anyone
  there shares the username and starts every tenant's shell on each manifest load, is
  removed. **Organizations are server data:** only the **active tenant's** organizations are
  listed — all of the user's organizations there, from that tenant's bindings — not those of
  other signed-in tenants. **E-mail is never an identity link** between accounts or tenants:
  it can change, and a future e-mail system will need aliases. **Where the list works:**
  tenants addressed by path (the default) or by subdomains of one site share the device
  list; Orchard's own routing already serves tenants by host name, including several host
  names per tenant and wildcard subdomains (`RequestUrlHost`). A tenant on an unrelated
  custom domain cannot read another site's device list, so there Crest's switcher shows
  only what that site can see; switching across unrelated domains is downstream.

- [ ] **One switcher, the same on both sides.** The admin and the member portal show the
  same switcher, listing only what the user is registered for, in two sections: **tenants**
  on top, then a separator, then **organizations**. Choosing a tenant switches tenant (as
  the admin's tenant list does today, users found in each tenant's store); choosing an
  organization opens the member portal with that organization active.
- [ ] **Tenant SSO stays staff-side.** A dual user gets tenant SSO on the admin side only.
- [ ] **Audit records the side** each action came from ([audit.md](audit.md)).
- [ ] **Impersonation under one account** (ruling 2026-10-06). An impersonated session acts on
  the member side (staff claims stripped like any member-side request); a dual user can be
  impersonated; stopping restores the staff session exactly as it was, including its own
  active organization; the audit records both people.

### Relational hierarchy

- [ ] **Replace the single-parent tree with manager → report relationships** (rulings
  2026-10-05, 2026-10-06). A person can have several managers. Built **on YesSql, as a
  closure table** — no hand-written SQL, less maintenance:
  - Each person in a hierarchy has a small document per organization holding their direct
    managers and their computed ancestor list.
  - A YesSql map index emits one row per ancestor, `(AncestorId, DescendantId, OrgId)`: that
    index is the closure table, created and queried like every other Crest index.
  - "Everyone under me" (the subtree) and "my chain up" (every path, for escalation and
    approval routing) are index queries; adding an edge is refused when the report is
    already above the manager — one index lookup.
  - Changing an edge recomputes the ancestor lists of the report and everyone below them, all
    saved in one YesSql session; edge writes are serialized per organization so two writes
    cannot together create a cycle. A rebuild from the edges doubles as repair.
  - The root guard holds for reports-to: every query and write carries the organization.
    Replaces `UserHierarchyService`'s plain-SQL tree and its path math.
- [ ] **Association relationship types** (ruling 2026-10-05). Typed connections beside
  reports-to (sales rep → contact, account rep → organization, …) that may cross staff and
  an organization. They grant no visibility by default; what, if anything, a type grants is
  decided per type. **Both modules and tenants declare types:** module types are built in,
  code relies on them, so a tenant cannot edit or remove them; tenant types are an editable
  list on top.

  **Associations are content items** (ruling 2026-10-06), kept apart from the hierarchy so
  the root guard and the closure table never see them: an Association content type with two
  ends (each a person and the organization they act in) and a type picker. Editing,
  versioning, permissions and audit come from the content system. A map index on the two
  ends and the type answers "my connections" on the hot path. A type may register a
  visibility resolver (default: grants nothing); the reading API enforces that a rep sees
  only their own connections and the organization's people do not see the rep side unless
  the type allows it.
### Org bindings and org structure

- [ ] **One home per fact: the org binding record, not a second declaration.** The rule, the same one-home-per-fact rule the field model follows: **the record is
  the seam of record; the binding is a derived read-model.** A picker is how an
  association is CHOSEN in the UI; the record IS the association.
  - Org membership is declared once, in the org-user's binding record (`Crest.Members`),
    carrying roles and member-admin standing.
  - The flattened projection on the user object stays — it is what the authorization
    hot path reads — but it is written ONLY by that record's write path and is
    rebuildable from the records. `AddBindingAsync`/`RemoveBindingAsync` stop being
    public API; they become internal projection writers. A rebuild command ships with
    the work, both as drift repair and as the migration that populates existing data.
- [ ] **Layering: identity in the user layer, structure in content (ruling 2026-09-16).**
  Three options were considered for where org access data lives.
  - **A — fully in the user layer.** Class, bindings and roles as claims/properties on
    `User`. Fastest auth, nothing extra to query. But org structure is not content, so
    it is not editable through the content UI, not versioned, and gets no content
    permissions — managing thousands of users means hand-building every admin surface.
  - **B — fully in content.** Org membership and groups as content items; auth queries
    them. Everything editable/versioned/permissioned for free, but the content store
    is hit on every request, or cached — and the cache is the derived projection again.
  - **C — split by what each layer is good at. CHOSEN.** Identity and the
    authorization hot path live in the user layer (class, active org, effective
    roles); the DECLARATIVE org structure — org settings (groups and group→role
    mappings were part of this until 2026-10-05; groups are dropped, see Decisions needed ›
    Grouping) — lives in content. Login resolves the effective roles once and projects the
    result onto the user/claims.

  C is the same middle path as the binding ruling, applied one seam over: the
  declarative thing is content (editable, listable, permissioned, importable — which
  is what makes thousands of users tractable), and the hot path reads a flat
  projection that is rebuildable from it.
- [ ] **The tenant's own organization has no members by default** (rulings 2026-10-05,
  2026-10-06): a tenant setting, default **block**, decides whether bindings may point at the
  Organization that represents the tenant ([parties.md](parties.md) › Organizations), so by
  default staff and member logins never share an organization. **Enforced through one
  source of truth** (ruling 2026-10-06): Parties exposes "is this the tenant's organization",
  read from the site-settings reference, and every binding write — the binding API, the
  member stamp path, the portal register endpoint — goes through one Members service that
  checks it with the tenant setting. No flag is stored on the organization; lists that
  show "the tenant's organization" ask the same Parties check.

### Data separation by organization

**Data separation**: a member's queries are scoped to records that reference
or are assigned to their organization — the organization-scope axis,
generalizing the option-source scope work (fail-closed: no org link means
NO records, never all records). Hierarchy expansion applies within the org
subtree.

- [ ] **Hierarchy-aware scope expansion feeding the existing fail-closed scope
  machinery.** **Hierarchy expansion is deliberately not wired yet**: hierarchy subtree expansion into the existing
  6d/6e scope machinery - `OptionSourceScope` is CREST code and party-blind, so
  feeding it subtree data needs a Crest-side expander seam, which per the
  Ownership ruling is a separate, explicitly justified decision before any code.
- [ ] **The organization-scope axis on the content surfaces portals query.**

### Admin surfaces

- [ ] **Admin list class filter.** Admin list: `IUsersAdminListFilterProvider` adds a named search term
  (`class:member`) — copy `RolesAdminListFilterProvider` (shows `.AlwaysRun()`
  for FORCED scoping, useful later for portal-side lists); the dropdown is a
  display driver on `UserIndexOptions` (stock puts the role dropdown at
  Thumbnail/Content:40; ours goes at Content:60). The stock `UsersFilter` enum
  is closed — the named-term route is the correct one, not an enum fork.
- [ ] **User editor class section.** The user editor gets a read-only class section via
  `SectionDisplayDriver<User, T>` (precedent: Demo's UserProfileDisplayDriver);
  conversion is NOT edited there — it is its own action.
- [ ] **Permission editor** (ruling 2026-10-05: planned). Role permissions edited in the
  Crest admin over Orchard's `IPermissionProvider`s (`Roles.razor` still points at it:
  "Role permissions will be added with the dedicated permission editor"). It also shows,
  per module, what each user can effectively do — the union of their roles, since a user
  holds several. The admin role shows as "everything" (Orchard grants it without permission
  claims), and a dual user gets two summaries, staff side and member side with the ceiling
  applied (ruling 2026-10-06).
- [ ] **Person creation for external-login auto-registered members**
  (they get class + binding only).

### Later

- [ ] **Tenant SSO wiring (Default-as-IdP).** **Tenant SSO** (Default tenant as OpenID Connect IdP — the stock OpenId
  Server/Client features) is for TENANT USERS across a person's multiple
  tenants; members are excluded by ruling. A member of two tenants
  simply has separate member accounts; if that person is ALSO
  staff somewhere, that staff account is a separate identity too.
- [ ] **The super tenant (deferred).** **Deferred by ruling (2026-09-08): the super tenant.** Cross-tenant read access
  for the Default tenant's users is a later design. Recorded so it is not lost:
  the sanctioned mechanism is `IShellHost.GetScopeAsync` into the target tenant's
  shell (never shared-DB queries), behind an opt-in per-type contract registry,
  fail-closed, read-only first, audited. Nothing above depends on it.

### Decisions needed

- [ ] **Grouping — not final; a starting point for next time** (2026-10-05). For now Crest
  has **no group concept**: roles stay exactly as they are, and the existing system keeps
  working with no Orchard change. Where the discussion landed, to pick up next time:
  **groups grant access (what you can see, read) and roles grant actions (create, update,
  delete and the other verbs)**, every action requiring that you can see the record first.
  Open with it: capability screens (settings, audit) as actions; answering the platform's view
  permissions from the access system (now changeable in place, since the hard fork); business roles as derived
  groups. Until then, sharing uses the **no-permission role shim** ([media.md](media.md) ›
  Terms).

- [x] **Decide conversion semantics.** Ruled 2026-10-05: both directions, as adding or
  removing a class (One account, both sides).
- [ ] **Decide whether the ceiling registry also ceilings staff-class-only permissions in
  reverse.** Whether the ceiling registry also ceilings STAFF-class-only permissions in
  reverse (probably unnecessary — staff are trusted with member-portal
  surfaces via impersonation).
