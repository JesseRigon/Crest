# Sharing: who may reach an item, on any kind of item

Status: ruled 2026-10-11; phases 1 (the engine and its registration with Access) and 2 (the
access index and list scopes) built; phases 3 (business roles) and 4 (the interface), system
accounts, deleting and moving, comments and filed items built 2026-10-13 (compiled, tests written
and not run). Search filtering (F5) is still open. Sharing is grants and blocks on objects that flow down a tree, as in Google Drive,
plus ordered rules. It was designed for files ([media.md](media.md)) and is now its own module
so any module's items can be shared.

## Ruling (2026-10-11)

- **Sharing is its own module**: `Crest.Sharing` and `Crest.Sharing.Abstractions`, a tenant
  feature that can be turned off. Not `Crest.Access.Sharing`: it is a domain with its own data
  (entries, rules, the access index, the time registry, the Share panel), and it registers
  with the access gate like every other domain ([access.md](access.md) › Rulings › Access is
  extended, never rewritten).
- **Any module makes its items shareable** through the abstractions: it registers a
  **shareable kind** - how to recognise one of its items, its id, owner, parent (the chain
  grants flow down), the facts rules test, which of its permissions map to which access level,
  and whether it is inactive. Media registers drives, folders and files; Contents registers
  content items (chain tenant → type → item, or the folder chain); Parties, Members and later
  modules opt in. None of them decides anything about sharing.
- **Sharing registers with Access once**, for every kind: one authoritative mapper
  (`IAuthoritativeResourcePermissionMapper`, the object decision) and `IScopeProvider`s over the
  access index (lists and search, phase 2). An inactive container is denied by the mapper, not
  by a ceiling: a ceiling would also deny tenant administrators, who must still reach it.
- **Precedence**: for a kind registered as shareable, while the feature is on, the sharing
  mapper is **authoritative** - default deny, the owner always allowed, type-level entries in
  place of per-type role permissions - and the kind's own mapper is not consulted. Tenant
  administrators pass at the decision's super-user step, before any mapper (the standing
  rule). With the feature off, or for a kind not registered, the module's own mapper decides.
  Nothing depends on registration order: priority decides ([access.md](access.md) › Mapper
  roles and priority).
- **Extending or replacing sharing** (ruled 2026-10-11). A module outside Crest can:
  - **add** sharing for items by contributing entries and rules through an
    `IShareEntrySource`: the engine resolves them with the stored ones under the same
    precedence (principal tier, depth, order, blocks, Anyone only Viewer, Admin only on a root),
    so a contribution wins only where sharing's own rules let it - it never grants past a
    deeper block or a more specific principal's decision. At one depth contributions follow the
    stored sharing, in feature-priority order; the explanation names their source. A
    contribution is not an independent grant: a provider that could grant past sharing would
    be the authority, and is shipped as one instead;
  - **replace** sharing for specific items with an authoritative mapper of higher feature
    priority;
  - **replace a kind** by registering one with the same name at a higher priority
    (`ShareableKinds` picks one per name; two at one priority conflict and allow nothing).
  Since phase 2 "shared with me" reads the access index, so contributed grants on an object
  appear in it like stored ones.
- **Content-item sharing** lives in neither Media nor Contents: Contents registers its kind.

## Module

`Crest.Sharing.Abstractions`:
- the model: `SharePrincipal` (Anyone, Tenant, Organization, Role, User), `AccessLevel`
  (Viewer, Commenter, Editor, Manager, Admin - the generic name for Drive Admin, granted only
  on a root object), `ShareEntry` (a grant or a block, optionally time-bound), `ShareRule`
  (ordered conditions → entries);
- `IShareableKind` and `ShareableObject`: what a module registers;
- `ISharingService`: grant, block, remove, set rules, list entries, "shared with me", explain;
- `ISharingResolver`: the effective level for a caller on an object;
- `IShareChangeChecks` (deleting and moving, 69-70), `IShareComments` and `IShareCommentStore`
  (71-72), `IShareableChildSource` (what another module files in a folder, 74),
  `IShareIndicators` (98), `ISharePrincipalDirectory` and `IShareableLinks` (the interface).

`Crest.Sharing`:
- the share documents (one per object with entries or rules) and their index;
- the resolver (requirements 58-65), reading time bounds exactly at every decision;
- the mapper, the permissions (`ManageTenantSharing`, `ShareWithAnyone`, both
  security-critical), the service with its change checks (85) and audit (99).

## Phases

