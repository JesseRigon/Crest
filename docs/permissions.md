# Permissions: the permission model, per-part access, and approvals

The permission model (who holds which permission, at which reach), part-level access within a
content item, and a shared approvals system for actions that need someone else's agreement
before they happen. All of it sits on the one access machinery
([access.md](access.md), [workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)).

## Per-part access for the content-items API

The platform's permission model is content-type and item level: `ContentTypePermissions`
(`Crest.Contents/Security/ContentTypePermissions.cs`) generates per-type permissions
(`Editable_<ContentType>`, `Viewable_<ContentType>`); a role can or cannot act on the whole item.

Per-part access is a **scope provider** on content parts in `Crest.Access`, not a generated
permission per (type, part). The seam is `Crest.Access.ContentPartScope`: a module registers an
`IScopeProvider` with the target `ContentPartScope.Target(type, part)`
(`ContentPart:{type}.{part}`); its rule, read against the item's content index row, shows the
part (`All`), hides it (`None`) or shows it on the rows a filter admits (a semi-join fails
closed). `ContentItemsController.ViewAsync` (`?parts=`) decides `ViewContent` on the item once,
then keeps each requested part its scope admits; a part with no provider follows the item. The
response shape is driven by the caller's access, not by which endpoint was called, so a public
visitor, a member and an admin use the same endpoint.

No rule ships: until a content type mixes, say, a public part with an internal-only part,
every part follows the item. The first rule is built when a type needs it.

### Open questions

- **Default grants for the first part rule.** Does a role that can view or edit the item see
  every part unless a rule narrows it (backward-compatible), or does a part with a rule show
  nothing until granted (safer, but breaks tenants relying on item-level grants)?

## Approvals

**Not started.** One Crest approvals system that any feature can use when an action needs
someone else's agreement before it happens, not only workflows. In the operations model an
approval is a pause on an action: the unit of work bookmarks, the request is recorded, and
the decision resumes the action as its own unit ([workflows.md](workflows.md) › Units of
work).

`Crest.Workflows` already has approvals ([workflows.md](workflows.md) › Approvals): the
`Request approval` activity records an `ApprovalTask` for a role or permission, the
`api/crest/workflows/approvals` endpoints list and decide them, and the page
`/workflows/approvals` shows them. It is reachable only from inside a workflow. The general
system grows out of that, not beside it: the task, the decision and the queue move to a shared
place, and the workflow activity becomes one way of requesting an approval.

### Backlog

- [ ] **Move approvals to a shared place** (above).
- [ ] **Personal drive access when a notice fails.** Today the access proceeds and the
  failure is recorded ([media.md](media.md), notices); with approvals, a tenant could
  require one instead.
- [ ] **Overriding the personal drive notice delay**, as an alternative to entering a reason.
- [ ] **Downstream uses.** Request/approval forms and optional approval of generated
  purchase orders; until the shared system exists they use the `Request approval` activity.
- [ ] **Later uses.** Making content public, granting sensitive permissions, publishing
  changes to shipped definitions.

### Open questions

- **Who can decide.** A role, a permission, a named user, the owner of the thing affected.
- **How many approvers**, and in what order.
- **Expiry.** What happens to a request nobody decides.
- **Notifications.** How requesters and approvers are told (the platform's
  notifications, as personal drive notices are).
- **Waiting and resuming.** What the requester sees while waiting, and how a decision
  resumes the action.
- **Audit.** Every request and decision recorded in the audit trail ([audit.md](audit.md)).

## The permission model: sets, roles, groups, overrides, reach

One store and one resolution for staff and members; the access gate still gates the admin and
member shells differently. What is shared is the data and how it resolves, not the gating (the
access side is in [access.md](access.md) › Members).

### Layers

`permission → permission set → role → group → user`, applied within a **reach**: the tenant
(staff) or one organization (members).

