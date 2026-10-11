# User stories: who may do what

The reference for **writing access tests** and **auditing code**. Each access type below is a
kind of caller the access machinery builds ([access.md](access.md)): who it is, which shell
side it acts on, whether it needs **anonymous** allowance and whether it is **privileged**,
what it does (stories), what it must never do (refusals), and the data objects it touches
with the privilege each takes.

How to use it:

- **Tests.** Every row of an object table and every refusal is a case for the conformance
  suite (`test/Crest.Tests/Access/Conformance/AccessWorld.cs`): the caller, the permission,
  the resource and the expected verdict. A refusal is as much a test as a grant.
- **Audits.** For a code path, find the stories that reach it and check it asks the one
  decision (`IAccessDecision`, or `IAuthorizationService` routed to it) with the permission
  and resource named here, and that reads are filtered by the caller's scope. A path that
  serves an object listed here without that check is a defect.
- **Changes.** A new module adds its objects to the types that touch them; a new access type
  adds a section. Keep permission names as the code spells them.

## Two systems: pages, and data and workflows

A caller's access is decided by **two separate systems**, and every access type below lists
both:

- **Pages** - which shell pages a caller may open (site, admin, member, the login shell). The
  request path decides it: the shell selector picks the side, the gate admits the request
  (anonymous or not), and the **route permission table** (`ICrestRoutePermissionProvider` →
  `CrestRouteAuthorizationService` for Blazor routes, `AdminFilter`/`AccessAdminPanel` for the
  remaining MVC admin) names the permission each page takes. It also decides which menu
  entries show. A page grants **no data**: it is a frame.