1. **Engine** (built 2026-10-11, compiled, tests written and not run): abstractions
   (`src/Crest.Sharing.Abstractions`), the module (`src/Crest.Sharing`: `ShareDocument` with its
   two indexes and migration, `SharingResolver`, `SharingPermissionMapper`, `SharingService`,
   permissions), and in Access the `IAuthoritativeResourcePermissionMapper` contract the decision
   asks first. Tests: `test/Crest.Tests/Sharing` - the worked example line by line and each
   resolution requirement (`SharingResolverTests`), changes, refusals, audit, "shared with me" and
   the mapper through the decision (`SharingServiceTests`). **No kind is switched on**, so
   nothing changes for any item yet; the feature is not in the setup recipe until phase 2 gives
   it something to do.
   Interpretations made while building, open to review: the owner of an object holds Manager on
   it (Admin when it is a root), and the owner of a root administers its whole tree; at one depth
   an object's own entries come after the rules defined on it, so an entry overrides a rule about
   the same principal; "shared with me" counts explicit grants to the user, their roles and
   their organization, not tenant-wide or public ones.
   **Media is registered** (2026-10-11): `Crest.Media/Sharing/MediaShareableKind.cs`, through
   `MediaSharingStartup` (`[RequireFeatures("Crest.Sharing")]`). Until file objects exist, a media
   object is a path: the library root ("") is the one root, folders and files inherit down the
   path, rules test the extension, viewing is Viewer and managing is Manager. Sharing is off by
   default, so media keeps its folder permissions; turning it on makes sharing the authority for
   media paths - default deny, so public media needs an Anyone grant - with two known gaps until
   phase 2: the media library's listings are not yet filtered by sharing, and media served
   without Secure Media is not checked at all (decision F2 in [media.md](media.md)). Tests:
   `test/Crest.Tests/Sharing/MediaShareableKindTests.cs`.
