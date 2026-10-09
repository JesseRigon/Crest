# Media: files, drives and access control

Status: requirements agreed and audited, not started. Next: phase 1. This plan covers Crest's
file system, drives and access control, in build order; nothing of it is built yet, and the
Crest APIs it replaces are listed under "Crest today" below. The phases are the build order.
Each phase ends with the full suite green and its own checks.

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

## Goal

Crest gets a file system with sharing modelled on Google Drive's behaviour, in its own
implementation and under its own names, and applied the way a Unix file system applies
permissions: every file, folder and drive is a file object, and access is overlaid on it.

Access has two layers: **sharing**, grants and blocks on objects that flow down the tree as in
Drive, and **rules**, ordered "conditions → effect" lists that Drive does not have, so "every
PDF here is public, but not the Word files they come from" is one rule. Conflicts are allowed;
unexplained access is not.

Publishing is separate. Published says which version readers get; access says who the readers
are. Content items use the same model, with their content type in the chain.

100. All of this is Crest: open source, with no dependency on any downstream module. Downstream
    modules build their publishable links on it, and party relationships supply business roles
    through a seam Crest defines.

## Terms

- **File object**: a drive, folder or file, with a stable id, kind, name, parent, owner,
  metadata and access entries. A file's **type** is its MIME type, its extension and, for a
  content item, its content type. A hard-linked file has one content record and several
  placements, each with its own parent (3, Hard links).
- **Principal**: Anyone (public), Tenant, Organization, Role or User.
- **Role**: any group the business recognises: staff roles (Editor, Sales Rep, Marketing) and
  business roles (Customer, Vendor, Lead, Member).
- **No-permission role (shim, 2026-10-05)**: a role that grants no permissions and exists
  only so people can be shared with as a set ("everyone on this drive"). It is a **shim**:
  Crest has no group concept yet, and how grouping should work is open
  ([members.md](members.md) › Decisions needed › Grouping). The code that introduces it is
  marked as a shim, so it is found and replaced when grouping is decided.
- **Access level**, each including the ones before it:
  - **Viewer**: read.
  - **Commenter**: read and comment, nothing more.
  - **Editor**: create, read and update.
  - **Manager**: create, read, update and delete.
  - **Drive Admin**: a Manager who can also change sharing and rules. Granted only on a drive
    itself, never inside it.
- **Tenant administrator**: Orchard's administrator role for the tenant. A different thing from
  Drive Admin: it is not an access level and is never granted on a drive. Tenant
  administrators hold every permission on everything within their tenant.
- **Entry**: a grant ("Customer: Viewer") or a block ("Sam: Blocked") on one object, optionally
  time-bound.
- **Rule**: conditions → grants and blocks, defined on a folder, a drive or the tenant.
- **Drive**: the top of a tree of file objects. Personal, shared and organization drives are
  one class with one behaviour; they differ only in their defaults. A drive's **drive type**
  (Personal, Shared or Organization) records which it is.
- **System account**: a principal that owns things rather than a person: the tenant account,
  and one account per organization.
- **Published**: the object has a published version. Readers only ever get it; drafts are for
  Editors and Managers.

## 1. Foundations

The content sharing feature and its settings; system accounts (tenant and organization); the role registry with role kind, and assignment restricted to assigned roles; default Anyone and Tenant view grants removed when sharing is on.

- [ ] **Make content sharing a tenant feature.**
  1. Content sharing is a feature each tenant enables. Without it a tenant has no drives, sharing
     or rules, and its content items keep Orchard's standard permissions. Nothing about it
     crosses tenants.

- [ ] **Add system accounts.**
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

- [ ] **Add the role registry with role kind.**
  44. Each role has a **kind**, kept in Crest's role registry: **Assigned** (stored on users, the
      ordinary Orchard kind) or **Derived** (computed, with the provider that computes it).
      Orchard's role model has no field for this, and its built-in "system role" mechanism covers
      only its own fixed roles.
  45. Derived roles cannot be assigned. Every assignment path (the user editor, the users API,
      recipes, bulk assignment) offers Assigned roles only and refuses Derived ones on the server.
      Like Orchard's own assignable-role filter, it also leaves out system roles (Anyone,
      Authenticated).

- [ ] **Remove default Anyone and Tenant view grants.**
  84. No recipe shipped with Crest or a host built on it grants anything to Anyone or Tenant
      unless it means to.

