# Sharing: who may reach an item, on any kind of item

Sharing is grants and blocks on objects that flow down a tree, as in Google Drive, plus
ordered rules. It is its own module so any module's items can be shared; files
([media.md](media.md)) were its first use. The engine, the access index and list scopes,
business roles, the interface, system accounts, deleting and moving, comments and filed items
are built (compiled; the tests are written and not yet run). Search filtering (F5) is open;
the remaining gaps are under [Backlog](#backlog).

## Design

- **Sharing is its own module**: `Crest.Sharing` and `Crest.Sharing.Abstractions`, a tenant
  feature that can be turned off. Not `Crest.Access.Sharing`: it is a domain with its own data
  (entries, rules, the access index, the time registry, the Share panel), and it registers
  with the access gate like every other domain ([access.md](access.md) › Principles › Access is
  extended, never rewritten). Without the feature a tenant has no drives, sharing or rules, and
  its content items keep the standard permissions. Nothing about it crosses tenants.
- **Any module makes its items shareable** through the abstractions: it registers a
  **shareable kind**: how to recognise one of its items, its id, owner, parent (the chain
  grants flow down), the facts rules test, which of its permissions map to which access level,
  and whether it is inactive. Media registers drives, folders and files; Contents registers
  content types and items; Parties, Members and later modules opt in. None of them decides
  anything about sharing. Content-item sharing lives in neither Media nor Contents: Contents
  registers its kind.
- **Sharing registers with Access once**, for every kind: one authoritative mapper
  (`IAuthoritativeResourcePermissionMapper`, the object decision) and `IScopeProvider`s over the
  access index (lists and search). An inactive container is denied by the mapper, not by a
  ceiling: a ceiling would also deny tenant administrators, who must still reach it.
- **Precedence**: for a kind registered as shareable, while the feature is on, the sharing
  mapper is **authoritative** (default deny, the owner always allowed, type-level entries in
  place of per-type role permissions) and the kind's own mapper is not consulted. Tenant
  administrators pass at the decision's super-user step, before any mapper. With the feature
  off, or for a kind not registered, the module's own mapper decides. Nothing depends on
  registration order: priority decides ([access.md](access.md) › Mapper roles and priority).
- **Extending or replacing sharing.** A module outside Crest can:
  - **add** sharing for items by contributing entries and rules through an
    `IShareEntrySource`: the engine resolves them with the stored ones under the same
    precedence (principal tier, depth, order, blocks, Anyone only Viewer, Admin only on a root),
    so a contribution wins only where sharing's own rules let it; it never grants past a
    deeper block or a more specific principal's decision. At one depth contributions follow the
    stored sharing, in feature-priority order; the explanation names their source. A provider
    that could grant past sharing would be the authority, and is shipped as one instead;
  - **replace** sharing for specific items with an authoritative mapper of higher feature
    priority;
  - **replace a kind** by registering one with the same name at a higher priority
    (`ShareableKinds` picks one per name; two at one priority conflict and allow nothing).

  "Shared with me" reads the access index, so contributed grants appear in it like stored ones.
- **Owners.** The owner of an object holds Manager on it (Admin when it is a root), and the
  owner of a root administers its whole tree. At one depth an object's own entries come after
  the rules defined on it, so an entry overrides a rule about the same principal. "Shared with
  me" counts explicit grants to the user, their roles and their organization, not tenant-wide or
  public ones.

## Module

`Crest.Sharing.Abstractions`:
- the model: `SharePrincipal` (Anyone, Tenant, Organization, Role, User), `AccessLevel`
  (Viewer, Commenter, Editor, Manager, Admin: the generic name for Drive Admin, granted only
  on a root object), `ShareEntry` (a grant or a block, optionally time-bound), `ShareRule`
  (ordered conditions → entries);
- `IShareableKind` and `ShareableObject`: what a module registers; the optional
  `IShareableTree` (`ChildrenAsync`: what a change recalculates below an object),
  `IShareableScopeTarget` (`ShareScopeTarget`: the index table its objects are rows of and the
  object-id column), `IShareableRoots`, `IShareableAdminAssignment`,
  `IShareableAdminEligibility`, `IShareableLinks` (separate interfaces rather than members of
  `IShareableKind`, so "has no tree" is distinguishable from "has no children");
- `ISharingService`: grant, block, remove, set rules, list entries, "shared with me", explain;
- `ISharingResolver`: the effective level for a caller on an object;
- `IShareAccessIndex`: what a module calls when something other than sharing changes an
  object's access (created, moved, deleted, or a fact a rule reads, requirement 78), and the
  rebuild;
- `IShareChangeChecks` (deleting and moving, 69-70), `IShareComments` and `IShareCommentStore`
  (71-72), `IShareableChildSource` (what another module files in a folder, 74),
  `IShareIndicators` (98), `ISharePrincipalDirectory`.

`Crest.Sharing`:
- the share documents (`ShareDocument`, one per object with entries or rules) and their index;
- the resolver (`SharingResolver`, requirements 58-65), reading time bounds exactly at every
  decision;
- the mapper (`SharingPermissionMapper`), the permissions (`ManageTenantSharing`,
  `ShareWithAnyone`, both security-critical), the service (`SharingService`) with its change
  checks (85) and audit (99);
- the access index, recalculation, the time registry and its job, the list scopes (below);
- the API and the `Sharing` recipe step.

## Terms

- **Shareable object**: an item a module registered a kind for (a drive, folder or file; a
  content item; a content type as a link in the chain), with a stable id, an owner and a
  parent. Its **chain** is its parents up to a root.
- **Principal**: Anyone (public), Tenant, Organization, Role or User.
- **Role**: any group the business recognises: staff roles (Editor, Sales Rep, Marketing) and
  business roles (Customer, Vendor, Lead, Member).
- **No-permission role (shim)**: a role that grants no permissions and exists only so people
  can be shared with as a set, until grouping is decided ([parties.md](parties.md) › Open
  questions › Grouping).
- **Access level**, each including the ones before it: **Viewer** (read), **Commenter** (read
  and comment), **Editor** (create, read, update), **Manager** (create, read, update, delete),
  **Admin** (a Manager who can also change sharing and rules; Drive Admin for a drive),
  granted only on a root object.
- **Tenant administrator**: holds every permission on everything in the tenant; not an access
  level, never granted on an object.
- **Entry**: a grant ("Customer: Viewer") or a block ("Sam: Blocked") on one object,
  optionally time-bound.
- **Rule**: conditions → grants and blocks, defined on an object or the tenant, applying to
  matching objects below it.
- **System account**: a principal that owns things rather than a person: the tenant account,
  and one account per organization.
- **Published**: readers only ever get the published version; drafts are for Editors and
  Managers.

## Example

A shared drive of product information. Customer is a business role, Staff and Marketing are
staff roles, Sam is a staff user, and Dana is an external contractor with a user account.

```
Product Info (shared drive)  Customer: Viewer, Staff: Viewer, Sam: Blocked
├── Spec sheets/             Tenant: Viewer
└── Images/                  Marketing: Editor
    │  rule 1: extension is .psd → Customer: Blocked, Dana: Editor
    │  rule 2: tag is "final"    → Anyone: Viewer
    ├── chair-original.jpg   customers, staff, marketing
    ├── chair.psd            staff, marketing, Dana - not customers
    └── chair-final.jpg      everyone, signed in or not: public
```

- The drive is owned by the tenant account and grants Customer and Staff. Access is denied by
  default, so nobody else gets in and no blocks are needed to keep them out.
- Spec sheets is open to the whole tenant, so Dana can see it too. Sam cannot: his block is
  about his user, which outranks a tenant grant at any depth. Only "Sam: Viewer" on Spec sheets
  would let him in.
- Everything under Images inherits customers and staff and adds marketing.
- Rule 1 lives on Images, the highest folder it should cover. On every Photoshop file under
  Images it blocks the inherited Customer grant and grants Dana. A Photoshop file in Spec sheets
  is untouched, and elsewhere under Images Dana has no grant.
- Rule 2 makes final images public. Public is absolute for viewing, so Sam can view
  chair-final.jpg despite his block, but his block still stops him commenting on or editing it.
- A customer opening chair-final.jpg sees the file, not how it is shared. A Drive Admin
  sees the whole chain. A customer never sees chair.psd: it is in none of their listings, searches
  or counts.
- The rules list for Product Info shows rules on Product Info and above, not rules 1 and 2.
- A marketer deleting Images is refused if they may not delete chair.psd, and the refusal does
  not mention it.

## 1. Foundations

The content sharing feature and its settings; system accounts (tenant and organization); the
role registry with role kind, and assignment restricted to assigned roles; default Anyone and
Tenant view grants removed when sharing is on.

**System accounts.**

33. Each tenant has one tenant account, created with the tenant, and each organization has one
    organization account, created with the organization. They own what their tenant or
    organization owns, such as shared and organization drives.
34. A system account cannot sign in, cannot be impersonated, and does not appear in user lists
    or pickers.
35. A system account is never counted as a user, member or seat by subscriptions, memberships,
    pricing or plan limits. Counting excludes system accounts by construction, not by a filter
    each count must remember.
36. Actions the system takes on a tenant's or organization's behalf are attributed to its
    account in the audit trail, distinct from people and machine actors
    ([machine-actors.md](machine-actors.md)).

Built as `Crest.Access.SystemAccounts`: the tenant account is `system:tenant`, an
organization's `system:organization:{id}`. They are ids, not user records, so 34-35 hold by
construction; no user may take a `system:` id (the user store and the `Users` recipe step refuse
it). The system actors act as them (`CallerContext.SystemAccountId`, `ActorId`), and the access
audit attributes their actions to the account. `ShareableObject.OwnerAccountId` names a system
owner (Media: shared drives are the tenant's, organization drives the organization's); the
organization system actor is its owner. Sharing refuses a system account as a principal.

**The role registry with role kind.**

44. Each role has a **kind**, kept in Crest's role registry: **Assigned** (stored on users, the
    ordinary kind) or **Derived** (computed, with the provider that computes it). The platform's
    role model has no field for this, and its built-in "system role" mechanism covers only its
    own fixed roles.