- **Permission**: code-defined by modules. Never created by a tenant or an organization.
- **Permission set**: a tenant-defined named list of permissions. Modules ship defaults.
- **Role**: tenant-defined; holds sets and loose permissions. One role name serves both
  sides ("Administrator" is one role; what it yields depends on the reach and the ceilings).
- **Group**: binds users to roles within a reach. A user in a group inherits every role of
  the group; a user may be in several groups.
- **Tenant schema vs organization-owned.** Permissions, sets and roles are the tenant
  schema: organizations reference them and they stay synced with the tenant, so migrations
  and later updates do not break an organization. Groups are not shared: the tenant's
  allow-listed groups (with their roles) are cloned into an organization when it is created
  (the default permission system of a new organization), and from then on the
  organization's admins edit their groups as they see fit, within the bounds the tenant
  sets. Fruitful's `DefaultGroupTemplate` / `MembershipGroup` are this one group type
  (memberships.md, "one group mechanism").
- **Derived roles stay derived.** Business roles derive from the organization's roles, person
  roles from the person ([parties.md](parties.md) › Core party records), and feed the same
  index.

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
  ceiling (Fruitful), the tenant's org-assignable list and owner-locked permissions are final
  denials applied after resolution; no override passes them.

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
  held by the organization's owner, e.g. payments stays with the account that signed up).

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

### Who manages the permission model

Two permission sets, each made of CRUD permissions:

- **Manage permissions**: roles and permission sets, which always go together (create, read,
  update, delete roles; the same for permission sets) and user overrides (create, read,
  update, delete). Permissions themselves are not CRUD-managed (modules bring them, or the
  content, media and workflow systems generate them), so only reading the catalog is a
  permission.
- **Manage groups**: create, read, update, delete groups and group membership.

**Reach of user-management permissions.** Managing a user's overrides and group membership is
bounded twice: by the shell (tenant reach on the admin side, the acting organization on the
member side) and by the **user hierarchy**: a manager manages only the users under them,
`subtree(manager)` from the closure table ([parties.md](parties.md) › Hierarchies; the staff
hierarchy is the tenant organization's). There is no "manages everyone" without a position. The
super user is not narrowed.

**Default hierarchy.** Admins are the roots: in an organization, its org-admins; in the
tenant, the tenant admins. Every other user is placed under the admins when they are bound (an
organization member under that organization's org-admins, a staff user under the tenant
admins), so a fresh tenant or organization is manageable without anyone building a hierarchy.
Managers then restructure below the roots as they see fit.

**Staff over organizations.** Staff holding `ManageOrgMembers` at tenant reach manage every
organization's members (their overrides, group membership and the organization's hierarchy)
from the admin shell, without a position in the organization's hierarchy; the permission is
what reaches across organizations. The owner is never managed.

### The organization owner

- Every organization has exactly one **owner**: a user account recorded on the organization.
  The first member user bound to an organization becomes its owner and its default org-admin.
  More org-admins may be added later; there is still only one owner.
- The owner is always an org-admin: their org-admin role cannot be removed or lowered while
  they own the organization, by anyone. The owner stands apart from the hierarchy's
  management (no one manages the owner); as an org-admin they are one of its roots.
- Owner-locked permissions (tenant setting, above) are held by the owner only and cannot be
  placed in any of the organization's groups or overrides.
- **Transfer.** Ownership moves to another user of the same tenant, possibly one outside the
  organization, never one outside the tenant. A transfer is started either by staff on the
  tenant side (a tenant permission) or by the current owner from their own account; no one
  else, not an org-admin. A new owner from outside the organization is bound to it as a member
  on transfer (owning implies membership) and becomes an org-admin automatically; the previous
  owner keeps their binding and stays an org-admin until someone changes it (they are no longer
  protected). Every transfer is an audit event.

### Implementation