## 2. Upload security

The scanner and type-detection provider interfaces, the quarantine store and flow, the ClamAV and libmagic providers, every upload path (including resumable uploads and Crest's own) routed through it. Host-wide, independent of sharing.

- [ ] **Build host-wide upload scanning.**
  86. Upload scanning is one host-level system shared by every tenant, independent of the sharing
      feature. Every upload from any user in any tenant is scanned before it is usable, because
      tenants share storage, processes and the host. No tenant can run without scanning.
  87. Scanning checks for malware, checks the content matches its claimed type, and removes
      metadata that should not be published, such as location data in images.
  88. Until it passes, an upload is quarantined outside the tenant's storage, shown to its
      uploader as pending, and never served or processed (no thumbnails, resizing, text extraction
      or indexing).
  89. The scanner is shared; tenant data is not. Uploads, quarantine and results are visible only
      to their own tenant.
  90. Scanners are providers behind one Crest interface, the way icon and tax providers are. The
      host configures its own scanners. A tenant can add an external scanner of its own and
      choose whether the host's scanners still run alongside it or are bypassed. A tenant with no
      external scanner always uses the host's.
  91. Type detection is a provider of its own: it identifies a file's real type from its content
      and reports it as a MIME type, which the type check (87) and rules (56) use. A scanner that
      also detects types can supply both.
  92. A provider returns a verdict: clean, rejected (with a reason), or failed. A failure keeps the
      upload in quarantine; it never releases it. Scanning is asynchronous, so a provider can be a
      remote service that answers later.
  93. Scanning and type detection run on the server side only, never on the uploading device. A
      client can be modified, scripted around or impersonated, so no verdict from it is trusted.
  94. A provider can be offsite: any scanner reachable through an API, such as a dedicated
      scanning server run beside the application or an external service. When uploads are a large
      part of an app's load, scanning moves to its own servers without changing the application.
      An offsite provider is handed the quarantined file by a time-limited reference it can fetch
      directly, so large files are not relayed through the application server, and reports its
      verdict back to Crest.
  95. The first deliverable is the interface and the quarantine flow. The first providers are
      ClamAV for malware and libmagic for type detection (91); pattern-rule scanning such as YARA is an
      optional later provider. See "Scanner candidates".

- [ ] **Resumable uploads land in quarantine like every upload** (ruling 2026-10-06). Every
  upload is quarantined while pending; resuming changes nothing. Orchard's Tus completion
  writes straight into the store (bypassing `FileCreationService`), so the reworked Media module routes
  it into quarantine with a pending placement, and the verdict commits or rejects it.
- [ ] **Module-generated files go through the same pipeline, with exemptions** (ruling
  2026-10-05). A module that generates files server-side writes them as new file objects or
  versions through the file-object API, so they are scanned like uploads. Not every write:
  the goal is cooperative real-time editing, so whether a write is scanned depends on the
  document type and the module creating it, and modules can be whitelisted.

## 3. File objects and drives

Documents, opaque blob keys, versions, metadata; the drive class, drive types and settings, organization drives with their admins, personal drives, inactive drives; the file-object API and the byte-serving endpoint.

- [ ] **Store file objects.**
  2. Images, files, videos and documents are file objects. Folders and drives are file objects of
     another kind: they contain objects, and their sharing flows down.
  3. An object's id never changes. Renaming, moving or replacing its bytes keeps the id, and
     links address the id, so a link keeps working and shows the current version.
  4. Every object carries editable metadata: title, description, alternative text, tags, owner,
     type, size, and who created and last modified it, and when.
  5. Replacing a file keeps its earlier versions, so a published version can be rolled back.

- [ ] **Hard links: one file, several places** (ruling 2026-10-06). A file can be placed in
  more than one folder, like a Linux hard link. The only real file is its content record
  (the id in the database, with its versions); every location is a virtual placement of
  it. Each placement is a full citizen of its folder: its own URL, its own permissions and
  share settings, inheriting from its own path. The content is shared exactly — editing
  through any placement changes it everywhere, and versions are the content's.
  - A **soft link** is different: an ordinary link that points at one file's location and
    opens it there; it has no permissions of its own and grants nothing.
  - Removing a placement removes that location only; the content goes when its last
    placement goes.
  - Folders can be hard-linked too (unlike Linux), with a **loop gate**: placing a folder
    is refused when the target is the folder itself or anything inside it, through any of
    its placements, so no folder ever contains itself. Checked on every placement and move.
  - This changes requirement 6 (one URL per file becomes one URL per placement, still no
    copies) and makes the access index per placement, not per file. Expected permission
    issues — such as someone with Editor on one placement changing content another
    placement's viewers see — are fixed as they are hit, not designed away up front.

- [ ] **One file system, built into the platform's Media module.**
  6. Files and media are one system. Every file has one URL; there is no separate media library
     holding copies. A file is public because it is shared with Anyone, not because it was put in a
     particular place.
  7. The platform's `OrchardCore.Media` module is reworked in place into Crest's file
     module. Its feature ids and the services other modules resolve stay, so everything built
     on Media keeps working: SEO, image fields, images in rich text and Markdown, media
     indexing, image processing, and recipes that enable Media.
  8. The Media libraries (`OrchardCore.Media.Abstractions`, `OrchardCore.Media.Core`) stay:
     stored content and other modules are built against their types. See "Reworking the
     Media module".
  9. `/media/...` URLs keep their form and are served by Crest from the drive tree, after the
     access check: public files cached as today, private files only to those with access.

- [ ] **Add storage providers.**
  10. File bytes are stored through Crest storage providers behind one Crest interface, the same
      pattern as icon and tax providers. A provider stores and serves bytes under opaque keys
      (object and version), keeps quarantined uploads apart from committed files, and keeps each
      tenant's bytes in that tenant's own root.
  11. Local disk is the first provider. Azure Blob and Amazon S3 providers follow, built on
      Orchard's storage libraries (`OrchardCore.FileStorage.AzureBlob`,
      `OrchardCore.FileStorage.AmazonS3`) rather than Orchard's Media storage modules.