45. Derived roles cannot be assigned. Every assignment path (the user editor, the users API,
    recipes, bulk assignment) offers Assigned roles only and refuses Derived ones on the server.
    Like the platform's assignable-role filter, it also leaves out system roles (Anyone,
    Authenticated).

Built in [§ 5](#5-business-roles).

**Default view grants removed.**

84. No recipe shipped with Crest or a host built on it grants anything to Anyone or Tenant
    unless it means to.

The platform gives Anonymous and Authenticated `ViewContent` by default, and Crest's setup
recipe grants Anonymous `ViewContent` again; with sharing on that would make every published
item public. `DefaultViewGrantsRemover`: when Sharing is enabled, the Anonymous and
Authenticated roles lose `ViewContent`, `ViewOwnContent`, `View_{Type}` and `ViewOwn_{Type}`.
Public content then needs an Anyone grant (on the type or the item), signed-in visitors a
Tenant grant; checks asked without an item no longer pass for visitors. Turning sharing off
does not restore the grants. A recipe that enables sharing and then grants these by a Roles step
puts them back: shipped recipes must not.

## 4. Access

Entries and rules, the resolution engine and its explanation, the access index and
recalculation, the time registry and job, enforcement on every read path, list and search
filtering.

**Principals.**

37. Tenant means everyone signed in to that one tenant, staff and members, and never anyone
    from another tenant on the same instance.
38. A member acts for one organization at a time, and only that organization's entries count
    for them.

**Entries.**

49. **Default deny.** Nobody has access unless granted, except the owner. No decision is not a
    block; it is simply no access, so a new user or role sees nothing and nothing needs updating
    when one is added.
50. Grants and blocks flow down the tree. Each object inherits its parent's and adds its own.
51. Blocks are optional: they carve an exception out of an inherited grant, such as one user or
    role that must not see a folder under a tenant-wide grant. There is no "stop inheriting"
    switch; narrowing is a block on the inherited principal plus grants for whoever keeps access.
52. An object shared with someone is reachable through its link even if they cannot open its
    parent folder, as in Drive.
53. An entry can have a start and an end.

**Rules.**

54. A rule applies only to objects below the folder, drive or tenant it is defined on that
    match all its conditions.
55. A rule belongs at the highest folder whose contents it should cover, so one rule replaces
    many individual shares.
56. Conditions cover at least file type, extension, named group ("Documents", "Images"), content
    type, metadata and tags, and time (a window, a schedule, an age). Modules can add their own.
57. A rule's effects are grants and blocks, the same as entries.

**Resolution.** Precedence runs from general to specific on two axes:

- **Principal**: Anyone → Tenant → Organization → Role → User.
- **Depth**: tenant → drive → each folder → the object.

58. Every entry and every matching rule effect on an object's path is a decision: a principal,
    a grant or block, its depth, and its position in that level's order. Only decisions about
    principals a person matches count for them: their user, their roles (business roles
    included), their acting organization, the tenant and Anyone.
59. **Principal first.** The most specific principal tier with any decision for the person
    decides. A user decision beats any role decision, a role decision beats any organization
    decision, and so on, at any depth. With no decision in any tier, access is denied.
60. **Then depth.** Within the deciding tier, the deepest decision wins. A user blocked on a
    folder and granted on a file inside it can open that file. A user with the Customer and
    Vendor roles, both blocked on a folder, can open a subfolder that grants Vendor.
61. **Then order, then grant.** Within one tier at one depth, rules apply in their listed order
    with equal priority, so a later rule overrides an earlier one about the same principal.
    If the person's different principals in that tier still disagree at that depth (one of
    their roles granted, another blocked), the grant wins, and grants stack to the highest
    level.