2. **Lists and search** (built 2026-10-11, compiled, tests written and not run). Decision F3
   is taken below.
   - **Scope model** (`Crest.Access.Abstractions/Scope.cs`): `ScopeCondition` gains the
     comparisons (`<`, `<=`, `>`, `>=`), `IsNull`, and semi-joins (`InSelect`, `NotInSelect`:
     a column in a set a `ScopeSelect` selects from another index table under its own
     `ScopeFilter`). All three compilers compile them with the tenant prefix, dialect quoting,
     an alias per selected table and bound values: `ScopeExpressions` (YesSql's
     `IsIn<TIndex>`/`IsNotIn<TIndex>`, the index type found by table name),
     `StructuredQueryCompiler`, and the SQL rewriter's scope predicate. In memory a semi-join
     cannot be evaluated: `ScopeFilter.Matches` throws `NotSupportedException` for a filter
     holding one (`HasSelect` lets a source refuse up front). `IScopeProviderSource` lets a
     domain register providers whose targets are known only at run time.
   - **Contracts a kind implements** (`Crest.Sharing.Abstractions/Lists.cs`), both optional:
     `IShareableTree` (`ChildrenAsync`: what a change recalculates below an object) and
     `IShareableScopeTarget` (`ShareScopeTarget`: the index table its objects are rows of and
     the object-id column). They are separate interfaces rather than members of
     `IShareableKind`, so phase 1 kinds compile unchanged and "has no tree" is distinguishable
     from "has no children". `IShareAccessIndex` is what a module calls when something other
     than sharing changes an object's access: created, moved, deleted, or a fact a rule reads
     (requirement 78). An object never calculated is in no list (82).
   - **Access index** (`Crest.Sharing`: `ShareAccessDocument` with `ShareAccessIndex`,
     `ShareRoleShadowIndex`, `ShareAccessObjectIndex`; migration 2; `ShareAccessIndexer`). Per
     object, per principal with a decision: the principal's winning decision after depth and
     order (its kind is the tier), effect, level, depth, and its window. Each principal gets a
     row per window between the chain's start and end times, so a read at any instant picks the
     right row: when a deeper time-bound block ends, the shallower decision's row starts at that
     instant, without waiting for the job. Owners get an owner row; an inactive object gets
     none. Within the role tier a caller's roles disagree by depth (requirement 60), which a
     per-principal row cannot settle alone: a role denial deeper than another role's grant is
     stored as a shadow row, and the list scope drops a role grant a shadow of the caller's
     roles covers. The public flag is the Anyone row (and `IsPublic` on the document).
   - **Recalculation**: a change to entries or rules recalculates the object and everything
     below it. A change that may remove access (removing a grant, a block, a shorter grant, any
     rule change) is applied before the call returns, in batches; one that only adds access is
     applied at once up to 200 objects and otherwise queued (`ShareRecalculation`) for the job.
     Tenant-level changes recalculate every indexed object.
   - **Time**: each document registers its next boundary (`ShareAccessObjectIndex.NextBoundaryUtc`,
     indexed: the time registry); `ShareAccessBackgroundTask` runs every minute per tenant, reads
     only the rows due and recalculates them (dropping passed windows), then the queued
     subtrees. Reads compare windows with now, so access starts and ends on time between runs.
   - **List scopes** (`SharingScopeProvider`, through `SharingScopeProviderSource`), one per
     kind with a scope target: visible iff public, or the user row grants, or (no user row) a
     role row grants with no deeper role denial, or (no role row) the organization grants, or
     (none of those) the tenant grants; rows active now only; the tenant administrator sees
     everything. Phase 1's mapper still decides single objects.
   - **Consistency** (81): the index is calculated from the resolver's own chain and decisions
     (`SharingResolver.TimedDecisionsAsync`, `OwnerLevels`); `ShareAccessIndexer.VerifyAsync`
     recalculates an object and lists any difference from its stored rows. "Shared with me"
     reads the index (rows decided on the object itself), so contributed entries count.
   - Tests: `test/Crest.Tests/Queries/ScopeSemiJoinTests.cs` (each compiler, SQLite and SQL
     Server text, refusal in memory), `test/Crest.Tests/Sharing/ShareAccessIndexTests.cs` (the
     example's rows, owners, windows and the registry, recalculation on change, removals before
     returning, queued additions, "shared with me"), `test/Crest.Tests/Sharing/ShareListScopeTests.cs`
     (on SQLite, every example caller's list through each compiler equals what the resolver
     allows, at instants around the time boundaries, before and after the job; verification).
   - **Content items registered** (2026-10-11, compiled, tests written and not run), in
     `Crest.Contents/Sharing` through `ContentSharingStartup` (`[RequireFeatures("Crest.Sharing")]`):
     - Two kinds: `contenttype` (a type is a root; its entries flow to its items; a content
       permission asked against a type name or a `ContentTypeProbe` is about the type, so
       creating an item takes Editor on its type) and `content` (parent: its type; owner: the
       item's owner; fact `contenttype`; scope target `ContentItemIndex.ContentItemId`). An item
       not saved yet has no owner, so the creator's ownership does not authorise creating it.
     - Levels (`ContentShareLevels`): View → Viewer; Preview and Edit → Editor; **Publish →
       Manager**, because the platform already ranks publishing above editing (`PublishContent`
       implies `EditContent`) and publishing is what shows an item to its Viewers and the
       public, so writers and publishers stay distinct; Delete → Manager. Each permission's own
       variant and per-type variants (`View_{Type}`, `EditOwn_{Type}`) take the same level.
       Clone, List and EditContentOwner are not mapped: role permissions decide them.
     - Recalculation (`ContentSharingHandler`): on create, update (owner changes), publish,
       unpublish and import; removal drops the item's rows when no version is left. A type
       created, changed or removed recalculates the type (and its items on a change).
     - **Lists**: the content scope and sharing's are AND-ed for `ContentItemIndex`, so with
       sharing on `ContentItemScopeProvider` yields instead of narrowing by role permissions:
       it keeps only the version rule, decided by sharing (`IShareScopes`): every item's
       published version, and the latest version of items where the caller is at least an
       Editor. Which items is sharing's own provider's to say (Viewer and up). Crest.Server's
       organization and assignment providers still AND on top: they are separate policy.
       Feature off, both content providers are as before. Two in-memory scope evaluations of
       content were moved into the query, because a sharing scope holds semi-joins:
       `SystemQuerySource` (one item) and `ContentItemOptionSourceProvider` (resolving ids).
     - **Requirement 84** (`DefaultViewGrantsRemover`): when Sharing is enabled, the Anonymous
       and Authenticated roles lose `ViewContent`, `ViewOwnContent`, `View_{Type}` and
       `ViewOwn_{Type}`. Effect: public content needs an Anyone grant (on the type or the item),
       signed-in visitors a Tenant grant; checks asked without an item no longer pass for
       visitors. Turning sharing off does not restore the grants. A recipe that enables sharing
       and then grants these by a Roles step puts them back: shipped recipes must not.
     - Type entries in recipes: the `Sharing` recipe step (any kind, the tenant included)
       replaces an object's entries and recalculates what they reach. Editing them in type
       settings is the Share panel (phase 4).
     - Requirement 74 (an item in a folder follows the folder's chain): built 2026-10-13
       (§ 6).
   - **Rebuild**: kinds may list their roots (`IShareableRoots`); enabling Sharing queues a
     rebuild (`IShareAccessIndex.QueueRebuildAsync`, also callable on demand), which the job
     turns into one queued subtree per root and runs a few per pass.
   - **Admin assignment by a tenant permission** (media requirement 31): a kind implementing
     `IShareableAdminAssignment` names a tenant permission whose holders may add and remove
     Admin grants on the kind's roots without being Admin there, and see only those grants;
     every other entry, and Admin below a root, still takes the tree's Admin. Each change is
     audited with "(through <permission>)".
   - **Organization-scoped roles**: `SharePrincipal` gains `OrganizationId`, for roles only
     (`SharePrincipal.Role("MemberAdmin", "acme")`, `role:MemberAdmin@acme` in recipes). It is
     in the Role tier; a caller matches it when they hold the role and act for that
     organization (requirement 38). The index stores it as `role@org:id`.
   - The list scope takes a level (`Build(caller, minimum)`): role shadows carry the deeper
     row's level, so a deeper Viewer row caps a shallower Editor grant within the role tier.
     The mapper picks, among kinds that recognise a resource, the one that maps the permission
     (a string is a media path to Media and a type name to Contents).
   - Tests added: `test/Crest.Tests/Sharing/ContentShareableKindTests.cs` and
     `SharingPhaseTwoExtensionsTests.cs`, plus level and organization-role cases in
     `ShareListScopeTests.cs`.
   - **Left for later**: the type-settings editor's link to the Share panel. (The pending
     indicator and requirement 74 landed with phase 4.) (Media registered its placements' scope target, tree, roots and admin
     assignment on 2026-10-11.)
   - **Media's edits** (2026-10-11, for media requirement 19):
     - `IShareableAdminAssignment.AppliesTo(root)` (a default method, every root): the
       assignment permission applies per root, so Media limits it to shared drives.
     - New optional `IShareableAdminEligibility.MayHoldAdmin(root, principal)`: `SharingService`
       refuses an Admin grant to a principal the kind rules out, whoever asks.
       `SharingResolver` counts such a grant, stored or contributed, as Manager. Media uses it
       so that only a personal drive's owner holds Admin there.
3. **Principals** (built 2026-10-13, compiled, tests written and not run): business roles and the
   role registry (§ 5, requirements 39-48).
   - **Role registry** (`Crest.Roles.Abstractions/DerivedRoles.cs`, `Crest.Roles/Services/RoleRegistry.cs`):
     `RoleKind` (Assigned, Derived), `IDerivedRoleProvider` (its `Roles`, and
     `GetRolesAsync(DerivedRoleContext)`: the user and the organization they act for, or every
     organization for the user editor), `IRoleRegistry` (every role with its kind; the derived
     roles a user holds; `EnsureDerivedRolesAsync`). A role is Derived when an enabled provider
     declares it; the kind is not stored. Each derived role is stored as a real role with no
     permissions when a feature is enabled (`DerivedRolesFeatureEvents`), so it has a stable
     identity, shows in pickers and permission screens and takes the permissions the tenant
     gives it there (40). A provider grants only the roles it declares.
   - **Assignment refused** (45): `IRoleService.IsDerivedRoleAsync`;
     `GetAssignableRolesAsync` leaves derived roles out (the user editor, role filters,
     `ManageUsersInRole_*`); `UserStore.AddToRoleAsync` (every path through the user manager:
     the user editor, the users API, workflows) and the `Users` recipe step throw
     `DerivedRoleAssignmentException`; a member binding naming one is refused.
   - **Per request** (41-42): the caller factory adds the derived roles of the organization the
     caller acts for, after the contributors (on the member side they replace the roles with the
     binding's), with their roles' permissions. Grants for another organization never count.
   - **Invalidation** (43): a relationship change bumps the tenant's permission version, so every
     cached caller is rebuilt on its next request. Parties bumps it when a party-role item or a
     person changes (`PartyRoleChangeHandler`); Members already bumps it on binding changes.
   - **Party relationships** (48, `Crest.Parties/Server/BusinessRoles`): every party role a
     module composes (a party type other than Person and Organization: Customer, Vendor, Lead,
     ...) is a derived role named after its content type. A user holds it when a published role
     item's `Party` points at their person (always) or at the organization they act for (only
     then). The party-role index is the content picker field index over `Party`. Organizations
     are also offered to sharing pickers (`PartyOrganizationDirectory`).
   - Interpretation open to review: **Member** stays the member role template every binding
     holds by default ([parties.md](parties.md) › Members), held only while acting for the
     organization, rather than a derived role: making it derived would refuse it on bindings.
     A stored role of the same name as a party role becomes derived; users who hold it as
     stored keep it until it is removed.
4. **Interface** (built 2026-10-13, compiled, not run) (§ 8):
   - API `api/sharing` (`Crest.Sharing/Controllers/SharingController.cs`), objects by `kind` and
     `id` in the query (the tenant: `kind=tenant`): `panel` (entries, every decision on the
     chain with the winner per principal, the public link through the kind's optional
     `IShareableLinks`, whether lists are still catching up: `ShareAccessIndexer.VerifyAsync`
     finds a difference, requirement 80), `entries` and `entries/remove`, `rules` (the levels
     from the tenant down to the object, only its own editable, 67), `indicators`, `principals`
     (`ISharePrincipalDirectory`: Anyone, the tenant, roles of both kinds with derived ones
     labelled, users for staff callers, organizations from Parties; never system accounts),
     `roles`, `roles/users/{id}` (a user's derived roles with their relationships, for those who
     may list users or the user themselves), and `comments`.
   - Components (`Crest.Components/Components/Sharing`): `CrestSharePanel` (96),
     `CrestShareRules` (97: "conditions → effect" in run order, reorder, filter, edit),
     `CrestAccessIndicator` (98), `CrestPrincipalPicker` (46), `CrestDerivedRoles` (47).
   - Where they show: the drive browser has a Share action per item and for the open folder
     (Share panel, and a Rules tab for folders and drives), an indicator per item, and an access
     filter applied in the query (`api/files/{id}/children?access=`, through
     `IShareScopes.IndicatorRuleFor`); the admin page `/Sharing` is the tenant's entries and
     rules (menu Access Control › Sharing, `ManageTenantSharing`) and `/Sharing/{kind}/{id}` any
     object's (a content type's: `/Sharing/contenttype/Article`); the user editor lists derived
     roles read-only beneath the assigned ones, each linking to its relationship.
   - Indicators: `ShareAccessDocument.IsShared` (a grant to anyone but the owner, other than
     Anyone) beside `IsPublic`, on `ShareAccessObjectIndex` (migration 4, which queues a
     rebuild); `IShareIndicators` answers only for objects the caller may view.
   - Left: the type settings editor does not link to the type's Share panel yet; members get
     no user search in the picker; comments have an API and no screen.

