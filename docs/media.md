# Media: files and drives

Status: requirements agreed and audited, not started. Next: phase 1. This plan covers Crest's
file system, drives and access control, in build order; nothing of it is built yet, and the
Crest APIs it replaces are listed under "Crest today" below. The phases are the build order.
Each phase ends with the full suite green and its own checks.

## Example

The sharing example (a shared drive with grants, blocks and rules) is in [sharing.md](sharing.md) › Example.

## Goal

Crest gets a file system with sharing modelled on Google Drive's behaviour, in its own
implementation and under its own names, and applied the way a Unix file system applies
permissions: every file, folder and drive is a file object, and access is overlaid on it.

Access to files is **sharing**, which is its own module since 2026-10-11 ([sharing.md](sharing.md)):
file objects are one shareable kind, registered by Media. Until file objects exist Media registers
its paths as the kind (`MediaShareableKind`, while the Sharing feature is on; [sharing.md](sharing.md)
› Phases).

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
  ([parties.md](parties.md) › Decisions needed › Grouping). The code that introduces it is
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

Moved to [sharing.md](sharing.md) § 1 (the sharing feature, system accounts, the role registry, default grants).

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
  7. The platform's `Crest.Media` module is reworked in place into Crest's file
     module. Its feature ids and the services other modules resolve stay, so everything built
     on Media keeps working: SEO, image fields, images in rich text and Markdown, media
     indexing, image processing, and recipes that enable Media.
  8. The Media libraries (`Crest.Media.Abstractions`, `Crest.Media`) stay:
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
      Orchard's storage libraries (`Crest.FileStorage.AzureBlob`,
      `Crest.FileStorage.AmazonS3`) rather than Orchard's Media storage modules.

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

Moved to [sharing.md](sharing.md) § 4: sharing is its own module (`Crest.Sharing`), and file objects are one shareable kind.

## 5. Business roles

Moved to [sharing.md](sharing.md) § 5.

## 6. Content items

Moved to [sharing.md](sharing.md) § 6.

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
      Notices go through Orchard's notification service (`Crest.Notifications`), which sends
      through each enabled method: email through its Email Notifications feature, and portal
      notifications through the stored, per-user notifications that Crest's portal notifications
      feature displays (requirement 30). Crest adds no delivery of its own. If a notice cannot be
      sent (email not configured, portal notifications turned off), the access proceeds and the
      failure is recorded with it. Requiring an approval instead waits for the approvals system
      ([permissions.md](permissions.md) › Approvals).

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

The Share panel, Rules screens, access indicators and pickers are sharing's
([sharing.md](sharing.md) § 8).

- [ ] **Build the viewer pane and its add-on seam** (ruling 2026-10-06). The drive browser
  has a viewer pane for the selected file. Add-ons register against file types and hook
  into it, so a file can be edited **in place** — simple edits to a document or an image
  right in the pane, without opening a separate app — or opened full in the add-on's own
  editor. With no add-on for a type, the pane previews. Add-ons can come from any module
  or third party; the pane and the seam are Crest's.

## Platform audit

Audited against the platform (and before it the OrchardCore fork on branch `Crest`) and
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
  committed. Orchard's ClamAV connector (`Crest.Antivirus`) is wrapped as the first
  malware provider.
- **Text extraction: reuse later.** `IMediaFileTextProvider` (PDF, Word, PowerPoint, text)
  can feed search for file objects.

### Authorization and data

Moved to [sharing.md](sharing.md) › Platform audit.

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

Crest's file system is built **into the platform's `Crest.Media` module**
(`Crest.Media`), so files and media are one system with one
URL per file while every module that depends on Media keeps working (requirements 6–11).
Since the hard fork (2026-10-09) this is an in-place rework. The earlier plan, a
Crest-owned copy carrying the stock module id with the stock assembly excluded from hosts
(proved by a spike on 2026-10-05), is retired, and so are its diff check against stock and
its asset-naming MSBuild fix.

**What the module is.** Media is two layers:

- **Libraries** (`Crest.Media.Abstractions`, `Crest.Media`): `MediaField`,
  `IMediaFileStore`, `MediaOptions`, `MediaPermissions`, `DefaultMediaFileStore`, the image
  processing contract. Other modules compile against these, and stored content refers to them.
- **The module** (`Crest.Media`: 139 source files, 8 features, about 70 service
  registrations): the feature ids, `/media` serving, API endpoints, admin UI, Secure Media, Tus
  uploads, SignalR, Liquid filters, shortcodes, display drivers, recipes, deployment.

**Who depends on what** (what the rework must keep working, or change alongside):

- *Feature-id dependents:* `Crest.Seo` depends on `Crest.Media`; the media startups
  in Content Fields, HTML and Markdown are `[RequireFeatures("Crest.Media")]`;
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

- **Crest.Antivirus** (Orchard, BSD-3): Orchard's own module, in the Orchard build the
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

- [ ] **Paths versus ids (F1, decide before the file system).** The platform's `MediaField`
  stores only file **paths**, and everything around it works in paths (Liquid `asset_url`,
  shortcodes, SEO, search indexing, GraphQL, Crest's media API). A rename or move breaks
  stored paths, and with hard links one file has several paths; this document says links
  address ids, yet `/media/...` URLs come from the tree. Options: (1) paths become stable
  aliases through a path → placement table with redirects on rename and move, resolved
  inside the reworked file store; (2) placement URLs carry the id and stored paths are
  rewritten (dev tenants reset); (3) both. Recommendation: 2 for stored data (fields store
  placement ids) plus 1 for incoming URLs (old and pretty paths redirect to the placement).
- [ ] **Serving, resizing and the CDN assume public files by path (F2).** `/media/...` is
  served by middleware that resolves the path, resizes images and serves static files, with
  a per-top-folder role check for secure media. Private bytes must never land in the public
  resize cache. Options: (1) the access check runs first, before resolution and resizing;
  public files keep the fast path and CDN caching; (2) Crest's own resize cache keyed by
  placement and size behind the same check. Recommendation: 1, with 2 for private files.
- [x] **Access-index size and recalculation (F3).** Moved to [sharing.md](sharing.md) › Decisions needed.

- [ ] **Folder hard links and the loop gate (F4).** With folder hard links the tree is a
  graph: the loop gate must check every placement's ancestors, two moves at once can race
  into a loop, and folder counts and quotas become ambiguous. Options: (1) serialize
  placements and moves per tenant; (2) check a precomputed ancestor closure inside the same
  transaction. Recommendation: 2, with 1 as the guard against races; quotas count content
  once, whatever its placements.
- [x] **Search filtering (F5).** Moved to [sharing.md](sharing.md) › Decisions needed.