- [ ] **Build the drive class and drive types.**
  12. Personal, shared and organization drives are one class with the same file objects, levels,
     sharing, rules, resolution, enforcement and interface. They differ only in their defaults:

     | Drive | Owner | Default Drive Admins | Created |
     | --- | --- | --- | --- |
     | Shared | the tenant account | none; assigned (requirement 31) | by hand |
     | Organization | the organization's account | the organization's admins | with each organization, when the tenant turns it on |
     | Personal | the staff user | that user, and only that user | with each staff user, when the tenant turns it on |

  13. Every drive stores its drive type: Personal, Shared or Organization. It is set when the
     drive is created, determines the defaults above, and is what the interface filters, groups
     and labels drives by ("My drive", "Shared drives", "Organization drives"). It never changes
     how access is resolved.

- [ ] **Set drive ownership and Drive Admin.**
  14. Everything in a drive is owned by the drive's owner, except in a personal drive, where its
     owner owns what they create. Members' portal uploads are owned by the drive they land in,
     normally their organization's.
  15. Drive Admin can be granted only on a drive itself, never on a folder or file inside it, and
     covers the whole drive. Below the drive, the highest grantable level is Manager.
  16. A Drive Admin never loses access to anything in their drive: blocks and rules below the
     drive do not apply to them. Full permissions in a drive (create, read, update, delete, and
     changing sharing and rules) belong to its Drive Admins.

- [ ] **Add organization and personal drives.**
  17. An organization's admins are its drive's Drive Admins by default, and follow the
      organization: someone who becomes an organization admin gains it, someone who stops loses
      it. Other Drive Admins can be added like any grant.
  18. Organization drives and personal drives are each a tenant setting, off by default, so each
      tenant chooses which drive types it uses: for example organization drives for each customer
      or household and no personal drives, or personal drives for staff alongside shared drives.
      Members never have a personal drive.
  19. In a personal drive, Drive Admin cannot be granted to anyone but the owner. How tenant
      administrators reach personal drives is set by the settings in "Administrator access to
      personal drives".

- [ ] **Make drives inactive.**
  21. A drive can be **inactive**: kept, with its contents and sharing intact, but out of use.
      Turning off the organization or personal drive setting makes the existing drives of that
      type inactive rather than deleting them, and a deleted user's drive can be kept inactive.
      What eventually happens to an inactive drive is for the retention system to decide.