## Terms

- **Shareable object**: an item a module registered a kind for (a drive, folder or file; a
  content item; a content type as a link in the chain), with a stable id, an owner and a
  parent. Its **chain** is its parents up to a root.
- **Principal**: Anyone (public), Tenant, Organization, Role or User.
- **Role**: any group the business recognises: staff roles (Editor, Sales Rep, Marketing) and
  business roles (Customer, Vendor, Lead, Member).
- **No-permission role (shim, 2026-10-05)**: a role that grants no permissions and exists only
  so people can be shared with as a set; replaced when grouping is decided
  ([parties.md](parties.md) › Decisions needed › Grouping).
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

The content sharing feature and its settings; system accounts (tenant and organization); the role registry with role kind, and assignment restricted to assigned roles; default Anyone and Tenant view grants removed when sharing is on.

- [ ] **Make content sharing a tenant feature.**
  1. Content sharing is a feature each tenant enables. Without it a tenant has no drives, sharing
     or rules, and its content items keep Orchard's standard permissions. Nothing about it
     crosses tenants.

- [x] **Add system accounts.** Built 2026-10-13. `Crest.Access.SystemAccounts`: the tenant
  account is `system:tenant`, an organization's `system:organization:{id}`. They are ids, not
  user records, so they cannot sign in or be impersonated, appear in no user list or picker and
  are never counted (34-35, by construction); no user may take a `system:` id (the user store
  and the `Users` recipe step refuse it). The system actors act as them
  (`CallerContext.SystemAccountId`, `ActorId`), and the access audit attributes their actions to
  the account (36). `ShareableObject.OwnerAccountId` names a system owner (Media: shared drives
  are the tenant's, organization drives the organization's); the organization system actor is
  its owner. Sharing refuses a system account as a principal.
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