- **Data and workflows** - which objects a caller may read, change or run, on any surface (the
  page's API calls, GraphQL, queries, Liquid, workflow activities, exports). The one decision
  (`IAccessDecision`) with the resource, the caller's scope (`ScopeSet`) for every read, and
  the workflow gate for runs. It does not care which page asked.

Rules that follow, for tests and audits:

- Test each system on its own. "May open the Users page" and "may list users in role X" are
  different cases; a page check never stands in for a data check.
- A page a caller may open shows only the data the data system allows: an editor's content
  list page lists only the editor's types; an empty or partial page is correct, a 403 on the
  page is not required.
- An API endpoint is data, not a page: it asks the data system, never the route table.
- Anonymous allowance is separate too: a page may admit anonymous callers (login, error,
  public pages) while every data object on it still takes its own check.

Terms:

- **Anonymous**: the type needs requests or runs with no signed-in user to be admitted.
- **Privileged**: the type holds rights over other people's data, security settings or the
  tenant itself (security-critical permissions, `IsSecurityCritical = true`, are marked ⚠).
- **Own / others'**: the item's `Owner` is / is not the caller. The resource mappers turn a
  base permission into its per-type (`EditContent_{type}`) and own (`EditOwnContent`) forms.
- **Scope**: the rows a caller may read at all (`ScopeSet`); a permission without scope is
  never enough for a list.
- **Side**: site, admin, member or system ([access.md](access.md) § Shells). The shell prefix
  decides it; `X-Shell` may only confirm.

---

## 1. Anonymous visitor

Site side, no identity. **Anonymous: yes. Privileged: no.** Rights are the `Anonymous` role's.

Stories:

- As a visitor I read published pages, posts and other public content types.
- As a visitor I see public media (images, documents) on those pages.
- As a visitor I submit a public form (contact, enquiry), which may start a workflow.
- As a visitor I see the error pages (404, 403, 500) of every shell, styled by that shell.
- As a visitor I search the site's public index.

Refusals:

- Never sees drafts, unpublished versions, or types the Anonymous role cannot view.
- Never reaches an admin or member page other than the auth and error pages (§ 2); an admin
  or member URL redirects to login.
- Never calls a backend API anonymously, except the public content and media endpoints for
  items marked public.
- Never starts a workflow except through a page-URL HTTP endpoint whose definition accepts
  anonymous runs; that run cannot write content or users unless published as system.
- Never reads users, roles, settings, audit, workflows, queries other than public ones.

**Pages:**

| Shell | Pages | Anonymous | Route permission |
| --- | --- | --- | --- |
| Site | public pages, content item pages of public types, search results | yes | none (the item itself is data) |
| Site, admin, member, login | error pages (404, 403, 500) | yes | none |
| Site, admin, member, login | login, sign-up, password reset (§ 2) | yes | none |
| Admin, member | anything else | no | redirect to login |

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Content item (public type, published) | view | `ViewContent` (per type: `ViewContent_{type}`) | published only |
| Content item (draft, other types) | none | - | refused, "not found" on reads |
| Media (public folder) | view | none (`PermissionCandidate.Granted`) | outside secured folders |
| Media (secured folder, attached to public item) | view | the item's `ViewContent` | re-asked for the item |
| Saved query (public) | execute | `ExecuteApi_{query}` | rows filtered by scope |
| Search index | query | `QuerySearchIndex` / per-index | content hits through the scoped index; raw documents refused |
| Form submission | create (via workflow) | none on the visitor; the flow's own identity | flow accepts anonymous runs |
| Pages: login, sign-up, password reset, error | view | page permission granted to Anonymous | every shell |

---

## 2. Visitor signing in or signing up

Any shell's auth pages, no identity yet. **Anonymous: yes. Privileged: no.**

Stories:

- As a visitor I open the login, sign-up, forgotten-password and reset-password pages of the
  site, admin and member shells, and the external-login callbacks.
- As a visitor I register an account (when the tenant allows registration); the registration
  workflow runs as the **new user** the event names, never as anonymous.
- As a visitor I confirm my e-mail through the emailed link.

Refusals:

- Registration never assigns a role beyond the tenant's registration default; a flow that
  needs more (a member profile, an organization binding) is published as system.
- Sign-up never lands the new user on a side their class may not use (a member on admin).
- The login trigger never runs as anonymous: its actor is the user who just signed in.

**Pages:**

| Shell | Pages | Anonymous | Route permission |
| --- | --- | --- | --- |
| Login shell (`LoginPath`) and each shell's auth routes | login, sign-up, forgotten and reset password, e-mail confirmation, external-login callback, two-factor challenge | yes | none |
| Site, admin, member | error pages | yes | none |

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Own new user account | create | none (registration setting) | via `IUserService` |
| Own e-mail confirmation / reset token | use | none | token-bound |
| Workflow runs from `user.registered` / `user.loggedin` | run | as the user named | sealed actor |

---

## 3. Signed-in site user

Site side, a user with the `Authenticated` role and perhaps site roles; staff or member class.
**Anonymous: no. Privileged: no.**

Stories:

- As a user I read content and media my roles allow, beyond what anonymous sees.
- As a user I edit my own profile and change my password and two-factor settings.
- As a user I see and mark my notifications.
- As a user I submit content my roles allow on the site (a comment, a listing) and manage it
  later as its owner.

Refusals:

- Never edits someone else's profile, content or media.
- A member-class user acting on the site side holds no tenant rights and no organization rows
  (defect 5); their member rights apply on the member side only.
- Never reads another user's notifications (`Notifications` filtered by user id).

**Pages:**

| Shell | Pages | Anonymous | Route permission |
| --- | --- | --- | --- |
| Site | everything a visitor sees, plus pages for signed-in users | no | `Authenticated` |
| Site | own account, profile, password, two-factor, notifications | no | `ManageOwnUserInformation`, `ManageOwnUserProfile` |
| Admin | none | no | `AccessAdminPanel` not held |

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Own user account | edit | `ManageOwnUserInformation` | own only |
| Own profile | edit | `ManageOwnUserProfile` | own only |
| Own notifications | view, mark read | none (filtered by user id) | own only |
| Content item, own | create, edit, delete, publish | `EditOwnContent`, `DeleteOwnContent`, `PublishOwnContent` (per type) | owner = caller |
| Content item, others' | view | `ViewContent` per type | published unless preview/edit held |
| Media, own folder | view, upload | `ViewOwnMediaContent`, `ManageOwnMediaContent` | user's asset folder |

---

## 4. Member

Member side (`/members`), member class, acting in **one organization** (the `X-Org` header or
the session's active organization, validated against the user's bindings). Rights come only
from that binding's role templates. **Anonymous: no. Privileged: no** (within the org).

Stories:

- As a member I switch between the organizations I am bound to; each switch changes what I
  see.
- As a member I see my organization's data: its parties, documents, lists, records.
- As a member I create and edit the records my binding's role allows, owned by me or my org.
- As a member I see the organization's members I am allowed to (the hierarchy below me).
- As a member I run the member-facing workflows of my organization.

Refusals:

- Never sees another organization's rows, on any surface (lists, pickers, queries, search,
  GraphQL, exports) - the organization scope applies everywhere.