- [ ] **Hide inactive drives.**
  23. Inactive drives are visible only to tenant administrators. Drive Admins, holders of the
      shared-drive permission and everyone with a grant inside lose access while it is inactive;
      nothing in it is reachable by link, search or listing.

- [ ] **Add shared-drive administration.**
  31. Managing shared drives is a tenant permission, separate from any access level: creating
      shared drives, assigning and removing their Drive Admins, and editing their base share
      settings (the entries on the drive itself). By default only the tenant administrator role
      holds it. It gives no access to a drive's contents: a user who holds it but has no grant
      inside a drive cannot open, edit or delete anything in it, though they can make someone,
      themselves included, a Drive Admin, which the audit trail records.
  32. Tenant administrators have full permissions on everything within their tenant, as a
      standing rule: every drive, every object in them, their sharing and rules, and tenant-level
      rules. Blocks and rules never apply to them. Personal drives are reached as set in requirements
      24–30. Everything they do is recorded in the audit trail like anyone else's actions.

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

- [ ] **Check deleting and moving.**
  69. Deleting needs Manager. Deleting a folder needs Manager on it and everything below it, including objects
      the person cannot see. If any fail, nothing is deleted, and the refusal says the folder
      contains items they cannot delete, without naming or counting them.
  70. Moving changes the access of everything moved, so it needs Manager over everything moved,
      plus Editor on the destination, with the same refusal.

- [ ] **Add comments.**
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

- [ ] **Compute business roles.**
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

- [ ] **Add derived role providers.**
  48. Derived roles are computed by providers behind a Crest interface: given a user and the
      organization they are acting for, a provider answers which business roles they hold.
      Party relationships are one provider, so the file system never needs to know what a party
      is.

## 6. Content items

Content types and items on the same model; the content items API's listing, creation and view checks.

- [ ] **Put content items on the same model.**
  73. Content items use the same principals, levels, entries and rules. Their chain is tenant →
      content type → item: a content type carries entries (set in its settings and in recipes),
      and an item adds its own.
  74. A content item that belongs in a folder, such as a document page in a client folder, follows
      that folder's chain instead, with its content type as its type for rules.

## 7. Administrator access to personal drives

Notice, delay, override, duration and logging; portal notifications; reactivation and user-deletion choices.

Tenant administrators always have full permissions on personal drives (the standing rule). Their
access is always a separate, deliberate step, and the tenant settings below decide what goes with
it: notice, delay and override. Together they let a tenant meet workplace-privacy requirements: a
legitimate reason, proportionate access, and telling people it can happen. The major drive
products take the same approach: admin access to a personal drive exists, but as a deliberate,
logged action rather than something in the admin's everyday view.

- [ ] **Open users' drives separately.**
  24. **Separate access.** Other users' personal drives never appear in a tenant administrator's
      everyday drive view. They are reached only through a separate "open a user's drive" button,
      which opens the drive in its own window, clearly marked as administrator access. This is
      fixed, not a setting.

- [ ] **Notify the owner.**
  25. **Notice.** When a tenant administrator opens another user's personal drive, the owner can
      be notified by email, by a portal notification, by both, or not at all. Default: not at all.
      Notices go through Orchard's notification service (`OrchardCore.Notifications`), which sends
      through each enabled method: email through its Email Notifications feature, and portal
      notifications through the stored, per-user notifications that Crest's portal notifications
      feature displays (requirement 30). Crest adds no delivery of its own. If a notice cannot be
      sent (email not configured, portal notifications turned off), the access proceeds and the
      failure is recorded with it. Requiring an approval instead waits for the approvals system
      ([approvals.md](approvals.md)).

- [ ] **Add the notice delay.**
  26. **Notice delay.** A tenant can set a delay between the notice and the access: with 30
      minutes, the administrator cannot open the drive until 30 minutes after the notice was
      sent, whether or not the owner has read it. Default: no delay. A delay requires notice to
      be on.

- [ ] **Allow overriding the delay.**
  27. **Delay override.** A tenant can allow the delay to be overridden. When allowed, a user with
      the "override personal drive notice delay" permission can open the drive at once, but must
      enter a reason, which is recorded and included in the notice. Default: not allowed.