- [x] **Add the role registry with role kind.** Built 2026-10-13 (Phases › 3).
  44. Each role has a **kind**, kept in Crest's role registry: **Assigned** (stored on users, the
      ordinary Orchard kind) or **Derived** (computed, with the provider that computes it).
      Orchard's role model has no field for this, and its built-in "system role" mechanism covers
      only its own fixed roles.
  45. Derived roles cannot be assigned. Every assignment path (the user editor, the users API,
      recipes, bulk assignment) offers Assigned roles only and refuses Derived ones on the server.
      Like Orchard's own assignable-role filter, it also leaves out system roles (Anyone,
      Authenticated).

- [x] **Remove default Anyone and Tenant view grants.** Built 2026-10-11 (Phases › 2).
  84. No recipe shipped with Crest or a host built on it grants anything to Anyone or Tenant
      unless it means to.

## 4. Access

Entries and rules, the resolution engine and its explanation, the access index and recalculation, the time registry and job, enforcement on every read path, list and search filtering.

- [ ] **Resolve principals.**
  37. Tenant means everyone signed in to that one tenant, staff and members, and never anyone
      from another tenant on the same instance.
  38. A member acts for one organization at a time, and only that organization's entries count
      for them.

- [ ] **Add sharing entries.**
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

- [ ] **Add rules.**
  54. A rule applies only to objects below the folder, drive or tenant it is defined on that
      match all its conditions.
  55. A rule belongs at the highest folder whose contents it should cover, so one rule replaces
      many individual shares.
  56. Conditions cover at least file type, extension, named group ("Documents", "Images"), content
      type, metadata and tags, and time (a window, a schedule, an age). Modules can add their own.
  57. A rule's effects are grants and blocks, the same as entries.