62. **Public is absolute for viewing.** When an object's Anyone decision, resolved by depth and
    order within the Anyone tier, is a grant, everyone can view it and no block removes that.
    Everything beyond viewing is still decided by 59–61: a user blocked on a public file can
    view it but not comment on, edit or delete it.
63. Anyone can only be granted Viewer.
64. Public follows the tree like any grant: children of a public folder are public, and a child
    leaves public view only by a deeper "Anyone: Blocked".
65. Resolution is fully determined by the path, the person's principals and rule order.

**Who sees the sharing.**

66. Only a Drive Admin of an object's drive, or a tenant administrator, sees how it is shared:
    every entry, block and matching rule from the tenant down, in order, which decision won, and
    who has access as a result. Everyone else sees nothing of it. Changing a tenant-level step
    needs the tenant permission in 85.
67. A folder's rules list shows rules from the tenant down to that folder, not below it.
68. An object a person cannot view does not exist for them: it is in no listing, search, picker,
    count, explanation or error message.

**Deleting and moving.**

69. Deleting needs Manager. Deleting a folder needs Manager on it and everything below it,
    including objects the person cannot see. If any fail, nothing is deleted, and the refusal
    says the folder contains items they cannot delete, without naming or counting them.
70. Moving changes the access of everything moved, so it needs Manager over everything moved,
    plus Editor on the destination, with the same refusal.