- Never acts on the admin side (refused at the gate); on the site side holds no org rows.
- Never holds a ceilinged permission, whatever a role grants (the member class ceiling,
  a final denial). **Gap (2026-10-11): the ceiling is registered but configured with no
  permissions, so today it denies nothing.** The list to ceiling (at least everything ⚠ in
  §§ 10-13, `ManageTenants`, `ManageFeatures`, `ManageRoles`, `ManageSettings`) is a ruling.
- A staff grant the same user holds as staff never reaches the member side (dual users).
- A revoked binding stops working on the next request (the version bumps on binding writes).

**Pages:**

| Shell | Pages | Anonymous | Route permission |
| --- | --- | --- | --- |
| Member (`/members`) | portal home, organization switcher, the organization's lists and records, own account | no | the binding role's route grants; organization chosen by `X-Org` or the session |
| Admin | none | no | refused at the gate (member class) |
| Site | public pages as a signed-in user, no org pages | no | as § 3 |

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Organization's content items | view, list | `ViewContent`, `ListContent` per type | organization scope |
| Own records in the org | create, edit, delete | `EditOwnContent`, `DeleteOwnContent` per type | owner + organization |
| Organization's parties | view | `ViewContent` on the party types | organization scope |
| Organization's members (below me in the hierarchy) | view | binding role's member rights | hierarchy root guard |
| Global lists / reference data | view | per list | read-only |
| Member workflows | run | `RunCrestWorkflows` + definition Run list | as the member; org scope inside |

---

## 5. Member admin (organization administrator)

Member side, member class, holding the organization's `MemberAdmin` role template (defaults
to the organization's first member). **Anonymous: no. Privileged: yes, within one
organization only.**

Stories:

- As a member admin I invite, add and remove members of my organization and set their role
  template.
- As a member admin I manage sub-users in my organization's hierarchy.
- As a member admin I edit my organization's details and its member-facing settings
  (payment types, schedules, wallet connections, as modules add them).
- As a member admin I see my organization's activity.

Refusals:

- Never touches another organization, tenant features, tenant settings, roles, or users
  outside the organization (the hierarchy bounds the who, the role template the what).
- Never grants a role template the tenant has not made assignable to member admins.
- Never holds a ceilinged permission (see § 4 gap).

**Pages:**

| Shell | Pages | Anonymous | Route permission |
| --- | --- | --- | --- |
| Member | everything a member sees, plus members, invitations, sub-users, organization settings | no | the member-admin template's route grants |
| Admin | none | no | refused at the gate |

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Organization's members and bindings | create, edit, remove | the template's member-admin rights | own organization; root guard |
| Organization record (party) | edit | `EditContent` on the organization type | own organization |
| Organization settings | edit | per module | own organization |
| Organization's users' credentials | reset | member-admin right | own organization |

---

## 6. Staff impersonating a member

Member side, a staff user acting as a member, recorded as the impersonator.
**Anonymous: no. Privileged: yes.**

Stories:

- As support staff I open a member's portal as that member to reproduce what they see.

Refusals:

- Rights are the member's, never the staff user's; the impersonator is recorded on every
  audit event.
- Cannot impersonate a staff user or a super user; cannot start impersonating without
  antiforgery (defect 10).

**Pages:**