- [ ] **Limit the access duration.**
  28. **Access duration.** When opening a user's drive, the administrator chooses how long the
      access lasts from a list the tenant sets and can edit. Default list: 30 minutes, 1 hour,
      1 hour 30 minutes, 2 hours, and until closed. When the time runs out the access ends and
      the window closes; continuing needs a new access, with notice and delay applied again.

- [ ] **Log every step.**
  29. **Logging.** Every step is recorded in the audit trail: the access request, the notice and
      how it was sent, any override and its reason, the opening itself and its chosen duration,
      and when and how the access ended.

- [ ] **Add portal notifications.**
  30. **Portal notifications** are a Crest feature, enabled by default when a tenant is created and
      able to be turned off. It shows each user's stored Orchard notifications in Crest's admin and
      member shells: a notification indicator, the list, and marking as read. Orchard's own display
      of them lives in its classic admin, which Crest's shells do not use. While an administrator
      is in another user's personal drive, every action they take there is recorded too, marked as
      administrator access.

- [ ] **Offer user-deletion choices for personal drives.**
  20. Deleting a user asks the person deleting them what happens to that user's personal drive,
      together with the rest of their data: transfer it to another user, move it into a shared
      drive, keep it inactive, or delete it. Which choices are allowed is set by the tenant's
      retention policies and legal requirements, once the retention system exists (a separate
      system, not yet planned); until then all four are offered.

- [ ] **Ask how to reactivate a drive.**
  22. Reactivating a drive, whether by turning its setting back on or individually, asks the
      person doing it whether to keep the drive's previous sharing and rules or start fresh with
      its drive type's defaults. Its contents are kept either way.

- [ ] **Scope: Crest owns drives, sharing, the CDN, version control, and the viewer pane
  with its add-on seam** (ruling 2026-10-06). Add-ons themselves (PDF, document, image and
  spreadsheet editors), bulk editing, version compare/merge, multi-provider redundancy and
  sync clients are downstream — provided by downstream modules or by anyone else. They
  work on files the way Google Drive add-ons do: through the file-object API with the
  user's own access, saving back as a new version or file object through the same pipeline
  as uploads. The file-object API must serve them.

## 8. Interface

Drive browser, Share panel, Rules screens, access indicators, the user editor's derived roles.

- [ ] **Build the viewer pane and its add-on seam** (ruling 2026-10-06). The drive browser
  has a viewer pane for the selected file. Add-ons register against file types and hook
  into it, so a file can be edited **in place** — simple edits to a document or an image
  right in the pane, without opening a separate app — or opened full in the add-on's own
  editor. With no add-on for a type, the pane previews. Add-ons can come from any module
  or third party; the pane and the seam are Crest's.

- [ ] **Build the Share panel.**
  96. Every object has a Share panel for its drive's Drive Admins: who has access and why, grants and blocks
      to add, change or remove, and the public link when it is public.

- [ ] **Build the Rules screens.**
  97. Each folder, drive and the tenant has a Rules screen, for the drive's Drive Admins or, at the
      tenant, for holders of the tenant permission (85): rules in run order as
      "conditions → effect", reorderable, filterable and editable, scoped as in 67.

- [ ] **Show access indicators.**
  98. Lists show an access indicator (private, shared, public) and filter by it.

- [ ] **Show derived roles in pickers and the user editor.**
  46. Sharing pickers offer both kinds, with derived roles labelled.
  47. The user editor shows a user's derived roles read-only beneath their assigned ones, each
      with the relationship that grants it ("Customer, member of Acme Corp"), linking to that
      relationship, since changing it is the only way to change the role.

## Platform audit

Audited against the platform (`src/`, then the OrchardCore fork on branch `Crest`) and
against Crest as it stands. What each area gives us, and what Crest builds.

### Files and storage

- **Bytes: reuse.** `IFileStore` / `IMediaFileStore` store bytes on local disk (per tenant,
  `App_Data/Sites/<tenant>/Media`), Azure Blob or S3. Crest keeps using it as a byte store, under
  opaque keys (object id and version), never user-visible paths.
- **File objects: build.** Orchard media has no database record, id, owner or metadata: a file
  is its path, so a move or rename breaks every reference (`MediaField` stores path strings) and
  there are no versions. Crest stores file objects as YesSql documents with their own indexes;
  names and folders live in the records, so renaming and moving never touch the bytes, and each
  version is an immutable blob.