`IShareChangeChecks` does this for any kind (Manager over the object and everything below it, a
kind's children and what is filed in it, each resolved live; moving also Editor on the
destination). Media's file service makes the same checks over its placements.

**Comments.**

71. A Commenter can add comments and do nothing else.
72. Comments are stored in the object's metadata by default. A file type can keep them where
    that format keeps comments instead, through a per-type comment store.

`IShareComments`: a Viewer reads, a Commenter adds, the author or a Manager removes, audited;
stored in the object's share document unless a registered `IShareCommentStore` or the kind
itself handles it.

**The access index and enforcement.**

75. Access is precomputed. For every object (every placement, for a hard-linked file), an index
    holds each principal's winning grant or block and its tier, and whether the object is
    public, after the resolution above. Every read path checks the index; nothing is resolved on
    access.
76. A person's principals are resolved once per session or when they change, so a check is a
    lookup of their most specific principal with an entry on the object, or of the public flag.
77. Every read path enforces access: object endpoints, folder listings, search, pickers, the
    display routes and file bytes. Listings and search filter in the database query, never by
    dropping rows after paging.
78. The index is recalculated for only the affected objects when an input changes: an entry, a
    rule or its order, a move or rename, metadata a rule reads, a content type's entries, a role
    or relationship, an organization binding. A rule change recalculates its scope; a move, the
    moved subtree.
79. Every time-bound entry and time-based rule registers its next boundary in a time registry,
    indexed by time. A per-tenant job runs every minute, reads only the registry entries now
    due, recalculates the objects they affect and registers their next boundaries. It never
    scans every object. Index entries with a known end carry it, and reads ignore them once it
    passes, so access ends on time even between runs.
80. Removing access takes effect before the change is reported as done. Adding access may wait
    for its recalculation, and the Share panel shows it as pending.
81. The explanation (66) and the index come from the same entries and rules and always agree.
    Crest can verify any scope by recalculating and comparing.
82. Failure is closed: an object with no resolvable access or no index entry is not visible.
83. Public bytes are cacheable. Private bytes are served only after the access check.

**Gating changes.**

85. Changing sharing or rules inside a drive needs Drive Admin of that drive. Tenant-level rules
    and entries, which reach every drive, need a dedicated tenant permission. Granting Anyone
    also needs a dedicated permission, so not every Drive Admin can make content public.

**Audit.**

99. Every sharing and rule change is recorded in the audit trail ([audit.md](audit.md)) with who
    made it, including while impersonating.

### Access index size and recalculation (F3)