| Shell | Pages | Anonymous | Route permission |
| --- | --- | --- | --- |
| Admin | member list with the impersonate action | no | `ManageMembers` + `ImpersonateMembers` |
| Member | the member's portal, with an impersonation banner | no | the member's |

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| A member's portal session | start, stop | `ImpersonateMembers` | members only |
| Everything inside | as the member | the member's | the member's organization |

---

## 7. Content contributor (author)

Admin side, staff, a role like `Author` or `Contributor`. **Anonymous: no. Privileged: no.**

Stories:

- As an author I create drafts of the types I am allowed, and edit, preview and delete my own.
- As an author I publish my own items when my role allows, or send them for approval.
- As an author I upload media to my own folder and attach it.
- As an author I clone my own items and localize them.

Refusals:

- Never edits, publishes or deletes others' items; never sees others' drafts.
- Never changes an item's owner.
- Never sees types outside my role in lists or pickers.

**Pages:**

| Shell | Pages | Anonymous | Route permission |
| --- | --- | --- | --- |
| Admin | dashboard, content list, editor for allowed types, media library (own folder) | no | `AccessAdminPanel`, `ListContent`, `ManageOwnMediaContent` |
| Admin | content types, users, roles, settings | no | not held: menu entries hidden, routes refused |

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Content item, own | create, edit, preview, delete, publish, clone, localize | `EditOwnContent`, `PreviewOwnContent`, `DeleteOwnContent`, `PublishOwnContent`, `CloneOwnContent`, `LocalizeOwnContent` (per type) | owner = caller |
| Content item, others' | view published | `ViewContent` per type | published only |
| Content list | list | `ListContent` per type | own types; drafts own only |
| Media, own folder | manage | `ManageOwnMediaContent` | own folder |
| Approval request | create | per approval flow | own items |

---

## 8. Editor / publisher

Admin side, staff, a role like `Editor`. **Anonymous: no. Privileged: moderately** (others'
content).

Stories:

- As an editor I edit, preview, publish, unpublish and delete anyone's items of my types.
- As an editor I approve or reject authors' submissions.
- As an editor I manage menus, taxonomies and the media library.
- As an editor I localize content and manage translations.

Refusals:

- Never changes content types, roles, users or settings.
- Never edits a type outside the role's per-type grants.

**Pages:**

| Shell | Pages | Anonymous | Route permission |
| --- | --- | --- | --- |
| Admin | dashboard, content list and editor, menus, taxonomies, media library, translations, approvals | no | `AccessAdminPanel`, `ListContent`, `ManageMenu`, `ManageTaxonomy`, `ManageMediaContent`, `ManageTranslations` |

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Content item, any | view, edit, preview, publish, delete, clone, localize | `EditContent`, `PreviewContent`, `PublishContent`, `DeleteContent`, `CloneContent`, `LocalizeContent` (per type) | per type |
| Content owner | change | `EditContentOwner` | per type |
| Menus, taxonomies | manage | `ManageMenu`, `ManageTaxonomy` | |
| Media, any folder | manage | `ManageMediaContent`, `ManageOthersMediaContent`, `ManageMediaFolder` | secure media: also view the folder |
| Approvals | decide | the approval's role or permission | the task's |
| Translations | manage | `ManageTranslations`, `ManageLocalization` | |

---

## 9. Workflow author, runner and viewer

Admin side, staff, workflow roles (`WorkflowEditor`, `WorkflowViewer`). **Anonymous: no.
Privileged: authors yes** (a flow acts with its actor's rights and can be published as
system only with ⚠ `ManageShippedCrestWorkflows`).

Stories:

- As a viewer I see definitions, instances, journals and the activity palette.
- As an author I design, save and test flows, attach hooks and use connections.
- As a publisher I publish and retract flows.
- As a runner I start a flow by hand, resume a waiting one, cancel or delete an instance.
- As an owner of a shipped flow I edit it, reset it or publish it as system.

Refusals:

- Every check is also narrowed by the definition's Edit and Run lists.
- A run acts as its sealed, instance-bound actor; no input, import or resume can change it.
- A system flow is maintained by code: no user edits or deletes it; a shipped flow cannot be
  deleted.
- A data activity in a run asks the decision for the run's caller against the item, role,
  user or tenant it touches.

**Pages:**

| Shell | Pages | Anonymous | Route permission |
| --- | --- | --- | --- |
| Admin | workflow definitions list, designer, instances, journals | no | `ViewCrestWorkflows` (the designer is read-only without `EditCrestWorkflows`) |
| Admin | connections | no | `ManageCrestWorkflowConnections` |
| Admin | approvals inbox | no | the approval's role or permission |
| Site / any | page-URL HTTP endpoints of flows | per definition | public only when the definition accepts anonymous runs |

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Workflow definition | view | `ViewCrestWorkflows` | |
| Workflow definition | create, edit, delete, test activities | `EditCrestWorkflows` | Edit list |
| Workflow definition | publish, retract | `PublishCrestWorkflows` | Edit list |
| Shipped / system flag | edit shipped, publish as system | ⚠ `ManageShippedCrestWorkflows` | |
| Workflow run / instance | start, resume, cancel, delete, import | `RunCrestWorkflows` | Run list of the definition |
| Workflow instance | view (journal, variables, export) | `ViewCrestWorkflows` | **gap: no per-definition narrowing** |
| Connections | manage | `ManageCrestWorkflowConnections` | |
| Connections | use in a flow | `UseCrestWorkflowConnections` | gate on the activity |
| Queries in flows | run | `ExecuteApi_{query}` (descriptor) | scope of the run's caller |

---

## 10. User administrator

Admin side, staff. **Anonymous: no. Privileged: yes.**

Stories:

- As a user admin I list, create, edit, disable and delete users in the roles I may manage.
- As a user admin I assign and remove roles I am allowed to assign.
- As a user admin I convert a user between staff and member.
- As a user admin I reset two-factor for a locked-out user.

Refusals:

- Per-role variations bound everything: managing users in role X takes the X variation
  (`ListUsersInRole_X`, `EditUsersInRole_X`, `AssignRoleToUsers_X`, …).
- Never assigns the Administrator role without that role's assign right.
- Disabling a user removes their rights on the next request and in their running flows.

**Pages:**

| Shell | Pages | Anonymous | Route permission |
| --- | --- | --- | --- |
| Admin | users list, user editor, roles list (read) | no | `ListUsers`, `ViewRoles` |
| Admin | user class conversion | no | `ConvertUserClass` |

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Users | list | `ListUsers` (per role) | |
| Users | view profiles (email, phone, properties) | `View Users` | pickers show names only without it |
| Users | edit | ⚠ `EditUsers` (per role) | |
| Users | delete | ⚠ `DeleteUsers` (per role) | |
| Users | everything, security settings | ⚠ `ManageUsers` | |
| Role assignment | assign, remove | ⚠ `AssignRoleToUsers` (per role) | |
| Two-factor | disable for a user | ⚠ `DisableTwoFactorAuthenticationForUsers` | |
| User class | convert staff ↔ member | `ConvertUserClass` | |
| Roles | view | `ViewRoles` | |

---

## 11. Site administrator

Admin side, the `Administrator` role: the **super user** of the tenant (every permission,
under the ceilings). **Anonymous: no. Privileged: yes.**

Stories:

- As an admin I manage roles and their permissions, features, settings, content types,
  themes, templates, layers, placements, queries, search indexes, sitemaps, deployment, the
  admin menu and dashboard, OpenId applications and scopes, e-mail and SMS providers.
- As an admin I see the audit trail.
- As an admin I import and export recipes and deployment packages.

Refusals:

- Still under every ceiling: cannot edit a locked item, cannot edit a system flow, cannot
  bypass a definition's own access lists where they apply as ceilings.
- Cannot act in another tenant.

**Pages:**