- **Serving: build.** `/media` is static-file serving keyed by path, publicly cached, and a CDN
  bypasses Orchard entirely. Secure Media can only restrict per top-level folder, by role.
  Crest serves file bytes from its own endpoint, by object id, after the index check; drive
  files live in a storage root of their own, outside the media tree, so Orchard's media
  endpoints, GraphQL `MediaAssets` and `/media` never reach them.
- **Upload hooks: partly reusable.** `IFileEventHandler` (through `FileCreationService`) can
  reject or replace an upload stream, but runs synchronously inside the request, and Orchard's
  resumable (Tus) upload path skips it. Crest's quarantine flow is its own: uploads land in
  quarantine storage, providers return verdicts asynchronously, and only then is the file
  committed. Orchard's ClamAV connector (`OrchardCore.Antivirus`) is wrapped as the first
  malware provider.
- **Text extraction: reuse later.** `IMediaFileTextProvider` (PDF, Word, PowerPoint, text)
  can feed search for file objects.

### Authorization and data

- **Per-object checks: reuse the hook.** Orchard's authorization runs every
  `IAuthorizationHandler`, and one that calls `Fail()` overrides every grant, including the
  administrator's. Crest's access handler answers from the access index for file objects and
  content items; it never fails a tenant administrator (the standing rule), because a `Fail()`
  would override even them.
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

### Scheduling, notifications, audit, email

- **Time job: reuse.** `IBackgroundTask` with a cron schedule can run every minute per tenant.
  Idle tenants are skipped unless shell warm-up is on, and the lock is process-local without
  Redis; neither affects expiry, which reads enforce exactly (requirement 79).
- **Notifications: reuse.** `INotificationService` stores a per-user notification and sends it
  by email or SMS; one recipient per send.
- **Audit: reuse, extend.** `IAuditTrailEventHandler` can set the recorded user, which is how
  impersonated and system actions are attributed. Orchard records no file access or view events,
  so Crest records its own (requirement 29).
- **Email: reuse.** `IEmailService` with SMTP or Azure providers, configured per tenant.

### Reworking the Media module

Crest's file system is built **into the platform's `OrchardCore.Media` module**
(`src/OrchardCore.Modules/OrchardCore.Media`), so files and media are one system with one
URL per file while every module that depends on Media keeps working (requirements 6–11).
Since the hard fork (2026-10-09) this is an in-place rework. The earlier plan, a
Crest-owned copy carrying the stock module id with the stock assembly excluded from hosts
(proved by a spike on 2026-10-05), is retired, and so are its diff check against stock and
its asset-naming MSBuild fix.

**What the module is.** Media is two layers:

- **Libraries** (`OrchardCore.Media.Abstractions`, `OrchardCore.Media.Core`): `MediaField`,
  `IMediaFileStore`, `MediaOptions`, `MediaPermissions`, `DefaultMediaFileStore`, the image
  processing contract. Other modules compile against these, and stored content refers to them.
- **The module** (`OrchardCore.Media`: 139 source files, 8 features, about 70 service
  registrations): the feature ids, `/media` serving, API endpoints, admin UI, Secure Media, Tus
  uploads, SignalR, Liquid filters, shortcodes, display drivers, recipes, deployment.

**Who depends on what** (what the rework must keep working, or change alongside):

- *Feature-id dependents:* `OrchardCore.Seo` depends on `OrchardCore.Media`; the media startups
  in Content Fields, HTML and Markdown are `[RequireFeatures("OrchardCore.Media")]`;
  `Media.Azure`, `Media.AmazonS3` and `Media.ImageSharpV3` depend on it in their manifests.
- *Library-only dependents:* Seo, Html, Markdown, ContentFields and the two indexing modules
  (PDF, OpenXML).
- *Module-assembly dependents:* `Media.Azure` and `Media.AmazonS3` implement `ITusTempStore`
  from the module assembly.
- *Services:* dependents resolve services the module registers, `IMediaFileStore` first
  (Seo's handler needs it; without it the tenant fails to start).

**Decisions.**

- **Storage is Crest's own provider interface** (the icon and tax provider pattern; ruling
  2026-10-05). Local disk first; Azure and S3 providers later, on the platform's storage
  libraries. The stock `Media.Azure` and `Media.AmazonS3` modules are reworked onto it or
  removed.