Access is precomputed per placement and principal: one document per object (per placement)
holds each principal's winning decision, inherited ones included, with its window; lists
semi-join it. Not an ancestor closure resolved at query time, because the winner within a tier
depends on depth and order across the whole chain and rules match per object, so a closure would
move the resolution into every list query and could not agree with the resolver by
construction. The cost is rows per inherited pair and recalculating subtrees, bounded by
batching: a change that may remove access is applied before returning, in batches; one that only
adds access is applied at once up to 200 objects and otherwise by the job.

**Multi-path inheritance.** Folders are hard-linked (media F4), so a child of a hard-linked
folder is reached through every placement of the folder. A kind names the extra parents in
`ShareableObject.OtherParents` (Media: the folder's other hard placements, and an attached-field
folder's content item). `SharingResolver.ChainsAsync` walks every path (at most 16, the primary
path first); `ResolveAsync` resolves each and gives the most any path not through an inactive
drive gives. The access index keeps one document per path (`PathKey`, empty for the primary
path; `AccessKey` = object id, or `{id}~{path key}`), and the list scope runs the tier logic per
path over `ShareAccessObjectIndex.AccessKey` and admits an object when any path admits the
caller. A change on a path other than the primary one reaches the index through the kind's
children (Media lists a folder's children through every placement).

### Implementation

- **Scope model** (`Crest.Access.Abstractions/Scope.cs`): `ScopeCondition` has comparisons
  (`<`, `<=`, `>`, `>=`), `IsNull`, and semi-joins (`InSelect`, `NotInSelect`: a column in a set
  a `ScopeSelect` selects from another index table under its own `ScopeFilter`). All three
  compilers compile them with the tenant prefix, dialect quoting, an alias per selected table
  and bound values: `ScopeExpressions` (YesSql's `IsIn<TIndex>`/`IsNotIn<TIndex>`, the index
  type found by table name), `StructuredQueryCompiler`, and the SQL rewriter's scope predicate.
  In memory a semi-join cannot be evaluated: `ScopeFilter.Matches` throws
  `NotSupportedException` for a filter holding one (`HasSelect` lets a source refuse up front).
  `IScopeProviderSource` lets a domain register providers whose targets are known only at run
  time.
- **Access index** (`ShareAccessDocument` with `ShareAccessIndex`, `ShareRoleShadowIndex`,
  `ShareAccessObjectIndex`; `ShareAccessIndexer`). Per object, per principal with a decision:
  the principal's winning decision after depth and order (its kind is the tier), effect, level,
  depth, and its window. Each principal gets a row per window between the chain's start and end
  times, so a read at any instant picks the right row: when a deeper time-bound block ends, the
  shallower decision's row starts at that instant, without waiting for the job. Owners get an
  owner row; an inactive object gets none. Within the role tier a caller's roles disagree by
  depth (requirement 60), which a per-principal row cannot settle alone: a role denial deeper
  than another role's grant is stored as a shadow row (carrying the deeper row's level), and the
  list scope drops a role grant a shadow of the caller's roles covers. The public flag is the
  Anyone row (`IsPublic`); `IsShared` (a grant to anyone but the owner, other than Anyone) sits
  beside it on `ShareAccessObjectIndex`.
- **Recalculation**: a change to entries or rules recalculates the object and everything below
  it, as F3 describes (`ShareRecalculation` for queued subtrees). Tenant-level changes
  recalculate every indexed object.
- **Time**: each document registers its next boundary (`ShareAccessObjectIndex.NextBoundaryUtc`,
  the time registry); `ShareAccessBackgroundTask` runs every minute per tenant, reads only the
  rows due and recalculates them (dropping passed windows), then the queued subtrees.
- **List scopes** (`SharingScopeProvider`, through `SharingScopeProviderSource`), one per kind
  with a scope target, taking a minimum level (`Build(caller, minimum)`): visible iff public, or
  the user row grants, or (no user row) a role row grants with no deeper role denial, or (no role
  row) the organization grants, or (none of those) the tenant grants; rows active now only; the
  tenant administrator sees everything. The mapper decides single objects; among kinds that
  recognise a resource it picks the one that maps the permission (a string is a media path to
  Media and a type name to Contents).
- **Consistency** (81): the index is calculated from the resolver's own chain and decisions
  (`SharingResolver.TimedDecisionsAsync`, `OwnerLevels`); `ShareAccessIndexer.VerifyAsync`
  recalculates an object and lists any difference from its stored rows.
