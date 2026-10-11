# Media: files and drives

Status: requirements agreed and audited; build started 2026-10-11 (scope: upload security, file
objects and drives, storage providers, serving, administrator access to personal drives, and a
minimal browser page to confirm the system answers; the full interface later). Previously: not started. This plan covers Crest's
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

  Built 2026-10-11 in `src/Crest.Media.Scanning` (always-enabled feature; compiled, tests written
  in `test/Crest.Tests/Media/Scanning`, not yet run):
  - [x] **Quarantine flow** (88, 92, 93): `IUploadQuarantine` writes to the storage provider's
    quarantine area under `MediaBlobKeys.ForVersion`, records a scan job (YesSql, per tenant, 89),
    and runs detection and scanners server-side in a deferred task, with a background task for
    retries (exponential backoff), pending timeouts and undelivered verdicts. Any rejection
    rejects; any failure stays quarantined; all clean with a matching type commits, then
    `IMediaVerdictHandler` is told. Rejected bytes are deleted; the job keeps the reason.
  - [x] **Host and tenant scanners** (86, 90): host scanners and the type detector come from
    configuration (`Crest_Media_Scanning`); a tenant's site settings add its own offsite scanner
    and choose to run it beside or instead of the host's. No scanner configured means nothing
    passes (a development-only switch allows it).
  - [x] **Offsite providers** (94): a signed job (HMAC-SHA256 with the provider's shared secret)
    with the time-limited reference, or the bytes when storage cannot make one; the verdict comes
    back on `POST api/media/scanning/verdict`, accepted only when signed by the scanner it names,
    for a scan that scanner was asked to do.
  - [x] **ClamAV** (95): wraps the Antivirus module's clamd connection as a scanner.
  - [x] **Type detection** (87, 91): libmagic through the `Mime` package, or the built-in signature
    detector when libmagic is not configured. The type check compares the content with the
    claimed type and the name's extension.
  - [x] **Location removal** (87): EXIF GPS and GPS-bearing XMP removed from JPEGs before commit,
    without re-encoding. PNG, WebP and HEIC location data: not yet.
  - [x] Every upload path routed through the quarantine: the file-object store (§ 3), Tus (below),
    and, once the media library is active, every write through `IMediaFileStore` (the media
    admin's upload, the `media` recipe step, attached fields), which the library drive backs (F1).
  - [ ] An admin editor for the tenant's scanning settings, with the secret protected at rest.

- [x] **Resumable uploads land in quarantine like every upload** (ruling 2026-10-06). Every
  upload is quarantined while pending; resuming changes nothing. Orchard's Tus completion
  writes straight into the store (bypassing `FileCreationService`), so the reworked Media module routes
  it into quarantine with a pending placement, and the verdict commits or rejects it.
  Where (2026-10-11): `OnFileCompleteAsync` in `src/Crest.Media/Startup.cs` calls
  `mediaFileStore.CreateFileFromStreamAsync`; it must instead create the file object's version
  and submit the Tus temp stream to `IUploadQuarantine.SubmitAsync`, then delete the temp file.
  Partly landed 2026-10-11: a Tus upload whose metadata carries `parentPlacementId` completes
  through `IMediaObjectService.UploadAsync` (quarantined like every upload) and its temp file is
  deleted. Landed with F1: an upload by `destinationPath` resolves the destination to its folder
  in the media library drive and goes through `IMediaObjectService.UploadAsync` too
  (`MediaLibraryUploads`). Until a tenant's library import has finished it still goes to the media
  folder, through `FileCreationService` (the antivirus hook), and the import scans it again.
- [ ] **Module-generated files go through the same pipeline, with exemptions** (ruling
  2026-10-05). A module that generates files server-side writes them as new file objects or
  versions through the file-object API, so they are scanned like uploads. Not every write:
  the goal is cooperative real-time editing, so whether a write is scanned depends on the
  document type and the module creating it, and modules can be whitelisted.

## 3. File objects and drives

Documents, opaque blob keys, versions, metadata; the drive class, drive types and settings, organization drives with their admins, personal drives, inactive drives; the file-object API and the byte-serving endpoint.

**Built 2026-10-11** (compiled; tests written, not run), in `src/Crest.Media/Files/` on the
contracts in `src/Crest.Media.Abstractions/Files/`:
- Storage: `MediaContentDocument` and `MediaPlacementDocument` in the default collection,
  with content (kind, drive type, owner), placement (parent, drive, content, name, link target,
  access id, kind) and ancestor indexes, and `FileMigrations`. A separate collection was tried
  and dropped: sharing's semi-join reads `ShareAccessIndex` in the query's own collection.
- The graph (`MediaGraph`, ruling F4): a folder's children belong to its content, so every hard
  placement of a folder shows them. Each placement document carries its closure (every
  placement above it through any path, with depth), mapped to `MediaAncestorIndex`. It is
  recalculated for what a create, move, link or delete touches and everything below that. A
  rename changes nothing in it. The loop gate checks every placement of the destination's
  content in the same session, under the tenant's distributed lock, and commits before the
  lock is released.
- `MediaObjectService` (`IMediaObjectService` and `IMediaVerdictHandler`): every check goes
  through `IAccessDecision` with the placement as the resource. Viewing takes `ViewMedia`
  (Viewer). Creating and updating take the new `EditMedia` (Editor). Deleting and moving take
  `ManageMedia` (Manager) on everything below, and the refusal names nothing. A move also needs
  Editor on the destination. Hard links and soft links work as described below. Content goes
  with its last placement, and shortcuts go with their target.
- Versions: an upload is version 1, quarantined and handed to `IUploadQuarantine`. A clean
  verdict commits it, re-measures its size and hash, and publishes it when it is the first.
  Rejected keeps only the reason; failed stays in quarantine. With no quarantine registered, a
  version is committed and an error is logged (requirement 86).
- Drives: shared drives need `ManageSharedDrives` (requirement 31). Organization and personal
  drives follow the `FileDriveSettings` site settings (18), with one personal drive per user
  and none for members. Other users' personal drives never appear in the drive list (24).
  Inactive drives are out of reach for everyone but tenant administrators (23).
- The "file" shareable kind (`Sharing/MediaFileShareableKind.cs`, registered by
  `MediaSharingStartup`). It implements the tree (children by sharing parent), the scope target
  (`MediaPlacementIndex.AccessId`, the target's id for a soft link), the roots (the drives) and
  admin assignment (`ManageSharedDrives` holders assign Drive Admins on **shared** drives
  only, requirement 31). Organization drives are excluded because their admins are the
  organization's own (17); tenant administrators can still add others. Personal drives are
  excluded because an administrator gets in only through § 7.
- Requirement 19 (2026-10-11): on a personal drive only its owner holds Admin. The kind says so
  through sharing's `IShareableAdminEligibility`. The sharing service refuses an Admin grant to
  anyone else, whoever asks, tenant administrators included. The resolver counts any other
  Admin grant on it, stored or contributed, as Manager. Tests: `DriveAdminTests.cs`.
- Lists in the query (2026-10-11, sharing phase 2): while sharing is on, children and drives
  are filtered by sharing's scope (`IShareScopes`) on the placement index and paged in the
  query (`IMediaObjectListing.ListChildrenPageAsync`). With sharing off, the media permissions
  decide each object and the page is cut from the filtered list. The service keeps the access
  index current. It recalculates on create, upload, link, move and rename (with everything
  below), and on a tag, version or published-version change. It removes entries on delete.
  An inactive change recalculates the whole drive.
- Organization drives (17): created with an Admin grant to the tenant's member-admin role
  scoped to the organization (`FileDriveSettings.OrganizationAdminRole`, default "Member
  Administrator"), so Admin follows the binding.
- Inactive drives (21-22): turning a drive type off in the settings (`PUT api/files/settings`)
  makes its drives inactive, and turning it on makes them active again. Single drives use
  `POST api/files/drives/{id}/inactive`. Reactivation either keeps the sharing or resets it
  (stored entries and rules on the drive and below removed).
- A test page at `/Admin/Files/Test` (`FilesTestController`). It lists drives, creates a folder,
  uploads, lists children and downloads through `api/files`, only to show the system answers.
- `LocalMediaStorageProvider` (`App_Data/Sites/<tenant>/Files`, `Quarantine` and `Committed`).
- The byte endpoint `GET /files/{placementId}[/v/{n}]` and `HmacMediaUrlSigner` (§ Serving and
  CDNs). CDN signers are stubs.
- The REST API `api/files` (`Controllers/FilesController.cs`).
- Tests: `test/Crest.Tests/Media/Files/`.

**Left:**
- A settings screen for `FileDriveSettings` and `PersonalDriveAccessSettings` (the API exists).
- Creating organization drives with each organization, and personal drives with each staff
  user, when the tenant turns them on (they are created by hand today).
- Azure Blob and S3 storage providers.
- CDN signers (Cloudflare, CloudFront, Bunny).
- Public and private resize caches (F2).
- `/media` paths resolving to placements and `MediaField` storing placement ids (F1): built,
  see › Reworking the Media module › The media library (F1) for what is left.
- A child of a hard-linked folder can live in another drive than the folder placement listed.
  A tenant administrator listing it gets the scope "everything", so such a child of another
  user's personal drive could show up in their list. Rare; to close with multi-path inheritance
  (F3).
- Children of a hard-linked folder inherit sharing along one path, the placement they were
  created or moved through. When that placement is removed they move to another placement of
  the folder. Multi-path inheritance is F3 ([sharing.md](sharing.md)).
- With sharing off, file objects fall back to the global media permissions, which have no
  per-object owner rule.

- [x] **Store file objects.**
  2. Images, files, videos and documents are file objects. Folders and drives are file objects of
     another kind: they contain objects, and their sharing flows down.
  3. An object's id never changes. Renaming, moving or replacing its bytes keeps the id, and
     links address the id, so a link keeps working and shows the current version.
  4. Every object carries editable metadata: title, description, alternative text, tags, owner,
     type, size, and who created and last modified it, and when.
  5. Replacing a file keeps its earlier versions, so a published version can be rolled back.

- [x] **Hard links: one file, several places** (ruling 2026-10-06). A file can be placed in
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
     Built 2026-10-11 (F1): the media library drive; see › Reworking the Media module.

- [ ] **Add storage providers.**
  10. File bytes are stored through Crest storage providers behind one Crest interface, the same
      pattern as icon and tax providers. A provider stores and serves bytes under opaque keys
      (object and version), keeps quarantined uploads apart from committed files, and keeps each
      tenant's bytes in that tenant's own root.
  11. Local disk is the first provider. Azure Blob and Amazon S3 providers follow, built on
      Orchard's storage libraries (`Crest.FileStorage.AzureBlob`,
      `Crest.FileStorage.AmazonS3`) rather than Orchard's Media storage modules.

- [x] **Build the drive class and drive types.**
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

- [ ] **Add organization and personal drives.** Built 2026-10-11, except automatic creation
  with each organization or staff user (see Left above).
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

- [x] **Make drives inactive.**
  21. A drive can be **inactive**: kept, with its contents and sharing intact, but out of use.
      Turning off the organization or personal drive setting makes the existing drives of that
      type inactive rather than deleting them, and a deleted user's drive can be kept inactive.
      What eventually happens to an inactive drive is for the retention system to decide.

- [x] **Hide inactive drives.**
  23. Inactive drives are visible only to tenant administrators. Drive Admins, holders of the
      shared-drive permission and everyone with a grant inside lose access while it is inactive;
      nothing in it is reachable by link, search or listing.

- [x] **Add shared-drive administration.**
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

**Built 2026-10-11** (compiled; tests written, not run: `test/Crest.Tests/Media/Files/PersonalDriveAccessTests.cs`,
`DriveAdministrationTests.cs`):
- `PersonalDriveAccessService` implements `IPersonalDriveAccessService`. The file-object API
  refuses a tenant administrator in another user's personal drive, as "not found", unless an
  access is in force now (`MediaObjectService.CanAsync`).
- The separate step goes through the API. `GET api/files/personal-drives/{userId}` finds the
  drive, never its contents. `POST .../{driveId}/access` opens it, `GET .../{driveId}/access`
  returns the access in force, and `DELETE .../access/{id}` closes it. The everyday drive list
  never shows another user's personal drive.
- Settings: `PersonalDriveAccessSettings` is a site settings section (`GET`/`PUT
  api/files/settings`).
- Notice: email, portal, both or none, through `IPersonalDriveNotifier`. Its default sends
  through `Crest.Notifications`: portal is the stored notification, email is the Email method.
  A failed send is recorded on the access and in the audit trail, and the access proceeds.
- Delay: enforced server-side. It counts only when a notice is on.
- Override: needs the tenant setting, the new `OverridePersonalDriveNoticeDelay` permission
  and a reason. The reason is recorded and put in the notice.
- Duration: must be one of the tenant's list. The access ends on time; reads compare the clock.
- Logging: each step is an `IAccessAuditor` event: request, notice, override, open, end and
  close. Every check during an access is recorded with the reason "administrator access {id}".
- User deletion (20): `GET`/`POST api/files/personal-drives/{userId}/disposal` lists and runs
  the four choices:
  - transfer to a user (their drive, or a folder in theirs);
  - move into a shared drive, owned by the tenant;
  - keep inactive;
  - delete.
- Reactivation (22): keep the sharing, or reset it to the drive type's defaults (§ 3).

**Left:** the portal notification display (30); asking these choices from the user-deletion
flow itself (the API exists); and opening the drive "in its own window", which belongs to the
drive browser (§ 8). Each chosen channel gets the notice exactly once. A portal notice is only
the stored notification, written as the notification service writes one, with its events, but
not sent through the user's own methods.

Notice, delay, override, duration and logging; portal notifications; reactivation and user-deletion choices.

Tenant administrators always have full permissions on personal drives (the standing rule). Their
access is always a separate, deliberate step, and the tenant settings below decide what goes with
it: notice, delay and override. Together they let a tenant meet workplace-privacy requirements: a
legitimate reason, proportionate access, and telling people it can happen. The major drive
products take the same approach: admin access to a personal drive exists, but as a deliberate,
logged action rather than something in the admin's everyday view.

- [x] **Open users' drives separately.**
  24. **Separate access.** Other users' personal drives never appear in a tenant administrator's
      everyday drive view. They are reached only through a separate "open a user's drive" button,
      which opens the drive in its own window, clearly marked as administrator access. This is
      fixed, not a setting.

- [x] **Notify the owner.**
  25. **Notice.** When a tenant administrator opens another user's personal drive, the owner can
      be notified by email, by a portal notification, by both, or not at all. Default: not at all.
      Notices go through Orchard's notification service (`Crest.Notifications`), which sends
      through each enabled method: email through its Email Notifications feature, and portal
      notifications through the stored, per-user notifications that Crest's portal notifications
      feature displays (requirement 30). Crest adds no delivery of its own. If a notice cannot be
      sent (email not configured, portal notifications turned off), the access proceeds and the
      failure is recorded with it. Requiring an approval instead waits for the approvals system
      ([permissions.md](permissions.md) › Approvals).

- [x] **Add the notice delay.**
  26. **Notice delay.** A tenant can set a delay between the notice and the access: with 30
      minutes, the administrator cannot open the drive until 30 minutes after the notice was
      sent, whether or not the owner has read it. Default: no delay. A delay requires notice to
      be on.

- [x] **Allow overriding the delay.**
  27. **Delay override.** A tenant can allow the delay to be overridden. When allowed, a user with
      the "override personal drive notice delay" permission can open the drive at once, but must
      enter a reason, which is recorded and included in the notice. Default: not allowed.

- [x] **Limit the access duration.**
  28. **Access duration.** When opening a user's drive, the administrator chooses how long the
      access lasts from a list the tenant sets and can edit. Default list: 30 minutes, 1 hour,
      1 hour 30 minutes, 2 hours, and until closed. When the time runs out the access ends and
      the window closes; continuing needs a new access, with notice and delay applied again.

- [x] **Log every step.**
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

- [x] **Offer user-deletion choices for personal drives.**
  20. Deleting a user asks the person deleting them what happens to that user's personal drive,
      together with the rest of their data: transfer it to another user, move it into a shared
      drive, keep it inactive, or delete it. Which choices are allowed is set by the tenant's
      retention policies and legal requirements, once the retention system exists (a separate
      system, not yet planned); until then all four are offered.

- [x] **Ask how to reactivate a drive.**
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

- [x] **Inventory the module** (2026-10-11). Every service, route, shape, filter and handler
  that a dependent or existing content relies on, and where each stands after the media library
  (F1, below). "Library" means it now works on the library drive's placements; "path" means it
  still works on `IMediaFileStore` paths, which the library drive now backs.
  - *Features:* `Crest.Media`, `.Indexing`, `.Indexing.Text`, `.Cache`, `.Slugify`, `.Security`
    (Secure Media), `.Tus`, `.SignalR`. Ids unchanged.
  - *Services dependents resolve:* `IMediaFileStore` (Seo, Lists' MetaWeblog, Icons' tenant
    media, Server's `api/crest/media`, OpenApi; now `DefaultMediaFileStore` over
    `LibraryFileStore`: path), `IMediaFileProvider` / `IStaticFileProvider` (file version
    hashing, Seo's robots), `IMediaTokenService`, `IMediaProfileService`, `IImageProcessingEngine`,
    `IResizedImageCache`, `IMediaNameNormalizerService`, `IUserAssetFolderNameProvider`,
    `IChunkFileUploadService`, `ITusTempStore` (implemented by `Media.Azure` and `Media.AmazonS3`),
    `IMediaFileTextProvider` (PDF and OpenXML indexing modules), `FileCreationService`. New:
    `IMediaLibrary`, `IMediaReferenceResolver` (library).
  - *Routes:* `/media/{path}` (library: resolved and served by `/files`), `/files/{placementId}[/v/{n}]`
    and `api/files` (file objects), `api/media/*` (18 endpoints: items, folders, tree, upload,
    move, copy, delete, permitted storage, localizations, `TusFileInfo/{uploadId}`; path),
    `api/media/tus` (Tus; library), `/hubs/media` (SignalR), the admin pages (`Admin/Index`,
    `Options`, `MediaCache`, `MediaProfiles`; path).
  - *Request pipeline:* `MediaLibraryRequestMiddleware` (new: `/media` and `/files` resizing,
    after the decision), `MediaFileStoreResolverMiddleware` (remote stores only),
    `MediaImageProcessingMiddleware` and the static files (the media folder on disk, until the
    library is active), `SecureMediaMiddleware` (superseded once active: `/media` never reaches it).
  - *Shapes and views:* `MediaField` (display: library), `MediaField_Edit` and its `Attached`
    and `Gallery` editors (the media picker on paths; ids kept: library), `MediaField_Option`,
    `MediaField_DisplayOption`, `MediaFieldSettings_Edit`, `MediaApiSettings_Edit`,
    `Media_Wrapper__HtmlField` / `__HtmlBodyPart` / `__MarkdownBodyPart` (ContentFields, Html
    and Markdown insert `/media` URLs, which resolve), `NavigationItemText-media.Id`.
    `DisplayMediaFieldViewModel` keeps `Paths` and adds `Items`.
  - *Liquid:* `asset_url` (library), `resize_url` and `img_tag` (any URL), the `a` tag's
    `asset_href` (library); `DisplayMediaFieldViewModel`, `MediaFieldDisplayItem` and `Anchor`
    registered for member access.
  - *Razor:* tag helpers `asset-src` and `asset-href` (library), `img-width`/`img-height`/
    `img-profile`… (any URL); `Platform.AssetUrl` and `AssetProfileUrlAsync` (path, `/media`
    form), `Platform.AssetUrlAsync` (new: library), `ImageResizeUrl`, `IsImageFile`.
  - *Shortcodes:* `[image]`, the deprecated `[media]`, `[asset_url]` (library).
  - *GraphQL:* `MediaAssets` (path listing, filtered by access once the library is active; its
    `path` is the `/files` URL), the `MediaField` type (`paths`, `fileNames`, `urls`,
    `mediatexts` behind the legacy switch; `files` and new `placementIds`: library),
    `MediaFileItem` (new `placementId`; `url` and `resizeUrl` from the placement). SEO's
    `SeoMeta` type exposes its images as `MediaField`.
  - *Indexing:* `MediaFieldIndexHandler` (keys `.MediaText`, `.FileText`; library),
    the `.txt` and `.md` text providers, the PDF and OpenXML modules (by extension).
  - *Content handlers and fields:* `MediaField` with `AttachedMediaFieldHandler`,
    `AttachedMediaFieldContentHandler` (deletes a content item's attached folder) and
    `AttachedMediaFieldFileService` (moves attached uploads to `mediafields/{type}/{id}`; a
    quarantined file keeps its name instead of its hash), `MediaFieldSettings` and its driver,
    the field's less-known `Anchors` and `AttachedFileNames`. New: `MediaFieldImportHandler`.
  - *Recipes and deployment:* steps `media` (writes through `IMediaFileStore`: library,
    quarantined), `MediaProfiles`, `move-attached-media-fields`, new `MediaLibraryImport`;
    deployment steps `Media` (exports clean files only) and `AllMediaProfiles`; remote
    publishing (MetaWeblog).
  - *Background tasks:* resized-image cache cleanup, remote media cache cleanup, chunked upload
    cleanup, temp folder cleanup at tenant start.
  - *Permissions:* `ManageMedia`, `EditMedia`, `ManageSharedDrives`, `ManageMediaFolder`,
    `ManageOwnMedia`, `ManageOthersMedia`, `ManageAttachedMediaFieldsFolder`,
    `UploadRestrictedMedia`, `ManageMediaProfiles`, `ViewMediaOptions`, `ManageMediaApiSettings`,
    the view permissions (`ViewMediaContent`, `ViewRootMediaContent`, `ViewOwnMediaContent`,
    `ViewOthersMediaContent`, `ViewMediaContent_{folder}`), `ManageAssetCache`,
    `OverridePersonalDriveNoticeDelay`; the folder mappers (path) and the new
    `LibraryPlacementPermissionMapper`.
  - *Stored state:* `MediaField` JSON (`Paths`, `MediaTexts`, `Anchors`, `AttachedFileNames`,
    new `PlacementIds`), `MediaProfilesDocument`, `MediaApiSettings` and `MediaTokenSettings`
    site settings, new `MediaLibraryDocument`, sharing entries of the "media" kind (paths).
- [x] **Route Tus uploads through `FileCreationService`** so `IFileEventHandler` (antivirus)
  runs on them, and into quarantine (§ 2). Once the library is active a `destinationPath`
  upload goes through `IMediaObjectService.UploadAsync` (quarantine); before, it goes to the
  media folder through `FileCreationService`.
- **Risks.** Recipes, deployment plans and admin menus that target the module's settings and
  endpoints must be updated along with it.

#### The media library (F1)

Built 2026-10-11 (compiled; tests written, not run) in `src/Crest.Media/Library/`, on contracts in
`src/Crest.Media.Abstractions/Library/` and `Fields/`.

- [x] **`MediaField` stores placement ids.** `PlacementIds` beside `Paths`, position for
  position; `GetReferences()` reads the id first and the path second, so unmigrated content
  keeps working. The display driver resolves every item through `IMediaReferenceResolver`,
  which opens it with `IMediaObjectService` (the access check); the editor hands the picker
  each placement's current path and keeps its id while the path comes back unchanged. `Paths`
  is kept current where the placement has a library path: templates that read paths and
  exports to another tenant keep working.
- [x] **The one-time migration** (`MediaLibraryImporter`), run by the data migration
  (`Migrations.UpdateFrom1`, a deferred task after the tenant's scope) and again by the
  `MediaLibraryImport` recipe step. It creates the "Media library" shared drive if missing;
  imports the media folder (folders become folder objects, files become file objects whose
  version 1 is the existing bytes); moves rights to sharing; activates the library; rewrites
  media fields (`MediaFieldRewriter`, latest and published versions, nested items and SEO's
  images included). Safe to run again: what is in the drive is kept. The media folder on disk
  is left as it was, for rollback, and nothing reads it once the library is active.
- [x] **Existing files are scanned.** Every imported file goes through `IUploadQuarantine` like
  an upload and is served once clean. The operator option `Crest_Media:Library:TrustExistingFiles`
  (or `"TrustExisting": true` on the recipe step) records them clean with the reason
  "imported, pre-existing" through `IMediaObjectWrites.ImportTrustedAsync` (system actor only),
  so a public site keeps serving them. They are never handed to the scanners: a file is scanned
  or trusted, never both (2026-10-11). Scanning is
  the default because the tree was never scanned and the trust option cannot be undone per file.
  Dev tenants can be reset instead: a new tenant gets an empty library from the same migration.
- [x] **Incoming `/media/...` paths** resolve to their placement in the library drive (path,
  folder chain, placement; soft links followed, hard-linked folders reachable by each path) and
  are served in place by the same access-checked serving as `/files`, not redirected: the URL
  keeps its form (requirement 9), there is no extra round trip, and cached pages keep working.
  Anything else under `/media` is a 404.
- [x] **Resizing after the decision** (F2). On `/media` and `/files`, resize commands run only
  after the access check. A public file (an Anyone grant) is resized into the shared cache keyed
  by placement, version and commands; a private one is resized per request and never enters it
  (TODO: the private resize cache).
- [x] **Media URLs are `/files/{placementId}`**, consistently: `asset_url`, the `a` tag,
  shortcodes, tag helpers, the field display, GraphQL and SEO all produce them, from a placement
  id, a `/files` or `/media` URL or a library path. A public file's URL carries the CDN base. A
  private one is signed with `IMediaUrlSigner` for 15 minutes (`Crest_Media:Library:SignedUrlMinutes`),
  only after the decision and only while sharing is on (with sharing off nothing is known to be
  private, so the byte endpoint decides on every request). SEO's images are never signed:
  crawlers do not carry the caller. The synchronous path APIs (`IMediaFileStore.MapPathToPublicUrl`,
  `Platform.AssetUrl`) keep the `/media` form, which resolves; a value that resolves to nothing
  keeps it too.
- [x] **Overwrites are published once clean** (2026-10-11). `LibraryFileStore` replaces a file
  with `IMediaObjectWrites.ReplaceAsync(..., publishWhenClean: true)`: the new version becomes
  the published one as soon as its verdict is clean (kept on the content document as
  `PublishWhenClean`). A plain replace still waits to be published.
- [x] **Signed URLs never reach another caller's page** (2026-10-11). The platform has no
  response or output cache middleware; its dynamic cache (shape and `{% cache %}`/`<cache>`
  blocks) can keep a rendered fragment for everyone. When `MediaReferenceResolver` signs a URL
  it adds the `user` context to the enclosing cache scope and expires it while the URL still has
  half its life. It also marks the response `Cache-Control: private, no-store`, so no CDN or
  proxy keeps the page. Tests: `test/Crest.Tests/Media/Files/SignedUrlCachingTests.cs`. The
  `user` context does not vary by acting organization; a URL signed for a member acting for one
  organization can show again on their page while acting for another, for at most half its
  lifetime.
- [x] **Name lookups go through the store** (`IMediaObjectStore.GetChildrenNamedAsync`, one
  indexed query); `YesSqlPlacementNameLookup` no longer queries the placement index itself.
- [x] **Tus.** The `destinationPath` branch resolves the destination to its library folder
  (creating a missing one through the store, as the caller) and uploads through
  `IMediaObjectService` (`MediaLibraryUploads`).
- [x] **`IMediaFileStore` paths are the library drive** (`LibraryFileStore` under
  `DefaultMediaFileStore`), so the media admin, `api/media`, recipes, deployment and attached
  fields work on file objects. Reads are path lookups, as the folder on disk was: those surfaces
  decide by path first. Writes go through `IMediaObjectService` as the caller (the system actor
  for server-side work with none).
- [x] **Secure Media is superseded by sharing on the library drive.** With sharing on, the import
  copies "media"-kind entries and rules from each path to its placement; without Secure Media it
  shares the drive with Anyone (the media folder was public); with it, each role's folder rights
  become Viewer grants (`ViewMediaContent` on the drive, `ViewMediaContent_{folder}` on the
  folder, `ViewRootMediaContent` on root files, `ViewOthersMediaContent` on the users folder;
  Anonymous is Anyone, Authenticated the tenant). With sharing off,
  `LibraryPlacementPermissionMapper` decides a library placement by its path, so Secure Media and
  the media folder permissions keep applying unchanged.
- [x] **Recipes and deployment.** Content arriving by recipe or deployment has its field paths
  resolved in this tenant's library and foreign ids dropped (`MediaFieldImportHandler`); the
  media deployment step exports clean files only.
- [x] Tests in `test/Crest.Tests/Media/Migration/`: path resolution, field reading with ids and
  paths, the import of a small tree (scanned and trusted, re-run, field rewrite, sharing grants),
  `asset_url`, Tus routing.

**Left (F1):**
- Gaps in the Secure Media mapping: a user's own folder is not granted to its user, and
  attached-field files do not follow their content item's view right; both stay with the drive's
  administrators until shared.
- The path cache lives 30 seconds per server; a move is seen within that time (a rename or a
  delete at once). A name lookup is one indexed query per segment.
- The media picker and the media admin still work on library paths (`api/media`); a field can
  hold a placement outside the library only through the API. A drive-aware picker replaces them.
- The private resize cache (F2), and CDN signers for the private zone.
- Media fields in site settings (custom settings) are read by path until edited; the rewrite
  covers content items only. Older content versions keep their paths (they still resolve).
- `Media.Azure` and `Media.AmazonS3` replace `IMediaFileStore` with their own; a tenant on them
  keeps blob paths until they are reworked onto storage providers. The import reads the local
  media folder only.
- The large-tree import runs in one scope; very large libraries may want batching.

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

## Serving and CDNs

How a CDN keeps private files private while still caching them (researched 2026-10-11), and
how that meets the access gate.

**What the CDNs offer.** None of them knows Crest's users or permissions; all of them can
check a **signed token** that the origin issued, and cache the bytes independently of it:

- **Cloudflare**: *token authentication* - a WAF custom rule calls `is_timed_hmac_valid_v0()`
  to check an HMAC token (and its issue time) on the URL, blocking requests without a valid
  one; or a Worker does the same, and the two are compatible. Cached content stays cached; the
  rule decides who may receive it.
- **Amazon CloudFront**: *signed URLs* (one URL each) or *signed cookies* (a policy covering
  many URLs, wildcards included), signed with an RSA 2048 or ECDSA 256 key the distribution
  trusts, with an end time, an optional start time and an optional IP range. Signed cookies
  keep the URL clean, so viewers share one cache entry; signed URLs need the cache key to
  ignore the signature parameters.
- **Bunny**: *token authentication* on a pull zone - an HMAC-SHA256 token over the path and an
  expiry, optionally limited to a path prefix or to countries; requests without a valid token
  are refused.

**How Crest uses them.** The CDN is a byte cache in front of the origin; the **access gate stays
the only decision**:

1. A page asks for a file. The access gate decides, through sharing, for the caller and the
   placement.
2. **Public** (an Anyone grant on the placement): the URL is the plain public CDN URL; the public
   zone and the public resize cache serve it to anyone, cached as long as the file's version
   lives (the version is in the URL, so a new version is a new URL).
3. **Private**: on an allow, Crest issues a **short-lived signed URL** (or a signed cookie scoped
   to the caller's session for a page with many images) for the **private zone** - a separate
   hostname for admin and member assets - covering exactly that placement, version and size,
   valid for minutes. The CDN checks the token, serves from its cache (the cache key leaves the
   token out, so each file is cached once), and fetches from Crest's private origin on a miss;
   the private origin itself requires the same token, so the CDN is never an open door.
4. **Revocation** takes effect for new tokens at once; a token already issued lives until it
   expires, so the token lifetime is the bound on how long a removed share can still be read
   from the cache. Keep it short (minutes) for staff and member assets.
5. **No CDN configured**: the same flow against Crest's own origin, with its own public and
   private resize caches.

The signer is a provider behind one Crest interface (Cloudflare HMAC, CloudFront key pair, Bunny
token), configured per tenant, like storage and scanner providers.

**Known gap (2026-10-11):** a cached fragment holding a signed `/files` URL varies by user, not
by acting organization, so a member can see a URL signed while acting for one organization on a
page rendered while acting for another, for at most half the URL's lifetime.

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

- [x] **Paths versus ids (F1).** Ruled 2026-10-11: **ids are stored**, so hard and soft links
  work. A file object is its id (the content record and its versions); a **path is a
  placement** of that id, and every placement is a full citizen of its folder: sharing's
  entries and rules apply to the placement, which carries its own settings and access.
  Fields (`MediaField` and every reference) store placement ids; existing path references are
  migrated once, and incoming `/media/...` paths resolve to their placement.
  - [x] Built 2026-10-11 (compiled, tests not run): the "Media library" drive, the import and
    field rewrite, `/media` served from the drive, `/files` URLs everywhere, Tus, Secure Media
    moved to sharing. Left: › Reworking the Media module › The media library (F1).
- [x] **Serving, resizing and the CDN (F2).** Ruled 2026-10-11: **public and private caches**,
  both behind the access gate and the integrated sharing pipeline. Public files (an Anyone
  grant) keep the fast path, a public resize cache and a **public CDN** for the site. Private
  files are served only after the decision, from a **private resize cache** keyed by placement,
  version and size, through a **private CDN** for admin and member assets served to staff and
  members. No private byte ever reaches a public cache. (How CDNs such as Cloudflare support
  private caching, and how that meets the access gate: § Serving and CDNs below.)
- [x] **Access-index size and recalculation (F3).** Moved to [sharing.md](sharing.md) › Decisions needed.

- [x] **Folder hard links and the loop gate (F4).** Ruled 2026-10-11: **folders are media
  objects like files; they just have children**, and they can be hard-linked like files. The
  graph exists (an ancestor closure over placements) and is **recalculated only on create,
  move, rename and delete**; otherwise it is cached and baked in. The loop gate checks the
  closure inside the same transaction, and placements and moves are serialized per tenant
  against races. Quotas count content once, whatever its placements.
- [x] **Search filtering (F5).** Moved to [sharing.md](sharing.md) › Decisions needed.