- [ ] **Build the resolution engine.**
  Precedence runs from general to specific on two axes:

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

- [ ] **Show the sharing to Drive Admins only.**
  66. Only a Drive Admin of an object's drive, or a tenant administrator, sees how it is shared: every entry, block and matching rule from
      the tenant down, in order, which decision won, and who has access as a result. Everyone
      else sees nothing of it. Changing a tenant-level step needs the tenant permission in 85.
  67. A folder's rules list shows rules from the tenant down to that folder, not below it.
  68. An object a person cannot view does not exist for them: it is in no listing, search, picker,
      count, explanation or error message.

- [x] **Check deleting and moving.** Built 2026-10-13: `IShareChangeChecks` for any kind
  (Manager over the object and everything below it, a kind's children and what is filed in it,
  each resolved live; moving also Editor on the destination; the refusal names and counts
  nothing). Media's file service makes the same checks over its placements; it does not yet
  ask about content items filed in a folder it deletes or moves (such an item falls back to its
  content type's chain).
  69. Deleting needs Manager. Deleting a folder needs Manager on it and everything below it, including objects
      the person cannot see. If any fail, nothing is deleted, and the refusal says the folder
      contains items they cannot delete, without naming or counting them.
  70. Moving changes the access of everything moved, so it needs Manager over everything moved,
      plus Editor on the destination, with the same refusal.

- [x] **Add comments.** Built 2026-10-13: `IShareComments` (a Viewer reads, a Commenter adds,
  the author or a Manager removes, audited); stored in the object's share document unless a
  registered `IShareCommentStore` (a file type's own format) or the kind itself handles it.
  71. A Commenter can add comments and do nothing else.
  72. Comments are stored in the object's metadata by default. A file type can keep them where
      that format keeps comments instead, through a per-type comment store.

- [ ] **Build the access index and enforcement.**
  75. Access is precomputed. For every object (every placement, for a hard-linked file), an index holds each principal's winning grant or
      block and its tier, and whether the object is public, after the resolution above. Every
      read path checks the index; nothing is resolved on access.
  76. A person's principals are resolved once per session or when they change, so a check is a
      lookup of their most specific principal with an entry on the object, or of the public flag.
  77. Every read path enforces access: object endpoints, folder listings, search, pickers,
      Orchard's display routes and file bytes. Listings and search filter in the database query,
      never by dropping rows after paging.
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

- [ ] **Gate changes to sharing and rules.**
  85. Changing sharing or rules inside a drive needs Drive Admin of that drive. Tenant-level rules and
      entries, which reach every drive, need a dedicated tenant permission. Granting Anyone also
      needs a dedicated permission, so not every Drive Admin can make content public.

- [ ] **Audit sharing and rule changes.**
  99. Every sharing and rule change is recorded in the audit trail ([audit.md](audit.md)) with who
      made it, including while impersonating.

## 5. Business roles

The provider interface, per-request derived role claims, session invalidation on relationship change, the party-role index and provider.

- [x] **Compute business roles.** Built 2026-10-13 (Phases › 3).
  39. Business relationships are roles: a user linked to a customer, vendor or lead party, or a
      member of an organization, holds the matching role (Customer, Vendor, Lead, Member).
  40. Each business role is a real Orchard role: it has a stable identity, appears in sharing
      pickers and permission screens, and is stored in the access index like any role.
  41. Membership is never stored on the user. It is computed from the relationship records when a
      session's principals are resolved (requirement 76) and attached to the session as role
      claims, which Orchard's permission checks already read. The relationship records are the
      only source of truth, so there is nothing to keep in sync.
  42. Membership follows the organization the person is acting for: a member of two organizations
      holds each organization's business roles only while acting for it.
  43. Changing a relationship invalidates the affected user's sessions, so their principals are
      recomputed on their next request and a removed role stops counting immediately.

- [x] **Add derived role providers.** Built 2026-10-13 (Phases › 3).
  48. Derived roles are computed by providers behind a Crest interface: given a user and the
      organization they are acting for, a provider answers which business roles they hold.
      Party relationships are one provider, so the file system never needs to know what a party
      is.

## 6. Content items

Content types and items on the same model; the content items API's listing, creation and view checks.

- [x] **Put content items on the same model.** Built 2026-10-11 (Phases › 2); 74 built
  2026-10-13: `ContentShareFolderPart` on the item names its folder (any kind),
  `ContentShareFolderIndex` lists what a folder holds (its own migration), the content kind
  takes the folder as parent while it exists (else the type) and keeps `contenttype` for rules,
  `ContentFolderChildSource` puts filed items below the folder, and
  `IContentShareFolders.MoveAsync` files or unfiles an item as a move (70).
  73. Content items use the same principals, levels, entries and rules. Their chain is tenant →
      content type → item: a content type carries entries (set in its settings and in recipes),
      and an item adds its own.
  74. A content item that belongs in a folder, such as a document page in a client folder, follows
      that folder's chain instead, with its content type as its type for rules.

## 8. Interface (sharing)

- [x] **Build the Share panel.** Built 2026-10-13 (Phases › 4).
  96. Every object has a Share panel for its drive's Drive Admins: who has access and why, grants and blocks
      to add, change or remove, and the public link when it is public.

- [x] **Build the Rules screens.** Built 2026-10-13 (Phases › 4).
  97. Each folder, drive and the tenant has a Rules screen, for the drive's Drive Admins or, at the
      tenant, for holders of the tenant permission (85): rules in run order as
      "conditions → effect", reorderable, filterable and editable, scoped as in 67.

- [x] **Show access indicators.** Built 2026-10-13 (Phases › 4).
  98. Lists show an access indicator (private, shared, public) and filter by it.

- [x] **Show derived roles in pickers and the user editor.** Built 2026-10-13 (Phases › 4).
  46. Sharing pickers offer both kinds, with derived roles labelled.
  47. The user editor shows a user's derived roles read-only beneath their assigned ones, each
      with the relationship that grants it ("Customer, member of Acme Corp"), linking to that
      relationship, since changing it is the only way to change the role.

## Platform audit

### Authorization and data

- **Per-object checks: through Access** (ruling 2026-10-11, replacing "reuse the
  `IAuthorizationHandler` hook"). Sharing's logic lives in `Crest.Sharing` and
  `Crest.Sharing.Abstractions`, and registers with the access system; the gate does all
  permission processing ([access.md](access.md) › Rulings › Access is extended, never
  rewritten). How sharing maps onto the access contracts:
  - **One object**: one `IResourcePermissionMapper` for every shareable kind answers from the access
    index - the permission asked maps to an access level (view → Viewer, comment →
    Commenter, create and update → Editor, delete and move → Manager, sharing and rules →
    Drive Admin), and the mapper returns `Granted` when the caller's winning entry reaches that
    level and an empty list otherwise. Blocks, tiers and depth are resolved inside sharing and
    never leak into the decision. The tenant administrator never reaches the mapper (the
    decision's super-user step), which is the standing rule.
  - **Lists and search**: an `IScopeProvider` per access index table, so listings, pickers and
    search filter in the query (requirement 77).
  - **Principals**: business roles and the acting organization come from
    `ICallerContextContributor`s (Members, Parties); sharing reads them off the caller.
  - **Hard limits**: an inactive drive (requirement 23) is denied by the mapper, so tenant
    administrators (who pass before any mapper) still reach it.
  - **Feature off**: none of it is registered, and each kind falls back to its module's
    own permissions (media: `ViewMediaContent`, `ManageMediaContent`, …; content: the
    per-type permissions).
- **Default view grants: neutralise.** Orchard gives Anonymous and Authenticated `ViewContent`
  by default, and Crest's setup recipe grants Anonymous `ViewContent` again. With content
  sharing on, that would make every published item public; the feature removes those grants
  (requirement 84).
- **Per-type permissions: reuse.** Securable content types get `View_{Type}`-style permissions;
  the per-type entries (requirement 73) build on the type, not on those role permissions.
- **Filtered lists: build.** Orchard filters content lists in SQL by content type and owner
  only. YesSql joins indexes that come from the same document, so each object's resolved access
  is stored on its own document and mapped to an access index (one row per principal with a
  decision); list queries join it, keeping filtering in the database. Recalculating an object
  re-saves its document.
- **Search: build.** Orchard search (Lucene, Elasticsearch, Azure AI Search) checks only which
  index the caller may query, never each result. Crest writes access into its index documents
  and filters at query time.
- **Roles and sessions: reuse, with care.** Roles are names plus permission claims; there is
  no role kind, so Crest keeps its own role registry (requirement 44). Orchard adds claims only
  at sign-in (`IUserClaimsProvider`) and keeps old claims when a session refreshes, so derived
  business roles are added per request instead, where Crest.Members already enriches the
  session (`MemberCookieEventsConfiguration`). The security stamp still signs users out where
  needed.
- **Storage engine.** YesSql over a relational database (SQLite, PostgreSQL, SQL Server, MySQL);
  no document database.

## Decisions needed

- [x] **Access-index size and recalculation (F3).** Access is precomputed per placement and
  principal; rules read metadata and time, a rule high in the tree recalculates its whole
  scope, hard-linked folders multiply inherited chains, and removing access must take effect
  before it is reported done. Options: (1) store only explicit and rule decisions and
  resolve inheritance through an ancestor closure (a join, not a row per inherited pair);
  (2) recalculate in batches in the background with a deny-first fence so removals apply
  at once; (3) hard links for files only in the first version. Recommendation: 1 and 2
  together, with folders allowed to hard-link once 1 exists.
  **Taken 2026-10-11 (phase 2): 2 and 3, not 1.** One document per object (per placement)
  holds each principal's winning decision, inherited ones included, with its window; lists
  semi-join it. Option 1 was set aside because the winner within a tier depends on depth and
  order across the whole chain, and rules match per object, so an ancestor closure would move
  the resolution into every list query (aggregates and correlated subqueries the scope model
  and three compilers would all need) and could not agree with the resolver by construction.
  The cost is rows per inherited pair and recalculating subtrees, bounded by option 2: a
  change that may remove access is applied before returning, in batches; one that only adds
  access is applied at once up to 200 objects and otherwise by the job. Hard links stay
  file-only (3) until subtree sizes are measured.
  **Multi-path inheritance (built 2026-10-13, compiled, tests not run).** Folders are
  hard-linked since ruling F4, so a child of a hard-linked folder is reached through every
  placement of the folder. A kind names the extra parents in `ShareableObject.OtherParents`
  (Media: the folder's other hard placements, and an attached-field folder's content item).
  `SharingResolver.ChainsAsync` walks every path (at most 16, the primary path first);
  `ResolveAsync` resolves each and gives the most any path not through an inactive drive gives.
  The access index keeps one document per path (`PathKey`, empty for the primary path;
  `AccessKey` = object id, or `{id}~{path key}`), and the list scope runs the tier logic per
  path over `ShareAccessObjectIndex.AccessKey` and admits an object when any path admits the
  caller. Migration 3 adds the columns and fills the existing rows' keys. A change on a path
  other than the primary one reaches the index through the kind's children (Media lists a
  folder's children through every placement); a change on a content item does not recalculate
  the files attached to it until their next recalculation.
- [ ] **Search filtering (F5).** Ruled: search engines are connections, and an engine
  indexing Crest data is set up with the permission system, so access is written into what
  is indexed and every search is filtered; each principal or rule change reindexes the
  affected documents. Options: (1) one search path (the Search module through the query
  pipeline) with the caller's scope always applied, direct index access forbidden; (2)
  coarse filtering in the index plus an exact check afterwards, which breaks exact counts.
  Recommendation: 1.