| Shell | Pages | Anonymous | Route permission |
| --- | --- | --- | --- |
| Admin | every admin page of the tenant (settings, features, roles, content types, design, search, deployment, OpenId, audit) | no | super user |
| Member | none by default | no | an admin is staff; the member side takes a binding |

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Roles and permissions | manage | ⚠ `ManageRoles` | bumps the permission version |
| Features | enable, disable | ⚠ `ManageFeatures` | |
| Site settings | manage | `ManageSettings`, `ManageGeneralSettings`, and per group | |
| Content types | edit | `EditContentTypes`, `ViewContentTypes` | |
| Themes, templates, layers, placements | manage | `ApplyTheme`, `ManageTemplates`, `ManageAdminTemplates`, `ManageLayers`, `ManagePlacements` | |
| Queries | manage | `ManageQueries`, ⚠ `ManageSqlQueries` | |
| Indexes | manage | `ManageIndexes`, `ManageLuceneIndexes`, `ManageElasticIndexes` | |
| Deployment | plans, export, import | `ManageDeploymentPlan`, `Export`, ⚠ `Import` | |
| Remote deployment | instances, clients | `ManageRemoteInstances`, `ManageRemoteClients`, `ExportRemoteInstances` | |
| OpenId | applications, scopes, server, validation | ⚠ `ManageApplications`, `ManageScopes`, `ManageServerSettings`, `ManageValidationSettings` | |
| Audit trail | view | audit permissions | denials always recorded |
| Background tasks | manage | `ManageBackgroundTasks` | |
| Security settings | CORS, HTTPS, headers, rate limits, reverse proxy | `ManageCorsSettings`, `ManageHttps`, `ManageSecurityHeadersSettings`, `ManageRateLimits`, `ManageReverseProxySettings` | |

---

## 12. Site owner and host administrator

The site's `SuperUser` (site settings), and on the **Default tenant** the host administrator.
**Anonymous: no. Privileged: highest.**

Stories:

- As the site owner I hold every permission in my tenant, including handing ownership over.
- As the host admin (Default tenant) I create, set up, enable and disable tenants and manage
  feature profiles.
- As the host admin I manage global reference data (geo, currencies, global lists) in the one
  host-level store.

Refusals:

- Tenants are managed from the Default tenant only.
- Handing over the super user bumps the version: the previous owner loses it on the next
  request.

**Pages:**

| Shell | Pages | Anonymous | Route permission |
| --- | --- | --- | --- |
| Admin (Default tenant) | tenants, tenant feature profiles, global reference data | no | `ManageTenants`, `ManageTenantFeatureProfiles`, `ManageCrestGlobalReferenceData` |
| Admin (any tenant) | everything | no | super user |

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Tenants | create, set up, enable, disable | ⚠ `ManageTenants` | Default tenant only |
| Tenant feature profiles | manage | `ManageTenantFeatureProfiles` | Default tenant only |
| Global reference data | manage | `ManageCrestGlobalReferenceData` | Default tenant only; the one store outside a tenant |
| Site owner | transfer | `SiteOwner` | version bump |

---

## 13. Application callers (machines)

No user record; `CallerClasses.Application`; authenticated through the `Api` scheme.
**Anonymous: no. Privileged: as assigned.** Rights come from roles assigned to the
application, never from claims a token carries ([machine-actors.md](machine-actors.md)).

Three kinds:

- **API client** (OpenId client credentials): an integration calling the REST or GraphQL
  APIs with a bearer token.
- **Remote deployment client**: another instance pushing a deployment package
  (`Authorization: RemoteDeployment {client}:{key}`).
- **Webhook connection identity** (ruled 2026-10-11): an external system calling a
  connection's webhook; its signature authenticates it and the run acts as the connection's
  application caller with the roles assigned on the connection.

Stories:

- As an integration I read and write the content types my application's roles allow.
- As a remote instance I import a package into this one.
- As Stripe (a billing connection) I report a paid invoice and the flow marks the order paid.

Refusals:

- Never a super user unless its roles say so; never a member; never the system caller.
- A credential for one tenant never works in another.
- A deleted or disabled user's cookie is never read as an application.
- Bearer calls are not blocked by antiforgery (machine-actors.md, open).

**Pages:**

