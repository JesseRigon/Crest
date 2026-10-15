# Media: files and drives

Grove's file system: upload security, file objects and drives, storage providers, serving and
CDNs, administrator access to personal drives, and the drive browser. The numbered
requirements are the spec; each section then says how it is built. Most of it is built and
compiled; the tests under `test/Grove.Tests/Media/` are written but not yet run, and the cloud
storage providers and CDN signers are not verified against live accounts. Status is tracked in
[backlog.md](backlog.md) › Media; the Grove APIs it replaces are under [Grove today](#grove-today).

## Example

The sharing example (a shared drive with grants, blocks and rules) is in [sharing.md](sharing.md) › Example.

## Goal

Grove gets a file system with sharing modelled on Google Drive's behaviour, in its own
implementation and under its own names, and applied the way a Unix file system applies
permissions: every file, folder and drive is a file object, and access is overlaid on it.

Access to files is **sharing**, its own module ([sharing.md](sharing.md)): file objects are one
shareable kind, registered by Media (the "file" kind). Media also registers its library paths as
the path-based "media" kind (`MediaShareableKind`, while the Sharing feature is on); the library
import copies those entries to placements ([The media library](#the-media-library-f1)).

Publishing is separate. Published says which version readers get; access says who the readers
are. Content items use the same model, with their content type in the chain.

100. All of this is Grove: open source, with no dependency on any downstream module. Downstream
    modules build their publishable links on it, and party relationships supply business roles
    through a seam Grove defines.

## Terms

- **File object**: a drive, folder or file, with a stable id, kind, name, parent, owner,
  metadata and access entries. A file's **type** is its MIME type, its extension and, for a
  content item, its content type. A hard-linked file has one content record and several
  placements, each with its own parent (3, Hard links).
- **Principal**: Anyone (public), Tenant, Organization, Role or User.
- **Role**: any group the business recognises: staff roles (Editor, Sales Rep, Marketing) and
  business roles (Customer, Vendor, Lead, Member).
- **No-permission role (shim)**: a role that grants no permissions and exists
  only so people can be shared with as a set ("everyone on this drive"). It is a **shim**:
  Grove has no group concept yet, and how grouping should work is open
  ([parties.md](parties.md) › Open questions › Grouping). The code that introduces it is
  marked as a shim, so it is found and replaced when grouping is decided.
- **Access level**, each including the ones before it:
  - **Viewer**: read.
  - **Commenter**: read and comment, nothing more.
  - **Editor**: create, read and update.
  - **Manager**: create, read, update and delete.
  - **Drive Admin**: a Manager who can also change sharing and rules. Granted only on a drive
    itself, never inside it.
- **Tenant administrator**: the platform's administrator role for the tenant. A different thing from
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

The scanner and type-detection provider interfaces, the quarantine store and flow, the ClamAV and libmagic providers, every upload path (including resumable uploads and Grove's own) routed through it. Host-wide, independent of sharing.

- [ ] **Host-wide upload scanning.**
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
  90. Scanners are providers behind one Grove interface, the way icon and tax providers are. The
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
      verdict back to Grove.
  95. The first deliverable is the interface and the quarantine flow. The first providers are
      ClamAV for malware and libmagic for type detection (91); pattern-rule scanning such as YARA is an
      optional later provider. See "Scanner candidates".

  Built in `src/Grove.Media.Scanning` (always-enabled feature; tests in
  `test/Grove.Tests/Media/Scanning`):
  - [x] **Quarantine flow** (88, 92, 93): `IUploadQuarantine` writes to the storage provider's
    quarantine area under `MediaBlobKeys.ForVersion`, records a scan job (YesSql, per tenant, 89),
    and runs detection and scanners server-side in a deferred task, with a background task for
    retries (exponential backoff), pending timeouts and undelivered verdicts. Any rejection
    rejects; any failure stays quarantined; all clean with a matching type commits, then
    `IMediaVerdictHandler` is told. Rejected bytes are deleted; the job keeps the reason.
  - [x] **Host and tenant scanners** (86, 90): host scanners and the type detector come from
    configuration (`Grove_Media_Scanning`); a tenant's site settings add its own offsite scanner
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
  - [x] **Location removal** (87): EXIF GPS and GPS-bearing XMP removed before commit, without
    re-encoding, from JPEG, PNG (`eXIf`, uncompressed XMP `iTXt`, CRC recomputed), WebP (`EXIF`,
    `XMP `) and HEIC/HEIF (the `Exif` and XMP items the `meta` box's `iinf`/`iloc` locate,
    single-extent items), in place and size-preserving (`ImageLocationStripper`,
    `MetadataLocationBlanker`): the GPS IFD is emptied and GPS XMP properties overwritten with
    spaces. Compressed XMP in PNG is not inspected.
  - [x] Every upload path routed through the quarantine: the file-object store (§ 3), Tus (below),
    and, once the media library is active, every write through `IMediaFileStore` (the media
    admin's upload, the `media` recipe step, attached fields), which the library drive backs (F1).
  - [x] An admin editor for the tenant's scanning settings, with the secret protected at rest:
    `Settings › Upload Scanning` (`/Settings/uploadScanning`,
    `ManageSettings`) over `api/grove/media/scanning/settings`. The secret is write-only: the API
    reports only whether one is set; a new one is stored through data protection
    (`SiteTenantScanningSettings.UpdateAsync`), and a secret written in clear before the editor
    still works until the next save protects it. An external scanner needs a secret and an
    absolute http(s) address.

- [x] **Resumable uploads land in quarantine like every upload.** Every upload is quarantined
  while pending; resuming changes nothing. The Tus completion (`OnFileCompleteAsync` in
  `src/Grove.Media/Startup.cs`) does not write into the store: a Tus upload whose metadata carries
  `parentPlacementId` completes through `IMediaObjectService.UploadAsync` (quarantined like every
  upload) and its temp file is deleted; an upload by `destinationPath` resolves the destination
  to its folder in the media library drive and goes through `IMediaObjectService.UploadAsync`
  too (`MediaLibraryUploads`). Until a tenant's library import has finished it goes to the media
  folder through `FileCreationService` (the antivirus hook), and the import scans it again.
- [x] **Module-generated files go through the same pipeline, with exemptions.** A module that generates files server-side writes them as new file objects or
  versions through the file-object API, so they are scanned like uploads. Not every write:
  the goal is cooperative real-time editing, so whether a write is scanned depends on the
  document type and the module creating it, and modules can be whitelisted.
  `IMediaGeneratedFiles` (`CreateAsync`, `WriteVersionAsync`, by
  module) writes with the caller's access, as an upload would. `MediaScanExemptionOptions` lists
  the exempt modules and their types (MIME types, `text/*` families, `.ext` extensions or `*`):
  modules add theirs with `services.Configure<MediaScanExemptionOptions>(o => o.Exempt(...))`,
  the host under `Grove_Media:Generated:Exemptions`. Nothing is exempt by default. An exempt
  write is recorded clean with "exempt: generated by {module}" and never handed to the
  scanners.

## 3. File objects and drives

Documents, opaque blob keys, versions, metadata; the drive class, drive types and settings, organization drives with their admins, personal drives, inactive drives; the file-object API and the byte-serving endpoint.

Built in `src/Grove.Media/Files/` on the contracts in `src/Grove.Media.Abstractions/Files/`
(tests in `test/Grove.Tests/Media/Files/`):
- Storage: `MediaContentDocument` and `MediaPlacementDocument` in the default collection,
  with content (kind, drive type, owner), placement (parent, drive, content, name, link target,
  access id, kind) and ancestor indexes, and `FileMigrations`. Not a separate collection:
  sharing's semi-join reads `ShareAccessIndex` in the query's own collection.
- The graph (`MediaGraph`, F4): a folder's children belong to its content, so every hard
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
- Requirement 19: on a personal drive only its owner holds Admin. The kind says so
  through sharing's `IShareableAdminEligibility`. The sharing service refuses an Admin grant to
  anyone else, whoever asks, tenant administrators included. The resolver counts any other
  Admin grant on it, stored or contributed, as Manager. Tests: `DriveAdminTests.cs`.
- Lists in the query: while sharing is on, children and drives
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
- `LocalMediaStorageProvider` (`App_Data/Sites/<tenant>/Files`, `Quarantine` and `Committed`).
- The byte endpoint `GET /files/{placementId}[/v/{n}]` and `HmacMediaUrlSigner`, plus the CDN
  signers (› Serving and CDNs).
- The REST API `api/files` (`Api/FilesApi.cs`).
- **Settings screen**: `Settings › Files` (`/Settings/files`, Blazor) over `api/files/settings`:
  the drive types, the organization admin role, reset-on-reactivation, and administrator
  access to personal drives (notice by email and/or portal, delay, override, durations).
- **Drives created automatically**: `IMediaDriveProvisioning` (`MediaDriveProvisioner`, as the
  system actor). A personal drive with each new staff user (`PersonalDriveUserEventHandler`,
  after the request), an organization drive with each published organization
  (`OrganizationDriveHandler` in Grove.Access.Members), and for everyone without one when a type is
  turned on (`PUT api/files/settings` runs `ProvisionAllAsync`). Who is staff and which
  organizations exist come from `IMediaDriveOwnerSource`, which Grove.Access.Members implements
  (`MemberDriveOwnerSource`); a user no source knows counts as staff. An existing drive, active
  or not, is never doubled.
- **Azure Blob and S3 storage providers**: `FileStoreMediaStorageProvider` puts file objects'
  bytes on any platform `IFileStore` (`Quarantine/`, `Committed/`, `Derived/` prefixes). The
  `Grove.Media.Azure.Storage` feature registers it over a `BlobFileStore`
  (`Grove_Media_Azure_Files`, or the media account and container under `{BasePath}-files`,
  `grove-files` when the base path is empty), with read-only SAS URLs for offsite scanners;
  `Grove.Media.AmazonS3` over an `AwsFileStore` in the media bucket
  (`Grove_Media_AmazonS3:FilesBasePath`, same default), with presigned GET URLs. Neither module
  replaces `IMediaFileStore`: they replace the library's pre-import store, so the media library
  stays the drive and the import reads the container or bucket. Not verified against live cloud
  accounts.
- **CDN signers** and **the private resize cache**: see › Serving and CDNs.
- **Multi-path inheritance** (F3, [sharing.md](sharing.md)): a child of a hard-linked folder
  inherits through every hard placement of the folder (`ShareableObject.OtherParents`); the
  resolver resolves each path and gives the most any active path gives; the access index keeps
  a document per path and lists admit an object when any path does.
- **Delete and move through sharing's checks**: `MediaObjectService` checks the
  placement itself (Manager; the personal-drive and inactive-drive rules stay Media's), then,
  while sharing is on, hands everything below to sharing's `IShareChangeChecks`
  (`RequireDeleteAsync`, `RequireMoveAsync`: Manager on everything below through every
  shareable kind, content items filed in a folder included, and Editor on the destination;
  the refusal never names or counts what failed). Media's own walk of file placements runs only
  with sharing off, where the media permissions decide and nothing else can be filed. Sharing's
  checks pass a tenant administrator, so Media first refuses when anything below lives in
  another user's personal drive that the caller has no administrator access to (§ 7), the
  same rule as listing: one query over the ancestor closure finds the other drives below, with
  no second walk, and the refusal names nothing.
- **Cross-drive children**: a listed child in another drive than the folder placement is decided
  on its own, so a tenant administrator never sees a child of another user's personal drive
  through a hard-linked folder.

**Left:**
- ☐ With sharing off, file objects fall back to the global media permissions, which have no
  per-object owner rule.

- [x] **Store file objects.**
  2. Images, files, videos and documents are file objects. Folders and drives are file objects of
     another kind: they contain objects, and their sharing flows down.
  3. An object's id never changes. Renaming, moving or replacing its bytes keeps the id, and
     links address the id, so a link keeps working and shows the current version.
  4. Every object carries editable metadata: title, description, alternative text, tags, owner,
     type, size, and who created and last modified it, and when.
  5. Replacing a file keeps its earlier versions, so a published version can be rolled back.

- [x] **Hard links: one file, several places.** A file can be placed in
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
  - Requirement 6 is therefore one URL per placement (still no copies), and the access index is
    per placement, not per file. Permission issues such as someone with Editor on one placement
    changing content another placement's viewers see are fixed as they are hit, not designed
    away up front.

- [ ] **One file system, built into the platform's Media module.**
  6. Files and media are one system. Every file has one URL; there is no separate media library
     holding copies. A file is public because it is shared with Anyone, not because it was put in a
     particular place.
  7. The platform's `Grove.Media` module is reworked in place into Grove's file
     module. Its feature ids and the services other modules resolve stay, so everything built
     on Media keeps working: SEO, image fields, images in rich text and Markdown, media
     indexing, image processing, and recipes that enable Media.
  8. The Media libraries (`Grove.Media.Abstractions`, `Grove.Media`) stay:
     stored content and other modules are built against their types. See "Reworking the
     Media module".
  9. `/media/...` URLs keep their form and are served by Grove from the drive tree, after the
     access check: public files cached, private files only to those with access. Built as the
     media library drive ([The media library](#the-media-library-f1)).

- [x] **Storage providers**, local disk, Azure Blob and S3 (above).
  10. File bytes are stored through Grove storage providers behind one Grove interface, the same
      pattern as icon and tax providers. A provider stores and serves bytes under opaque keys
      (object and version), keeps quarantined uploads apart from committed files, and keeps each
      tenant's bytes in that tenant's own root.
  11. Local disk is the first provider; Azure Blob and Amazon S3 providers are built on the
      platform's storage libraries (`Grove.Data.Storage.AzureBlob`, `Grove.Data.Storage.AmazonS3`)
      rather than its Media storage modules.

- [x] **The drive class and drive types.**
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

- [ ] **Drive ownership and Drive Admin.**
  14. Everything in a drive is owned by the drive's owner, except in a personal drive, where its
     owner owns what they create. Members' portal uploads are owned by the drive they land in,
     normally their organization's.
  15. Drive Admin can be granted only on a drive itself, never on a folder or file inside it, and
     covers the whole drive. Below the drive, the highest grantable level is Manager.
  16. A Drive Admin never loses access to anything in their drive: blocks and rules below the
     drive do not apply to them. Full permissions in a drive (create, read, update, delete, and
     changing sharing and rules) belong to its Drive Admins.

- [x] **Organization and personal drives**, created automatically with each organization or
  staff user (above).
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

- [x] **Inactive drives.**
  21. A drive can be **inactive**: kept, with its contents and sharing intact, but out of use.
      Turning off the organization or personal drive setting makes the existing drives of that
      type inactive rather than deleting them, and a deleted user's drive can be kept inactive.
      What eventually happens to an inactive drive is for the retention system to decide.

- [x] **Inactive drives are hidden.**
  23. Inactive drives are visible only to tenant administrators. Drive Admins, holders of the
      shared-drive permission and everyone with a grant inside lose access while it is inactive;
      nothing in it is reachable by link, search or listing.

- [x] **Shared-drive administration.**
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

Moved to [sharing.md](sharing.md) § 4: sharing is its own module (`Grove.Access.Sharing`), and file objects are one shareable kind.

## 5. Business roles

Moved to [sharing.md](sharing.md) § 5.

## 6. Content items

Moved to [sharing.md](sharing.md) § 6.

## 7. Administrator access to personal drives

Tenant administrators always have full permissions on personal drives (the standing rule). Their
access is always a separate, deliberate step, and the tenant settings below decide what goes with
it: notice, delay and override. Together they let a tenant meet workplace-privacy requirements: a
legitimate reason, proportionate access, and telling people it can happen. The major drive
products take the same approach: admin access to a personal drive exists, but as a deliberate,
logged action rather than something in the admin's everyday view.

Built (tests: `test/Grove.Tests/Media/Files/PersonalDriveAccessTests.cs`,
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
  through `Grove.Communications`: portal is the stored notification (written as the notification
  service writes one, with its events, but not sent through the user's own methods), email is
  the Email method. Each chosen channel gets the notice exactly once. A failed send is recorded
  on the access and in the audit trail, and the access proceeds.
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
- The drive opens in its own window (`/Files/user-drive/{driveId}`, marked as administrator
  access, closed when the access ends; reached from "Open a user's drive" on the drive browser).

**Left:** ☐ asking the user-deletion choices from the user-deletion flow itself (the API exists).

- [x] **Users' drives are opened separately.**
  24. **Separate access.** Other users' personal drives never appear in a tenant administrator's
      everyday drive view. They are reached only through a separate "open a user's drive" button,
      which opens the drive in its own window, clearly marked as administrator access. This is
      fixed, not a setting.

- [x] **The owner is notified.**
  25. **Notice.** When a tenant administrator opens another user's personal drive, the owner can
      be notified by email, by a portal notification, by both, or not at all. Default: not at all.
      Notices go through the platform's notification service (`Grove.Communications`), which sends
      through each enabled method: email through its Email Notifications feature, and portal
      notifications through the stored, per-user notifications that Grove's portal notifications
      feature displays (requirement 30). Grove adds no delivery of its own. If a notice cannot be
      sent (email not configured, portal notifications turned off), the access proceeds and the
      failure is recorded with it. Requiring an approval instead waits for the approvals system
      ([permissions.md](permissions.md) › Approvals).

- [x] **Notice delay.**
  26. **Notice delay.** A tenant can set a delay between the notice and the access: with 30
      minutes, the administrator cannot open the drive until 30 minutes after the notice was
      sent, whether or not the owner has read it. Default: no delay. A delay requires notice to
      be on.

- [x] **Delay override.**
  27. **Delay override.** A tenant can allow the delay to be overridden. When allowed, a user with
      the "override personal drive notice delay" permission can open the drive at once, but must
      enter a reason, which is recorded and included in the notice. Default: not allowed.

- [x] **Limited access duration.**
  28. **Access duration.** When opening a user's drive, the administrator chooses how long the
      access lasts from a list the tenant sets and can edit. Default list: 30 minutes, 1 hour,
      1 hour 30 minutes, 2 hours, and until closed. When the time runs out the access ends and
      the window closes; continuing needs a new access, with notice and delay applied again.

- [x] **Every step is logged.**
  29. **Logging.** Every step is recorded in the audit trail: the access request, the notice and
      how it was sent, any override and its reason, the opening itself and its chosen duration,
      and when and how the access ended.

- [x] **Portal notifications.** The `Grove.Communications.Notifications.Portal` feature (enabled by the Grove setup recipes, can be turned off) with
  `GET api/grove/notifications`, `POST .../{id}/read` and `POST .../read-all`, the caller's own
  notifications only. `GrovePortalNotifications` (Grove.Display.Components) shows the indicator, the list
  and marking as read in the admin title bar and the member shell header, and renders nothing
  while the feature is off. A portal notice of § 7 counts as not sent while it is off.
  30. **Portal notifications** are a Grove feature, enabled by default when a tenant is created and
      able to be turned off. It shows each user's stored notifications in Grove's admin and
      member shells: a notification indicator, the list, and marking as read. While an administrator
      is in another user's personal drive, every action they take there is recorded too, marked as
      administrator access.

- [x] **User-deletion choices for personal drives** (through the API; see Left above).
  20. Deleting a user asks the person deleting them what happens to that user's personal drive,
      together with the rest of their data: transfer it to another user, move it into a shared
      drive, keep it inactive, or delete it. Which choices are allowed is set by the tenant's
      retention policies and legal requirements, once the retention system exists (a separate
      system, not yet planned); until then all four are offered.

- [x] **Reactivation asks how.**
  22. Reactivating a drive, whether by turning its setting back on or individually, asks the
      person doing it whether to keep the drive's previous sharing and rules or start fresh with
      its drive type's defaults. Its contents are kept either way.

**Scope.** Grove owns drives, sharing, the CDN, version control, and the viewer pane with its
add-on seam. Add-ons themselves (PDF, document, image and spreadsheet editors), bulk editing,
version compare/merge, multi-provider redundancy and sync clients are downstream — provided by
downstream modules or by anyone else. They work on files the way Google Drive add-ons do: through
the file-object API with the user's own access, saving back as a new version or file object
through the same pipeline as uploads. The file-object API serves them.

## 8. Interface

Drive browser, Share panel, Rules screens, access indicators, the user editor's derived roles.

The Share panel, Rules screens, access indicators and pickers are sharing's
([sharing.md](sharing.md) § 8).

- [x] **The viewer pane and its add-on seam.** The drive browser
  has a viewer pane for the selected file. Add-ons register against file types and hook
  into it, so a file can be edited **in place** — simple edits to a document or an image
  right in the pane, without opening a separate app — or opened full in the add-on's own
  editor. With no add-on for a type, the pane previews. Add-ons can come from any module
  or third party; the pane and the seam are Grove's.
  Built in `Grove.Display.Components/Components/Files`: `GroveDriveBrowser` (drives
  grouped as "My drive", "Shared drives", "Organization drives"; path, children, upload, new
  folder, rename, delete, new shared drive; pick mode for the picker), `GroveFileViewerPane`
  (image, video, audio, PDF and text previews, open and download, versions with publish) and
  the seam: a component marked `[GroveFileViewerAddOn(name, types...)]` with a
  `GroveFileViewerContext Context` parameter, in any loaded assembly, edits in place or opens
  full; it saves back through `Context.SaveVersionAsync` (a new version through the upload
  pipeline, published once clean: `PUT api/files/{id}/content?publish=true`). A built-in text
  add-on (`GroveTextFileAddOn`) edits plain text, Markdown, CSV and JSON. Pages: `/Files` in the
  admin shell (menu Media › Files) and `/files` in the member shell.

## Platform audit

What each area of the platform gives the file system, and what Grove builds.

### Files and storage

- **Bytes: reuse.** `IFileStore` / `IMediaFileStore` store bytes on local disk (per tenant,
  `App_Data/Sites/<tenant>/Media`), Azure Blob or S3. Grove keeps using it as a byte store, under
  opaque keys (object id and version), never user-visible paths.
- **File objects: build.** Platform media has no database record, id, owner or metadata: a file
  is its path, so a move or rename breaks every reference (`MediaField` stores path strings) and
  there are no versions. Grove stores file objects as YesSql documents with their own indexes;
  names and folders live in the records, so renaming and moving never touch the bytes, and each
  version is an immutable blob.
- **Serving: build.** Stock `/media` is static-file serving keyed by path, publicly cached, and a
  CDN bypasses the application entirely. Secure Media can only restrict per top-level folder, by role.
  Grove serves file bytes from its own endpoint, by object id, after the index check; drive
  files live in a storage root of their own, outside the media tree, so the stock media
  endpoints, GraphQL `MediaAssets` and `/media` never reach them.
- **Upload hooks: partly reusable.** `IFileEventHandler` (through `FileCreationService`) can
  reject or replace an upload stream, but runs synchronously inside the request, and the stock
  resumable (Tus) upload path skips it. Grove's quarantine flow is its own: uploads land in
  quarantine storage, providers return verdicts asynchronously, and only then is the file
  committed. The platform's ClamAV connector (`Grove.Security.Antivirus`) is wrapped as the first
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
  impersonated and system actions are attributed. The platform records no file access or view events,
  so Grove records its own (requirement 29).
- **Email: reuse.** `IEmailService` with SMTP or Azure providers, configured per tenant.

### Reworking the Media module

Grove's file system is built **into the platform's `Grove.Media` module**
(`Grove.Media`), so files and media are one system with one
URL per file while every module that depends on Media keeps working (requirements 6–11). It is
an in-place rework of the module, not a Grove-owned copy beside it.

**What the module is.** Media is two layers:

- **Libraries** (`Grove.Media.Abstractions`, `Grove.Media`): `MediaField`,
  `IMediaFileStore`, `MediaOptions`, `MediaPermissions`, `DefaultMediaFileStore`, the image
  processing contract. Other modules compile against these, and stored content refers to them.
- **The module** (`Grove.Media`: 139 source files, 8 features, about 70 service
  registrations): the feature ids, `/media` serving, API endpoints, admin UI, Secure Media, Tus
  uploads, SignalR, Liquid filters, shortcodes, display drivers, recipes, deployment.

**Who depends on what** (what the rework must keep working, or change alongside):

- *Feature-id dependents:* `Grove.Seo` depends on `Grove.Media`; the media startups
  in Content Fields, HTML and Markdown are `[RequireFeatures("Grove.Media")]`;
  `Media.Azure`, `Media.AmazonS3` and `Media.ImageSharpV3` depend on it in their manifests.
- *Library-only dependents:* Seo, Html, Markdown, ContentFields and the two indexing modules
  (PDF, OpenXML).
- *Module-assembly dependents:* `Media.Azure` and `Media.AmazonS3` implement `ITusTempStore`
  from the module assembly.
- *Services:* dependents resolve services the module registers, `IMediaFileStore` first
  (Seo's handler needs it; without it the tenant fails to start).

**Design.**

- **Storage is Grove's own provider interface** (the icon and tax provider pattern): local
  disk, Azure and S3 on the platform's storage libraries; `Media.Azure` and `Media.AmazonS3`
  are reworked onto it.
- **Versions.** The local feed is 4.0.0-local, and Grove's compatibility and assembly
  versions are 4.0.0.

**Inventory.** Every service, route, shape, filter and handler
  that a dependent or existing content relies on, and where each stands after the media library
  (F1, below). "Library" means it now works on the library drive's placements; "path" means it
  still works on `IMediaFileStore` paths, which the library drive now backs.
  - *Features:* `Grove.Media`, `.Indexing`, `.Indexing.Text`, `.Cache`, `.Slugify`, `.Security`
    (Secure Media), `.Tus`, `.SignalR`. Ids unchanged.
  - *Services dependents resolve:* `IMediaFileStore` (Seo, Lists' MetaWeblog, Icons' tenant
    media, Server's `api/grove/media`, OpenApi; now `DefaultMediaFileStore` over
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
    library is active), `SecureMediaMiddleware` (unused once the library is active: `/media` never reaches it).
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
- **Tus uploads** go into quarantine (§ 2): once the library is active through
  `IMediaObjectService.UploadAsync`; before, to the media folder through `FileCreationService`,
  so `IFileEventHandler` (antivirus) runs on them.
- **Risks.** Recipes, deployment plans and admin menus that target the module's settings and
  endpoints must be updated along with it.

#### The media library (F1)

Built in `src/Grove.Media/Library/`, on contracts in `src/Grove.Media.Abstractions/Library/` and
`Fields/` (tests in `test/Grove.Tests/Media/Migration/`: path resolution, field reading with ids
and paths, the import of a small tree (scanned and trusted, re-run, field rewrite, sharing
grants), `asset_url`, Tus routing).

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
  an upload and is served once clean. The operator option `Grove_Media:Library:TrustExistingFiles`
  (or `"TrustExisting": true` on the recipe step) records them clean with the reason
  "imported, pre-existing" through `IMediaObjectWrites.ImportTrustedAsync` (system actor only),
  so a public site keeps serving them. They are never handed to the scanners: a file is scanned
  or trusted, never both. Scanning is
  the default because the tree was never scanned and the trust option cannot be undone per file.
  Dev tenants can be reset instead: a new tenant gets an empty library from the same migration.
- [x] **Incoming `/media/...` paths** resolve to their placement in the library drive (path,
  folder chain, placement; soft links followed, hard-linked folders reachable by each path) and
  are served in place by the same access-checked serving as `/files`, not redirected: the URL
  keeps its form (requirement 9), there is no extra round trip, and cached pages keep working.
  Anything else under `/media` is a 404.
- [x] **Resizing after the decision** (F2). On `/media` and `/files`, resize commands run only
  after the access check. A public file (an Anyone grant) is resized into the shared cache keyed
  by placement, version and commands; a private one goes to the private resize cache
  (› Serving and CDNs) and never enters the shared one.
- [x] **Media URLs are `/files/{placementId}`**, consistently: `asset_url`, the `a` tag,
  shortcodes, tag helpers, the field display, GraphQL and SEO all produce them, from a placement
  id, a `/files` or `/media` URL or a library path. A public file's URL carries the CDN base. A
  private one is signed with `IMediaUrlSigner` for 15 minutes (`Grove_Media:Library:SignedUrlMinutes`),
  only after the decision and only while sharing is on (with sharing off nothing is known to be
  private, so the byte endpoint decides on every request). SEO's images are never signed:
  crawlers do not carry the caller. The synchronous path APIs (`IMediaFileStore.MapPathToPublicUrl`,
  `Platform.AssetUrl`) keep the `/media` form, which resolves; a value that resolves to nothing
  keeps it too.
- [x] **Overwrites are published once clean.** `LibraryFileStore` replaces a file
  with `IMediaObjectWrites.ReplaceAsync(..., publishWhenClean: true)`: the new version becomes
  the published one as soon as its verdict is clean (kept on the content document as
  `PublishWhenClean`). A plain replace still waits to be published.
- [x] **Signed URLs never reach another caller's page.** The platform has no
  response or output cache middleware; its dynamic cache (shape and `{% cache %}`/`<cache>`
  blocks) can keep a rendered fragment for everyone. When `MediaReferenceResolver` signs a URL
  it adds the `user` context to the enclosing cache scope and expires it while the URL still has
  half its life. It also marks the response `Cache-Control: private, no-store`, so no CDN or
  proxy keeps the page. Tests: `test/Grove.Tests/Media/Files/SignedUrlCachingTests.cs`. Known
  gap: the `user` context does not vary by acting organization, so a URL signed for a member
  acting for one organization can show again on their page while acting for another, for at most
  half its lifetime.
- [x] **Name lookups go through the store** (`IMediaObjectStore.GetChildrenNamedAsync`, one
  indexed query), not through `YesSqlPlacementNameLookup` querying the placement index itself.
- [x] **Tus.** The `destinationPath` branch resolves the destination to its library folder
  (creating a missing one through the store, as the caller) and uploads through
  `IMediaObjectService` (`MediaLibraryUploads`).
- [x] **`IMediaFileStore` paths are the library drive** (`LibraryFileStore` under
  `DefaultMediaFileStore`), so the media admin, `api/media`, recipes, deployment and attached
  fields work on file objects. Reads are path lookups, as the folder on disk was: those surfaces
  decide by path first. Writes go through `IMediaObjectService` as the caller (the system actor
  for server-side work with none).
- [x] **Sharing on the library drive replaces Secure Media.** With sharing on, the import
  copies "media"-kind entries and rules from each path to its placement; without Secure Media it
  shares the drive with Anyone (the media folder was public); with it, each role's folder rights
  become Viewer grants (`ViewMediaContent` on the drive, `ViewMediaContent_{folder}` on the
  folder, `ViewRootMediaContent` on root files, `ViewOthersMediaContent` on the users folder;
  Anonymous is Anyone, Authenticated the tenant). With sharing off,
  `LibraryPlacementPermissionMapper` decides a library placement by its path, so Secure Media and
  the media folder permissions keep applying unchanged. The import also grants each user their
  own folder (Viewer with `ViewOwnMediaContent`, Manager with `ManageOwnMediaContent`, by their
  roles). An attached-field folder (`mediafields/{type}/{id}` at the library root) has its
  content item as a further sharing parent, so its files follow the item's access when content
  items are shareable; a change to the item's sharing reaches the folder's list rows at their
  next recalculation, single decisions see it at once.
- [x] **Recipes and deployment.** Content arriving by recipe or deployment has its field paths
  resolved in this tenant's library and foreign ids dropped (`MediaFieldImportHandler`); the
  media deployment step exports clean files only.
- [x] **The drive-aware picker.** The Blazor admin chooses files from any drive through
  `GroveDrivePicker`: `MediaFieldEditor` (the Blazor editor for `MediaField`) stores placement
  ids, position for position with the field's paths, texts and anchors; the HTML and Markdown
  editors insert a chosen file as its `files/{placementId}` URL (an image for an image, a link
  otherwise, appended to the text); the display system's `media` property kind is edited by
  `MediaPicker`, which stores the placement id.

Limits and what is left:
- The path cache lives 30 seconds per server; a move is seen within that time (a rename or a
  delete at once). A name lookup is one indexed query per segment.
- ☐ The classic (Razor) media picker and the media admin still work on library paths
  (`api/media`); they go with the MVC admin.
- Media fields in site settings (custom settings) are read by path until edited; the rewrite
  covers content items only. Older content versions keep their paths (they still resolve).
- ☐ The large-tree import runs in one scope; very large libraries may want batching.

### Grove today

- **Media API** (`api/grove/media`) is path-based with one global permission (`ManageMedia`);
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

How a CDN keeps private files private while still caching them, and how that meets the access
gate.

**What the CDNs offer.** None of them knows Grove's users or permissions; all of them can
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

**How Grove uses them.** The CDN is a byte cache in front of the origin; the **access gate stays
the only decision**:

1. A page asks for a file. The access gate decides, through sharing, for the caller and the
   placement.
2. **Public** (an Anyone grant on the placement): the URL is the plain public CDN URL; the public
   zone and the public resize cache serve it to anyone, cached as long as the file's version
   lives (the version is in the URL, so a new version is a new URL).
3. **Private**: on an allow, Grove issues a **short-lived signed URL** (or a signed cookie scoped
   to the caller's session for a page with many images) for the **private zone** - a separate
   hostname for admin and member assets - covering exactly that placement, version and size,
   valid for minutes. The CDN checks the token, serves from its cache (the cache key leaves the
   token out, so each file is cached once), and fetches from Grove's private origin on a miss;
   the private origin itself requires the same token, so the CDN is never an open door.
4. **Revocation** takes effect for new tokens at once; a token already issued lives until it
   expires, so the token lifetime is the bound on how long a removed share can still be read
   from the cache. It is short (minutes) for staff and member assets.
5. **No CDN configured**: the same flow against Grove's own origin, with its own public and
   private resize caches.

The signer is a provider behind one Grove interface (Cloudflare HMAC, CloudFront key pair, Bunny
token), configured per tenant, like storage and scanner providers.

**Implementation** (not verified against live CDNs):
- `IMediaUrlSigner` gains the zone's `BaseUrl` and `Verify`. The tenant's shell configuration
  `Grove_Media:Cdn` names the private zone (`PrivateBaseUrl`), the `Signer` (`cloudflare`,
  `cloudfront`, `bunny`) and its secret (`Cloudflare:Secret` and `TtlSeconds`,
  `CloudFront:KeyPairId` and `PrivateKey` PEM, RSA or ECDSA P-256, `Bunny:SecurityKey`).
  `CloudflareMediaUrlSigner` writes `verify={timestamp}-{HMAC}` as `is_timed_hmac_valid_v0`
  checks it (timestamp back-dated by the TTL so the token ends at the URL's expiry);
  `CloudFrontMediaUrlSigner` a canned policy (`Expires`, `Signature`, `Key-Pair-Id`); and
  `BunnyMediaUrlSigner` token authentication (`token`, `expires`).
- With a zone configured, private URLs (`MediaReferenceResolver`, `GET api/files/{id}/url`)
  are the zone's, signed with its token. The byte endpoint and the resizing middleware accept a
  valid signature from any of the tenant's signers (`MediaUrlSigners`) and refuse an invalid
  one, so the private origin requires the zone's own token.
- The private resize cache: derivatives of a private file are made only after the decision
  and kept in the storage provider's `Derived` area, keyed by placement, version and a hash of
  the commands, served `private, no-store`, and deleted with the placement.

**Known gap:** a cached fragment holding a signed `/files` URL varies by user, not
by acting organization, so a member can see a URL signed while acting for one organization on a
page rendered while acting for another, for at most half the URL's lifetime.

## Scanner candidates

Research for the scanner and type-detection providers (requirements 90 and 91). Licences checked October 2026; nothing here
is legal advice.

- **Grove.Security.Antivirus** (BSD-3): the platform's own module. It scans through a file-storage hook before a file is stored, using a ClamAV daemon
  over its socket, and rejects the upload synchronously. It is the obvious first provider, but
  it rejects rather than quarantines, so Grove adapts it to the asynchronous verdict (92).
- **ClamAV** (GPLv2): the only mature open-source engine with a maintained signature database.
  It runs as a separate daemon reached over a socket rather than linked into Grove, on the same
  machine or a dedicated scanning server (requirement 94), which is
  how the platform's module and most products use it. No MIT, BSD or Apache engine matches its
  coverage. ClamAV identifies file types from content, but as its own type codes
  (`CL_TYPE_PDF`), not MIME types, and the daemon does not return them: its
  `GenerateMetadataJson` option only prints to its debug log or writes into its temp
  directory. The command-line scanner can output them but reloads its whole signature
  database per file, too slow per upload. So ClamAV is a malware provider, not the type
  detector.
- **nClam** (Apache-2.0): a .NET client for the ClamAV daemon, an alternative to the platform's
  own connector.
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
  because it runs on the server (requirement 93). Not Mime-Detective, the pure .NET
  alternative: only its smaller default signature pack is free for commercial use.
- **External services** (a tenant's own provider): cloud malware scanning such as the storage
  scanning offered by the large cloud providers, behind the same interface.

## Decisions

- **F1. Paths versus ids: ids are stored**, so hard and soft links work. A file object is its id
  (the content record and its versions); a **path is a placement** of that id, and every
  placement is a full citizen of its folder: sharing's entries and rules apply to the placement,
  which carries its own settings and access. Fields (`MediaField` and every reference) store
  placement ids; existing path references are migrated once, and incoming `/media/...` paths
  resolve to their placement ([The media library](#the-media-library-f1)).
- **F2. Serving, resizing and the CDN: public and private caches**, both behind the access gate
  and the integrated sharing pipeline. Public files (an Anyone grant) keep the fast path, a
  public resize cache and a **public CDN** for the site. Private files are served only after the
  decision, from a **private resize cache** keyed by placement, version and size, through a
  **private CDN** for admin and member assets served to staff and members. No private byte ever
  reaches a public cache ([Serving and CDNs](#serving-and-cdns)).
- **F3. Access-index size and recalculation**: [sharing.md](sharing.md).
- **F4. Folder hard links and the loop gate: folders are media objects like files; they just
  have children**, and they can be hard-linked like files. The graph (an ancestor closure over
  placements) is **recalculated only on create, move, rename and delete**; otherwise it is
  cached and baked in. The loop gate checks the closure inside the same transaction, and
  placements and moves are serialized per tenant against races. Quotas count content once,
  whatever its placements.
- **F5. Search filtering**: [sharing.md](sharing.md).
