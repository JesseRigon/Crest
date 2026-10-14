# Permissions: the permission model, per-part access, and approvals

Two deferred pieces of the permission picture: part-level access within a content item, and
a shared approvals system for actions that need someone else's agreement before they happen.
Both sit on the one access machinery ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)).

## Per-part access for the content-items API

**Deferred future work.** Not implemented. Item-level permission checks (`CommonPermissions.ViewContent`/`EditContent`) are what's actually enforced today. This document exists so the gap is a deliberate, tracked decision instead of a silent limitation someone rediscovers later. The content-items API that enforces item-level checks today is in [content-items.md](content-items.md).

> **Ruling 2026-10-10:** per-part permissions become **scope providers** on content fields in
> `Crest.Access` ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery) › Decisions): the seam in step 1, the first
> rule when a type needs it. The provider and controller changes below describe the stock
> shape and are superseded by that.

**What landed (2026-10-13): the seam.** `Crest.Access.ContentPartScope`: a module registers an
`IScopeProvider` with the target `ContentPartScope.Target(type, part)`
(`ContentPart:{type}.{part}`); its rule, read against the item's content index row, shows the
part (`All`), hides it (`None`) or shows it on the rows a filter admits (a semi-join fails
closed). `ContentItemsController.ViewAsync` (`?parts=`) decides `ViewContent` on the item once,
then keeps each requested part its scope admits; a part with no provider follows the item. No
rule ships, so nothing changes until a type needs one; the default-grants question below
belongs to that first rule.

### Per-part permissions

OrchardCore's built-in permission model is **content-type/item level**, not part level. `Crest.Contents.Security.ContentTypePermissions` (`Crest.Contents/Security/ContentTypePermissions.cs`) generates permissions per content type via `ContentTypePermissionsHelper.PermissionTemplates` (e.g. `Editable_<ContentType>`, `Viewable_<ContentType>`) — there is no native concept of "can view `CrestBlazorComponentPart` on this item but not `TitlePart` on the same item." A role either can or can't act on the whole item.

This surfaced while building `CrestContentItemsController`'s new `?parts=`-filtered read endpoint (see `Crest.Server/Controllers/ContentItemsController.cs`), which is meant to return only the requested fields/parts that the caller is actually permitted to see — for a public Site visitor, a logged-in member, or an admin, from the same endpoint, with the response shape driven entirely by permission rather than by which controller was called. The current implementation checks the *same item-level `ViewContent` permission* for every requested part — meaning "permitted" today effectively means "can view this item at all," not yet differentiated per part. The code path is deliberately structured so a real per-part check slots in later (each requested field's permission check is already an isolated step, not inlined into one big condition) — but the check itself doesn't discriminate between parts yet.

Why deferred: this is real new infrastructure (a permission provider + admin UI verification + a default-grant migration decision), not a small addition, and nothing currently consuming the content-items API needs finer-than-item-level granularity yet — the initial consumer (`BlazorCounter.razor`, a public/anonymous read of a Widget-stereotype content item) only ever needs "can this caller view this item," which item-level `ViewContent` already answers correctly. Build this when a real feature needs to differentiate access within a single content item (e.g. a content type mixing a publicly-visible part with an internal-only part), not speculatively ahead of that need.

What true per-part permissions would require:

- [ ] **Add a per-part permission provider.** A new `IPermissionProvider` (a Crest-specific one, e.g. `CrestPartPermissions`) that generates a permission per `(ContentType, PartName)` pair, mirroring how `ContentTypePermissionsHelper` generates per-content-type permissions today.
- [ ] **Verify admin UI exposure.** Admin UI exposure so these generated permissions are assignable to roles (Orchard's Roles admin page already lists dynamically-provided permissions from any registered `IPermissionProvider`, so this is mostly "define the provider correctly," not new UI work — but needs verification once actually built).
- [ ] **Swap the per-field check.** `CrestContentItemsController`'s per-field permission check swaps from `CommonPermissions.ViewContent` to the new per-part permission, keyed by the specific part name being evaluated in that loop iteration.

### Decisions needed

- [ ] **Default grants.** A decision on default grants: does an existing role that already has `Editable_<ContentType>` implicitly get every part's permission (backward-compatible default), or does nothing get granted until explicitly assigned (safer, but a breaking change for any tenant already relying on item-level grants)?

## Approvals

**Not started.** One Crest approvals system that any feature can use when an action needs
someone else's agreement before it happens, not only workflows. In the operations model an
approval is a pause on an action: the unit of work bookmarks, the request is recorded, and
the decision resumes the action as its own unit ([workflows.md](workflows.md) › Units of
work).

`Crest.Workflows` already has approvals ([workflows.md](workflows.md) › Approvals): the
`Request approval` activity records an `ApprovalTask` for a role or permission, the
`api/crest/workflows/approvals` endpoints list and decide them, and the page
`/workflows/approvals` shows them. It is reachable only from inside a workflow.

- [ ] **Move approvals to a shared place.** The general system grows out of that rather than
  beside it: the task, the decision and the queue move to a shared place, and the workflow
  activity becomes one way of requesting an approval.

### Known needs

- [ ] **Personal drive access when a notice fails.** Today the access proceeds and the
  failure is recorded ([media.md](media.md), notices); with approvals, a tenant could
  require one instead.
- [ ] **Overriding the personal drive notice delay**, as an alternative to entering a reason.
- [ ] **Downstream uses.** Request/approval forms and optional approval of generated
  purchase orders; until the shared system exists they use the `Request approval` activity.
- [ ] **Later uses.** Making content public, granting sensitive permissions, publishing
  changes to shipped definitions.

### Decisions needed

- [ ] **Who can decide.** A role, a permission, a named user, the owner of the thing affected.
- [ ] **How many approvers**, and in what order.
- [ ] **Expiry.** What happens to a request nobody decides.
- [ ] **Notifications.** How requesters and approvers are told (the platform's
  notifications, as personal drive notices are).
- [ ] **Waiting and resuming.** What the requester sees while waiting, and how a decision
  resumes the action.
- [ ] **Audit.** Every request and decision recorded in the audit trail ([audit.md](audit.md)).

## The permission model: sets, roles, groups, overrides, reach

Ruling 2026-10-13 (discussed in chat; the access side is in [access.md](access.md) § Members).
One store and one resolution for staff and members; the access gate still gates the admin and
member shells differently. What is shared is the data and how it resolves, not the gating.

### Layers

`permission → permission set → role → group → user`, applied within a **reach**: the tenant
(staff) or one organization (members).

- **Permission**: code-defined by modules, as today. Never created by a tenant or an
  organization.
- **Permission set**: a tenant-defined named list of permissions. Modules ship defaults.
- **Role**: tenant-defined; holds sets and loose permissions. One role name serves both
  sides ("Administrator" is one role; what it yields depends on the reach and the ceilings).
- **Group**: binds users to roles within a reach. A user in a group inherits every role of
  the group; a user may be in several groups.
- **Tenant schema vs organization-owned.** Permissions, sets and roles are the tenant
  schema: organizations reference them and they stay synced with the tenant, so migrations
  and later updates do not break an organization. Groups are not shared: the tenant's
  allow-listed groups (with their roles) are cloned into an organization when it is created
  - the default permission system of a new organization - and from then on the
  organization's admins edit their groups as they see fit, within the bounds the tenant
  sets. Fruitful's `DefaultGroupTemplate` / `MembershipGroup` become this one group type
  (memberships.md "one group mechanism", ruled there, built here).

### Overrides

- Per **user**, at every level from roles down: a user may have a role override, a set
  override and a permission override, each a grant or a deny, within a reach.
- An override belongs to the user, not to the group it was edited under. A user in Group 1
  and Group 2 shows the same overrides in both editors, so there are no cross-group
  conflicts.
- **Most specific wins.** Resolution walks group → role → set → permission; the most
  specific level that mentions a permission decides it. A direct permission override on the
  user grants (or denies) regardless of what any group, role or set says. Two entries at
  the same specificity may not conflict: the editor refuses a grant and a deny of the same
  permission at one level for one user and reach.
- **Ceilings are not overrides.** The member class ceiling, an organization's subscription
  ceiling (Fruitful) and the tenant's org-assignable list are final denials applied after
  resolution; no override passes them.

### Reach

- Staff hold their groups at tenant reach and act on the admin side over every
  organization's data. Staff are never members of an organization's groups (those are
  visible to the organization's admins), and a new customer or contact needs no grant.
- A member holds organization groups at that organization's reach and, on the member side,
  gets only those rows. A user who is both staff and a member of a sub-organization has
  full rights in the admin shell and only the organization's rights in the member shell;
  admin-shell permissions never bleed into the member side.
- Tenant-wide settings: the allow-list of groups and roles an organization starts with and
  that its admins may assign; permissions an organization may not reassign (owner-locked:
  held by the organization's owner account, e.g. payments stays with the account that
  signed up).

### Projection

The authorization hot path reads an index, not the relations: `UserAccessIndex`, keyed by
(user, tenant | organization), with rows per group, role, set and permission and the final
permission list with its provenance (which level produced it, which override). It is rebuilt
for a user when any set, role, group, membership, override or binding touching them changes,
under the permission version. `CallerContextFactory` reads tenant rows on the admin side and
the acting organization's rows on the member side. The relation stores (user ↔ group, group
↔ role, role ↔ set, set ↔ permission, user overrides) remain the source of truth.

### Editors

- Admin: roles, permission sets, tenant groups and user overrides (the permission editor
  `Roles.razor` defers to), plus the organization defaults allow-list in settings.
- Member side: an organization admin edits the organization's groups: membership, roles
  from the allowed list, overrides for its members.

### Status

- [x] Sets, groups, overrides, the projection index and the editors.
- [x] Fruitful groups, templates, overrides and the subscription ceiling fold in
  (memberships.md).

**What landed (2026-10-13).**
- *Sets and roles* (`Crest.Roles`): `PermissionSet` and `IPermissionSetService` (one cached
  document; modules ship defaults through `IPermissionSetProvider`, created once when missing).
  `Role.PermissionSets` beside the loose `RoleClaims`; `RoleStore.GetClaimsAsync` returns the
  loose permissions plus every set's, so every existing reader of a role and every existing
  role keep working. A set write bumps the permission version.
- *Groups, overrides, defaults* (`Crest.Access`, contracts in `Crest.Access.Abstractions`):
  `AccessReach` (tenant or one organization), `IAccessGroupService` (one group type, YesSql
  documents with `AccessGroupIndex` and `AccessGroupMemberIndex`), `IUserAccessOverrideService`
  (per user and reach, role, set or permission level, grant or deny; a same-level conflict
  throws `AccessOverrideConflictException`), `IOrganizationAccessDefaultsService` (site
  settings: default groups, assignable roles, owner-locked permissions). An organization's
  groups are cloned from the default groups when the organization is published
  (`OrganizationAccessGroupsHandler`, Members) and removed with it.
- *Resolution and projection*: `AccessResolution` (pure: group → role → set → permission, each
  level taking its overrides, most specific wins); `UserAccessProjector` stores a
  `UserAccessProjection` per (user, reach) with `UserAccessIndex` rows per group, role, set and
  permission (with its source) under the permission version, rebuilt when stale or when a
  group or override touching the user changes. `CallerContextFactory` reads the tenant rows;
  `MemberCallerContributor` replaces them on the member side with the acting organization's
  rows (admin rights never reach the member side). `CallerStateCache` stays in front.
- *Ceilings*: `OrganizationAssignableRolesCeiling` (a caller acting in an organization holds
  nothing outside the assignable roles' permissions, when the tenant set a list); the member
  class ceiling unchanged; Fruitful's `SubscriptionCeiling`.
- *Members*: binding roles move into the organization's groups (`MemberOrganizationGroups`;
  migration `UpdateFrom1` places every stored binding role, new bindings place their roles).
- *Editors*: `api/crest/permission-catalog`, `permission-sets`, `roles/{name}` (create, update
  with sets and loose permissions, delete), `access/groups`, `access/users/{id}/overrides`,
  `access/users/{id}` (resolved, with provenance), `access/organization-defaults`,
  `access/assignable-roles` (`AccessModelController`); tenant reach takes `ManageRoles`, an
  organization's `ManageOrgMembers` against it, and an organization's groups take only the
  assignable roles and only its members. Admin pages: Roles (sets and permissions), Permission
  sets, Groups (with overrides and the organization defaults); member side: Organization access
  (`Crest.Members.Member.BlazorWasm`); the group and override editors are
  `Crest.Components.Access` components both use.
- Not built: enforcing owner-locked permissions. The ruling names "the organization's owner
  account" (the account that signed up), which the code has no record of; the list is stored.
  Which tenant permission manages tenant groups, sets and overrides: ruled below (› Who
  manages the permission model); the editors use `ManageRoles` until then.

### Who manages the permission model

Ruling 2026-10-13. Two permission sets, each made of CRUD permissions:

- **Manage permissions** (set): roles and permission sets, which always go together.
  Sub-permissions: create, read, update, delete roles; create, read, update, delete
  permission sets. Permissions themselves are not CRUD-managed - modules bring them, or the
  content, media and workflow systems generate them - so only reading the catalog is a
  permission. **User overrides** belong to this set (create, read, update, delete overrides).
- **Manage groups** (set): create, read, update, delete groups and group membership.

**Reach of user-management permissions.** Managing a user's overrides and group membership is
bounded twice: by the shell (tenant reach on the admin side, the acting organization on the
member side) and by the **user hierarchy** - a manager manages only the users under them, i.e.
`subtree(manager)` from the closure table ([parties.md](parties.md) › Hierarchies; the staff
hierarchy is the tenant organization's). The hierarchy is mandatory for managing users:
there is no "manages everyone" without a position. The super user is not narrowed. This is
also where the hierarchy's subtree expansion first meets the scope machinery.

**Default hierarchy.** Admins are the roots: in an organization, its org-admins; in the
tenant, the tenant admins. Every other user is placed under the admins by default when they
are bound (an organization member under that organization's org-admins, a staff user under the
tenant admins), so a fresh tenant or organization is manageable without anyone building a
hierarchy. Managers then restructure below the roots as they see fit.

To build: the CRUD permissions and the two sets (replacing `ManageRoles` on the new editors);
the hierarchy bound on overrides and group membership writes and on the user lists the
editors show.

### The organization owner

Ruling 2026-10-13.

- Every organization has exactly one **owner**: a user account recorded on the organization.
  The first member user bound to an organization becomes its owner and its default org-admin.
  More org-admins may be added later; there is still only one owner.
- The owner is always an org-admin: their org-admin role cannot be removed or lowered while
  they own the organization, by anyone. The owner stands apart from the hierarchy's
  management (no one manages the owner); as an org-admin they are one of its roots.
- Owner-locked permissions (tenant setting, above) are held by the owner only and cannot be
  placed in any of the organization's groups or overrides.
- **Transfer.** Ownership moves to another user of the same tenant - possibly one outside
  the organization, never one outside the tenant. A transfer is started either by staff on
  the tenant side (a tenant permission) or by the current owner from their own account; no
  one else, not an org-admin. A new owner from outside the organization is bound to it as a
  member on transfer (owning implies membership) and **becomes an org-admin automatically**;
  the previous owner keeps their binding and stays an org-admin until someone changes it
  (they are no longer protected). Every transfer is an audit event.
- To build: the owner record, first-binding assignment, owner-locked enforcement in the
  resolution (a ceiling for everyone but the owner), the transfer endpoint and its two entry
  points (admin organization page, member owner's settings).
- [x] Ruled 2026-10-14: party-derived roles stay derived (organization roles from the
  organization); `CrestAssignmentPart` retired; `CrestOrganizationPart` and the organization scope to
  Crest.Parties (access.md). To build.