Machines open **no pages**: every request is an API call, which is data. The route table
never admits an application; a bearer request to a page URL is refused.

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Content items (assigned types) | view, edit, publish | per the application's roles | per type |
| Remote import | import | ⚠ `ImportRemoteInstances` | remote client only |
| Workflow run (webhook) | run | as the connection's caller | sealed actor |
| GraphQL | execute | `ExecuteGraphQL`, `ExecuteGraphQLMutations` | |
| Content API | access | `AccessContentApi`, `ApiViewContent` | |

---

## 14. System actors

Built in process only; no credential produces them. **Anonymous: no. Privileged: yes.**

- **Tenant system actor**: acts as a tenant admin (super user). Used by background tasks,
  recipe steps with no caller (setup, startup recipes), definitions published as system,
  timers and cron runs of those definitions.
- **Organization system actor**: acts as one organization's admin, with its member-admin roles.

Stories:

- As the system I publish scheduled content (publish later, archive later).
- As the system I run the setup recipe and shipped flows.
- As a system flow I create a member profile when a user registers.

Refusals:

- Never inherited by a flow that is not published as system (a flow triggered by a
  background task does not become the super user).
- Never captured as a workflow actor; never produced from a workflow input.
- Still under the ceilings.

**Pages:**

System actors open **no pages**: they exist only in process (background tasks, recipe
steps, system flows).

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Everything in the tenant | all | super user | ceilings apply |
| One organization's data | as its admin | the org's member-admin roles | organization scope |

---

## 15. Workflow run actor

Not a person: the identity a run acts as, sealed and bound to its instance
([workflows.md](workflows.md) § Running identity). It is whoever started the run (a user, an
application, the new user for registration), or the system actor for system definitions.

Stories:

- As a run started by an editor publishing a post, I create the related items with the
  editor's rights.
- As a run resumed by an approver, I continue as the editor who started me, not the approver.
- As a run whose starter lost a right between bursts, my next burst fails.

Refusals:

- No input, import, resume or author expression changes the actor.
- An anonymous actor only for page-URL HTTP endpoints whose definition accepts anonymous runs.
- Every data activity asks the decision for this caller against the resource it touches.

**Pages:**

A run opens **no pages**. A page-URL HTTP endpoint is a page for its *caller* (§ 1, § 9);
the run then acts as that caller's sealed actor.

**Data and workflows:**

| Object | Privilege | Permission | Scope / notes |
| --- | --- | --- | --- |
| Whatever the activities touch | as the actor | the actor's current rights | rebuilt per burst |

---

## 16. Shared-content recipient

A person who reaches a document, media file or folder because **another user shared it**,
with the settings on that object deciding what they may do ([sharing.md](sharing.md);
processed by the access gate through the sharing module's registered mapper and scope). Any
module's items can be shareable: files, content items, and later parties and records. The recipient may be named as a user, or reached through a role,
their organization, the tenant, or Anyone (a public link). **Anonymous: only for an Anyone
grant (view).** **Privileged: no**, except a Drive Admin within their drive.

Share settings on an object: the principal, the **access level** (Viewer, Commenter, Editor,
Manager; Drive Admin on a drive only), optional **start and end**, and **blocks** that carve
exceptions out of inherited grants. Settings flow down the folder tree; rules can grant or
block by file type, metadata or time.

Stories:

- As a recipient I open a document shared with me through its link, even when I cannot open
  the folder it sits in.
- As a recipient I see a "Shared with me" list of everything shared with me, my roles, my
  organization or the tenant.
- As a Viewer I read the published version and download it.
- As a Commenter I also add comments, nothing more.
- As an Editor I create and update files in a shared folder.
- As a Manager I also delete and move what I manage.
- As a Drive Admin I change the sharing and rules of my drive and see why each person has
  access.
- As a visitor with a public link I view a file shared with Anyone.
- As a member I see what is shared with my organization only while I act for it.

Refusals:

- An object I cannot view does not exist for me: in no listing, search, picker, count,
  explanation or error.
- A share that has ended stops working on time, even between recalculations.
- A block on me wins over my role's grant at the same depth (principal tier first, then
  depth); a public file stays viewable but a block still stops comment and edit.