- **Rebuild**: kinds may list their roots (`IShareableRoots`); enabling Sharing queues a
  rebuild (`IShareAccessIndex.QueueRebuildAsync`, also callable on demand), which the job turns
  into one queued subtree per root and runs a few per pass.
- **Admin assignment by a tenant permission** (media requirement 31): a kind implementing
  `IShareableAdminAssignment` names a tenant permission whose holders may add and remove Admin
  grants on the kind's roots without being Admin there, and see only those grants; every other
  entry, and Admin below a root, still takes the tree's Admin. `AppliesTo(root)` limits it per
  root (Media: shared drives only). Each change is audited with "(through <permission>)".
  `IShareableAdminEligibility.MayHoldAdmin(root, principal)`: `SharingService` refuses an Admin
  grant to a principal the kind rules out, and `SharingResolver` counts such a grant, stored or
  contributed, as Manager (Media: only a personal drive's owner holds Admin there).
- **Organization-scoped roles**: `SharePrincipal.OrganizationId`, for roles only
  (`SharePrincipal.Role("MemberAdmin", "acme")`, `role:MemberAdmin@acme` in recipes), in the
  Role tier; a caller matches it when they hold the role and act for that organization
  (requirement 38). The index stores it as `role@org:id`.
- **Media's kind**: `Crest.Media/Sharing/MediaShareableKind.cs`, through `MediaSharingStartup`
  (`[RequireFeatures("Crest.Sharing")]`), with its placements' scope target, tree, roots and
  admin assignment. Sharing is off by default, so media keeps its folder permissions until it is
  turned on; then sharing is the authority for media (default deny, so public media needs an
  Anyone grant) ([media.md](media.md)).
- **Mapping onto Access.** One object: the authoritative mapper maps the permission asked to an
  access level (view → Viewer, comment → Commenter, create and update → Editor, delete and move →
  Manager, sharing and rules → Drive Admin) and returns `Granted` when the caller's winning entry
  reaches that level; blocks, tiers and depth stay inside sharing. Principals: business roles and
  the acting organization come from `ICallerContextContributor`s and the derived-role registry;
  sharing reads them off the caller. Feature off: none of it is registered, and each kind falls
  back to its module's own permissions (media: `ViewMediaContent`, `ManageMediaContent`, …;
  content: the per-type permissions).
- Tests: `test/Crest.Tests/Sharing` (the worked example line by line and each resolution
  requirement in `SharingResolverTests`; `SharingServiceTests`; `ShareAccessIndexTests`;
  `ShareListScopeTests` on SQLite, every example caller's list through each compiler equal to
  what the resolver allows, around the time boundaries; `MediaShareableKindTests`,
  `ContentShareableKindTests`, `SharingPhaseTwoExtensionsTests`) and
  `test/Crest.Tests/Queries/ScopeSemiJoinTests.cs`.

## 5. Business roles

The provider interface, per-request derived roles, invalidation on relationship change, the
party-role index and provider.

39. Business relationships are roles: a user linked to a customer, vendor or lead party, or a
    member of an organization, holds the matching role (Customer, Vendor, Lead, Member).
40. Each business role is a real role: it has a stable identity, appears in sharing pickers and
    permission screens, and is stored in the access index like any role.
41. Membership is never stored on the user. It is computed from the relationship records when a
    caller's principals are resolved (requirement 76). The relationship records are the only
    source of truth, so there is nothing to keep in sync.
42. Membership follows the organization the person is acting for: a member of two organizations
    holds each organization's business roles only while acting for it.
43. Changing a relationship invalidates the affected user's cached caller, so their principals
    are recomputed on their next request and a removed role stops counting immediately.
48. Derived roles are computed by providers behind a Crest interface: given a user and the
    organization they are acting for, a provider answers which business roles they hold. Party
    relationships are one provider, so the file system never needs to know what a party is.

Implementation:

- **Role registry** (`Crest.Roles.Abstractions/DerivedRoles.cs`,
  `Crest.Roles/Services/RoleRegistry.cs`): `RoleKind` (Assigned, Derived),
  `IDerivedRoleProvider` (its `Roles`, and `GetRolesAsync(DerivedRoleContext)`: the user and the
  organization they act for, or every organization for the user editor), `IRoleRegistry` (every
  role with its kind; the derived roles a user holds; `EnsureDerivedRolesAsync`). A role is
  Derived when an enabled provider declares it; the kind is not stored. Each derived role is
  stored as a real role with no permissions when a feature is enabled
  (`DerivedRolesFeatureEvents`), so it takes the permissions the tenant gives it (40). A
  provider grants only the roles it declares. A stored role of the same name as a party role
  becomes derived; users who hold it as stored keep it until it is removed.