- *Sets and roles* (`Crest.Roles`): `PermissionSet` and `IPermissionSetService` (one cached
  document; modules ship defaults through `IPermissionSetProvider`, created once when missing).
  `Role.PermissionSets` beside the loose `RoleClaims`; `RoleStore.GetClaimsAsync` returns the
  loose permissions plus every set's, so every existing reader of a role keeps working. A set
  write bumps the permission version.
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
  rows. `CallerStateCache` stays in front.
- *Ceilings*: `OrganizationAssignableRolesCeiling` (a caller acting in an organization holds
  nothing outside the assignable roles' permissions, when the tenant set a list); the member
  class ceiling; Fruitful's `SubscriptionCeiling`; `OwnerLockedCeiling` (below).
- *Members*: binding roles live in the organization's groups (`MemberOrganizationGroups`;
  new bindings place their roles there).
- *Management permissions*: `AccessPermissions` (Crest.Access): Create/Read/Update/Delete for
  roles, permission sets, user overrides, groups and group membership, and
  `ReadPermissionCatalog`; the default sets "Manage permissions" and "Manage groups"
  (`AccessPermissionSetProvider`, created by the Access migration, which also gives them to the
  Administrator role; the stereotype grants the permissions too). `AccessModelController`, the
  roles list, the route table and the menu ask the operation's permission; at an organization's
  reach it is asked against the organization (`OrganizationReachPermissionMapper`: a caller
  acting in no organization for every one, a caller acting in one only there). The
  organization defaults read and write take `ReadGroups` and `UpdateGroups`.
- *Hierarchy bound*: `IUserManagementReach` (Access.Abstractions), implemented by Members'
  `MemberHierarchyReach` over the closure table: the users under the caller in the reach's
  hierarchy (the tenant organization's for the tenant), never themself, never the owner; the
  shell bounds the reach; the super user manages every user of the reach; a staff caller
  acting in no organization whose decision grants `ManageOrgMembers` manages every member of
  any organization except its owner. Override reads and writes, group-membership writes and the
  editors' user lists (`api/crest/access/managed-users`) go through it. Default hierarchy:
  `MemberBindingService` places every newly bound user under the reach's admins unless they are
  one or already have a manager.
- *Owner*: `IOrganizationOwnership` (Access.Abstractions; Members'
  `OrganizationOwnershipService`, an `OrganizationOwnerRecord` per organization with
  `OrganizationOwnerIndex`). The first member bound records the owner and joins the
  organization's group holding the Member Administrator role (`ClaimOwnershipAsync`); a
  migration gives every existing organization with member admins an owner (one of them: the
  stored data does not say which was bound first). Protection: a group update, deletion or
  membership removal that leaves the owner in no group holding that role, a role-level deny of
  it, and removing the owner's binding are refused (`OwnerProtectionException`).
  `OwnerLockedCeiling` denies owner-locked permissions to everyone acting in the organization
  but its owner, and an organization's override cannot grant them. Transfer:
  `POST api/crest/members/organizations/{id}/owner` (and `GET` for the owner), started by staff
  holding `TransferOrganizationOwnership` (a tenant permission on the member ceiling) or by the
  current owner; records the `OwnershipTransferred` audit event.
- *Editor APIs and pages*: `api/crest/permission-catalog`, `permission-sets`, `roles/{name}`,
  `access/groups`, `access/users/{id}/overrides`, `access/users/{id}` (resolved, with
  provenance), `access/organization-defaults`, `access/assignable-roles`
  (`AccessModelController`). Admin pages: Roles (sets and permissions), Permission sets, Groups
  (with overrides and the organization defaults); member side: Organization access
  (`Crest.Members.Member.BlazorWasm`) with `CrestOrganizationOwnerPanel` (Crest.Components);
  the group and override editors are `Crest.Components.Access` components both use.

### Backlog

- [ ] The admin organization page (Crest.Parties) hosts `CrestOrganizationOwnerPanel`, as the
  member Organization access page does.

### Open questions

- **Staff grants per organization.** Scoping a staff user's `ManageOrgMembers` to particular
  organizations rather than the whole tenant (Fruitful territories).