- Anyone can only ever be a Viewer; a public link never edits.
- I never see how something is shared unless I am its drive's Drive Admin or a tenant admin.
- Deleting or moving a folder needs Manager on everything below it, including what I cannot
  see; the refusal never names or counts those items.
- Drafts are for Editors and Managers; a Viewer gets the published version only.
- An inactive drive is unreachable, whatever the grant.
- With the sharing feature off, none of this applies and the media permissions decide.

**Pages:**

| Shell | Pages | Anonymous | Route permission |
| --- | --- | --- | --- |
| Site | a public file's link | yes | none (Anyone: Viewer on the object) |
| Site, member, admin | a shared object's page (viewer, comments, editor by level) | no | none on the route; the object's access level decides |
| Site, member, admin | "Shared with me", drives and folders I can reach | no | `Authenticated` |
| Any | the Share panel | no | Drive Admin of the object's drive, or tenant admin |

**Data and workflows:**

| Object | Privilege | Access level / permission | Scope / notes |
| --- | --- | --- | --- |
| File, folder or document shared with me | view, download | Viewer | published version; via link even without the parent |
| Same | comment | Commenter | |
| Same | create, update | Editor | drafts visible |
| Same | delete, move | Manager | over everything affected, seen or not |
| Drive's sharing and rules | view explanation, change | Drive Admin (drive only) | tenant-level rules: a tenant permission |
| Public object | view | Anyone: Viewer | anonymous; bytes cacheable |
| "Shared with me" listing | list | any grant on the object | filtered in the query by the access index |
| Content item in a shared folder | as the folder's chain | the item's resolved level | [sharing.md](sharing.md) § 6 |

## Objects index (data and workflows)

Which access types touch each object, for audits that start from the code.

| Object | Types |
| --- | --- |
| Content items | 1, 3, 4, 5, 7, 8, 11, 13, 14, 15, 16 |
| Media | 1, 3, 7, 8, 11, 16 |
| Users and profiles | 2, 3, 10, 11, 12 |
| Roles and permissions | 10, 11 |
| Members, bindings, hierarchy | 4, 5, 6, 10, 14 |
| Parties | 4, 5, 8 |
| Workflows (definitions, runs, connections) | 1 (public endpoints), 2 (triggers), 4, 9, 13, 14, 15 |
| Queries and search | 1, 3, 4, 9, 11 |
| Settings, features, themes | 11, 12 |
| Tenants, global data | 12 |
| Deployment, recipes | 11, 13, 14 |
| Notifications | 3, 4 |
| Audit trail | 11 |
| OpenId applications and tokens | 11, 13 |

## Pages index

Which access types open each group of pages, for audits of the route table.

| Pages | Shell | Anonymous | Types |
| --- | --- | --- | --- |
| Public pages, public item pages, search | site | yes | 1, 3 |
| Error pages | every shell | yes | all people (1-12, 16) |
| Login, sign-up, password reset, external login | login and every shell | yes | 2 |
| Own account, profile, notifications | site, member | no | 3, 4, 5 |
| Portal: organization switcher, lists, records | member | no | 4, 5, 6 |
| Portal: members, invitations, organization settings | member | no | 5, 6 |
| Admin dashboard, content list and editor | admin | no | 7, 8, 11 |
| Media library | admin | no | 7 (own folder), 8, 11 |
| Menus, taxonomies, translations, approvals | admin | no | 8, 9, 11 |
| Workflows (designer, instances, connections) | admin | no | 9, 11 |
| Users, roles | admin | no | 10, 11 |
| Settings, features, design, search, deployment, OpenId, audit | admin | no | 11 |
| Tenants, feature profiles, global data | admin, Default tenant | no | 12 |
| Page-URL HTTP endpoints of flows | site | per definition | 1, 3, 9 |
| Public file links | site | yes | 1, 16 |
| Shared objects, "Shared with me", Share panel | site, member, admin | no | 16 |