- **Assignment refused** (45): `IRoleService.IsDerivedRoleAsync`; `GetAssignableRolesAsync`
  leaves derived roles out (the user editor, role filters, `ManageUsersInRole_*`);
  `UserStore.AddToRoleAsync` (every path through the user manager) and the `Users` recipe step
  throw `DerivedRoleAssignmentException`; a member binding naming one is refused.
- **Per request** (41-42): the caller factory adds the derived roles of the organization the
  caller acts for, after the contributors (on the member side they replace the roles with the
  binding's), with their roles' permissions. Grants for another organization never count. Roles
  are added per request rather than as sign-in claims, since claims are added only at sign-in.
- **Invalidation** (43): a relationship change bumps the tenant's permission version. Parties
  bumps it when a party-role item or a person changes (`PartyRoleChangeHandler`); Members bumps
  it on binding changes.
- **Party relationships** (48, `Crest.Parties/Server/BusinessRoles`, `PartyDerivedRoleProvider`):
  every party role a module composes (Customer, Vendor, Lead, ...) is a derived role named after
  its content type. An organization role counts only through the organization the caller acts
  for; a person role only through the person ([parties.md](parties.md) › Core party records).
  The party-role index is the content picker field index over `Party`. Organizations are also
  offered to sharing pickers (`PartyOrganizationDirectory`).
- **Member** is the member role template every binding holds
  ([parties.md](parties.md) › Members), held only while acting for the organization, not a
  derived role (making it derived would refuse it on bindings).

## 6. Content items

Content types and items on the same model; the content items API's listing, creation and view
checks.

73. Content items use the same principals, levels, entries and rules. Their chain is tenant →
    content type → item: a content type carries entries (set in its settings and in recipes),
    and an item adds its own.
74. A content item that belongs in a folder, such as a document page in a client folder, follows
    that folder's chain instead, with its content type as its type for rules.

Implementation (`Crest.Contents/Sharing`, through `ContentSharingStartup`,
`[RequireFeatures("Crest.Sharing")]`):

- Two kinds: `contenttype` (a type is a root; its entries flow to its items; a content
  permission asked against a type name or a `ContentTypeProbe` is about the type, so creating an
  item takes Editor on its type) and `content` (parent: its folder while filed, else its type;
  owner: the item's owner; fact `contenttype`; scope target `ContentItemIndex.ContentItemId`).
  An item not saved yet has no owner, so the creator's ownership does not authorise creating it.
- Levels (`ContentShareLevels`): View → Viewer; Preview and Edit → Editor; **Publish →
  Manager**, because the platform ranks publishing above editing (`PublishContent` implies
  `EditContent`) and publishing is what shows an item to its Viewers and the public, so writers
  and publishers stay distinct; Delete → Manager. Each permission's own variant and per-type
  variants (`View_{Type}`, `EditOwn_{Type}`) take the same level. Clone, List and
  EditContentOwner are not mapped: role permissions decide them.
- Recalculation (`ContentSharingHandler`): on create, update (owner changes), publish,
  unpublish and import; removal drops the item's rows when no version is left. A type created,
  changed or removed recalculates the type (and its items on a change).
- **Lists**: the content scope and sharing's are AND-ed for `ContentItemIndex`, so with sharing
  on `ContentItemScopeProvider` keeps only the version rule, decided by sharing
  (`IShareScopes`): every item's published version, and the latest version of items where the
  caller is at least an Editor. Which items is sharing's own provider's to say (Viewer and up).
  The organization provider still ANDs on top: it is separate policy. Feature off, the content
  provider is as without sharing. In-memory scope evaluations of content are in the query,
  because a sharing scope holds semi-joins (`SystemQuerySource`, `ContentItemOptionSourceProvider`).
- **Filed items** (74): `ContentShareFolderPart` on the item names its folder (any kind),
  `ContentShareFolderIndex` lists what a folder holds, `ContentFolderChildSource` puts filed
  items below the folder, and `IContentShareFolders.MoveAsync` files or unfiles an item as a
  move (70).
- Type entries in recipes: the `Sharing` recipe step (any kind, the tenant included) replaces an
  object's entries and recalculates what they reach. In the UI they are the type's Share panel.

## 8. Interface (sharing)

96. Every object has a Share panel for its drive's Drive Admins: who has access and why, grants
    and blocks to add, change or remove, and the public link when it is public.
97. Each folder, drive and the tenant has a Rules screen, for the drive's Drive Admins or, at the
    tenant, for holders of the tenant permission (85): rules in run order as
    "conditions → effect", reorderable, filterable and editable, scoped as in 67.
98. Lists show an access indicator (private, shared, public) and filter by it.
46. Sharing pickers offer both role kinds, with derived roles labelled.
47. The user editor shows a user's derived roles read-only beneath their assigned ones, each
    with the relationship that grants it ("Customer, member of Acme Corp"), linking to that
    relationship, since changing it is the only way to change the role.

Implementation:

- API `api/sharing` (`Crest.Sharing/Controllers/SharingController.cs`), objects by `kind` and
  `id` in the query (the tenant: `kind=tenant`): `panel` (entries, every decision on the chain
  with the winner per principal, the public link through the kind's optional `IShareableLinks`,
  whether lists are still catching up: `ShareAccessIndexer.VerifyAsync` finds a difference,
  requirement 80), `entries` and `entries/remove`, `rules` (the levels from the tenant down to
  the object, only its own editable, 67), `indicators`, `principals`
  (`ISharePrincipalDirectory`: Anyone, the tenant, roles of both kinds with derived ones
  labelled, users for staff callers, organizations from Parties; never system accounts),
  `roles`, `roles/users/{id}` (a user's derived roles with their relationships, for those who
  may list users or the user themselves), and `comments`.
- Components (`Crest.Components/Components/Sharing`): `CrestSharePanel` (96),
  `CrestShareRules` (97), `CrestAccessIndicator` (98), `CrestPrincipalPicker` (46),
  `CrestDerivedRoles` (47).
- Where they show: the drive browser has a Share action per item and for the open folder
  (Share panel, and a Rules tab for folders and drives), an indicator per item, and an access
  filter applied in the query (`api/files/{id}/children?access=`, through
  `IShareScopes.IndicatorRuleFor`); the admin page `/Sharing` is the tenant's entries and rules
  (menu Access Control › Sharing, `ManageTenantSharing`) and `/Sharing/{kind}/{id}` any object's
  (a content type's: `/Sharing/contenttype/Article`); the user editor lists derived roles
  read-only beneath the assigned ones, each linking to its relationship.
- `IShareIndicators` answers only for objects the caller may view.

## Platform audit

How the platform's mechanisms relate to sharing.

- **Per-object checks go through Access**, not an `IAuthorizationHandler` of sharing's own
  ([Mapping onto Access](#implementation)).
- **Default view grants: neutralised** when sharing is on (requirement 84).
- **Per-type permissions: reused.** Securable content types get `View_{Type}`-style
  permissions; the per-type entries (requirement 73) build on the type, not on those role
  permissions.
- **Filtered lists: built.** The platform filters content lists in SQL by content type and owner
  only. YesSql joins indexes that come from the same document, so each object's resolved access
  is stored on its own document and mapped to an access index; list queries semi-join it,
  keeping filtering in the database.
- **Search: to build.** The platform's search (Lucene, Elasticsearch, Azure AI Search) checks
  only which index the caller may query, never each result (F5, below).
- **Roles and sessions.** The platform's roles are names plus permission claims with no role
  kind, so Crest keeps its own role registry (44). Derived business roles are added to the
  caller per request; the security stamp still signs users out where needed.
- **Storage engine.** YesSql over a relational database (SQLite, PostgreSQL, SQL Server, MySQL);
  no document database.

## Backlog

- [ ] The content type settings editor links to the type's Share panel.
- [ ] Members get user search in the principal picker.
- [ ] A screen for comments (they have an API only).
- [ ] Media's delete and move checks ask about content items filed in the folder (today such an
  item falls back to its content type's chain).
- [ ] A change on a content item recalculates the files attached to it (today they wait for
  their next recalculation).
- [ ] Run the sharing test suites (written, not yet run).

## Open questions

- **Search filtering (F5).** Search engines are connections, and an engine indexing Crest data
  is set up with the permission system, so access is written into what is indexed and every
  search is filtered; each principal or rule change reindexes the affected documents. Open: (1)
  one search path (the Search module through the query pipeline) with the caller's scope always
  applied, direct index access forbidden; or (2) coarse filtering in the index plus an exact
  check afterwards, which breaks exact counts. Recommendation: 1.