- **Versions.** The local feed is 4.0.0-local, and Crest's compatibility and assembly
  versions are 4.0.0.

**Still to do and to watch.**

- [ ] **Inventory the module** — every service, route, shape, filter and handler that a
  dependent or existing content relies on (display drivers for `MediaField`, Liquid filters
  such as `asset_url`, shortcodes, the resizing middleware). That inventory is the real size
  of the rework.
- [ ] **Route Tus uploads through `FileCreationService`** so `IFileEventHandler` (antivirus)
  runs on them, and into quarantine (§ 2).
- **Risks.** Recipes, deployment plans and admin menus that target the module's settings and
  endpoints must be updated along with it.

### Crest today

- **Media API** (`api/crest/media`) is path-based with one global permission (`ManageMedia`);
  replaced by the file-object API. Tenant icons sit in the same media tree.
- **Content items API** lists without per-item filtering, creates on a global permission, and
  serves `/view` to anyone holding `ViewContent`; all change with requirements 73–74 and 77.
- **Roles and users.** The roles API returns every role; assigning roles to a user checks no
  per-role permission; the user editor takes roles as free text. All change with requirements
  44–47.
- **Members.** An organization's admins are the bindings marked `IsMemberAdmin`, set only for
  the first member, with no way to change it and no event when it changes; requirement 17 needs
  both. A binding's own roles are assigned roles scoped to the organization, separate from the
  derived business roles.
- **Parties.** Nothing answers "which party roles does this user hold"; the role items'
  party picker is not indexed. The business-role provider needs that index.
- **Patterns to reuse.** `IIconProvider` / `CompositeIconRegistry` for providers, the
  fail-closed in-query scoping of option sources for list filtering, `IMemberLifecycleHandler`,
  the member permission ceiling.

## Scanner candidates

Research for the scanner and type-detection providers (requirements 90 and 91). Licences checked October 2026; nothing here
is legal advice.

- **OrchardCore.Antivirus** (Orchard, BSD-3): Orchard's own module, in the Orchard build the
  hosts use. It scans through a file-storage hook before a file is stored, using a ClamAV daemon
  over its socket, and rejects the upload synchronously. It is the obvious first provider, but
  it rejects rather than quarantines, so Crest adapts it to the asynchronous verdict (92).
- **ClamAV** (GPLv2): the only mature open-source engine with a maintained signature database.
  It runs as a separate daemon reached over a socket rather than linked into Crest, on the same
  machine or a dedicated scanning server (requirement 94), which is
  how Orchard's module and most products use it. No MIT, BSD or Apache engine matches its
  coverage. ClamAV identifies file types from content, but as its own type codes
  (`CL_TYPE_PDF`), not MIME types, and the daemon does not return them: its
  `GenerateMetadataJson` option only prints to its debug log or writes into its temp
  directory. The command-line scanner can output them but reloads its whole signature
  database per file, too slow per upload. So ClamAV is a malware provider, not the type
  detector.
- **nClam** (Apache-2.0): a .NET client for the ClamAV daemon, an alternative to Orchard's own
  connector.
- **YARA / YARA-X** (BSD-3): a pattern-matching engine that runs detection rules on top of
  ClamAV's signatures, such as "Office documents with macros" or "PDFs containing JavaScript".
  Not a scanner on its own and not type detection; it needs rule sets, which carry their own
  licences. An optional later provider.
- **Atomdrift Scan** (Apache-2.0, rules included): a newer, ML-assisted scanner. Promising
  licence; too young to rely on without evaluation.
- **Type detection** (requirement 91): **libmagic, chosen.** The library behind the Unix `file`
  command (BSD-2-Clause), with the broadest file-type coverage. It is native: built into macOS
  and Linux and packaged for Windows (vcpkg). The `Mime` .NET wrapper (MIT) ships libmagic
  binaries for Windows (x86, x64, arm64), macOS (x64, arm64) and Linux (x64, arm64, musl),
  covering every server platform; it has no iOS or Android build, which detection never needs
  because it runs on the server (requirement 93). Mime-Detective, the pure .NET alternative,
  was set aside: only its smaller default signature pack is free for commercial use.
- **External services** (a tenant's own provider): cloud malware scanning such as the storage
  scanning offered by the large cloud providers, behind the same interface.

## Decisions needed

None at present.
