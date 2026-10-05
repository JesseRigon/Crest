# Spike: replacing Orchard's Media module with Crest's

Internal working plan, not a product plan. It decides whether Crest's file system (drives,
access control, published vs public) can replace Orchard's `OrchardCore.Media` module outright,
so files and media are one system with one URL per file, while every module that depends on
Media keeps working.

## The approach being tested

Orchard's Media is two layers:

- **Libraries** (`OrchardCore.Media.Abstractions`, `OrchardCore.Media.Core`): `MediaField`,
  `IMediaFileStore`, `MediaOptions`, `MediaPermissions`, `DefaultMediaFileStore`, the image
  processing contract. Other modules compile against these, and stored content refers to them.
- **The module** (`OrchardCore.Media`, 139 source files, 8 features, about 70 service
  registrations): the feature ids, `/media` serving, API endpoints, admin UI, Secure Media,
  Tus uploads, SignalR, Liquid filters, shortcodes, display drivers, recipes, deployment.

Crest keeps the libraries and replaces the module: a Crest module provides the feature id
`OrchardCore.Media`, and each host leaves the stock module out. Dependents keep matching on both
the feature id and the library types.

## What is already known

From reading the Orchard fork the hosts are built from (`/workspaces/OrchardCore`, branch
`Crest`):

- **Feature-id dependents.** `OrchardCore.Seo` declares a dependency on `OrchardCore.Media`.
  The media startups in Content Fields, HTML and Markdown are `[RequireFeatures("OrchardCore.Media")]`.
  Media.Azure, Media.AmazonS3 and Media.ImageSharpV3 depend on it in their manifests.
- **Library-only dependents.** Seo, Html, Markdown, ContentFields and the two indexing modules
  reference only the Media libraries, not the module.
- **Module-assembly dependents.** Media.Azure, Media.AmazonS3 and Media.ImageSharpV3 reference
  the module project itself. The only module type Azure and S3 use is `ITusTempStore` (in the
  module's `Services` namespace); ImageSharp uses only processing types, which Orchard 3.0 moved
  into `OrchardCore.Media.Abstractions` (`OrchardCore.Media.Processing`). Without the stock
  module assembly, Azure and S3 would fail to load until `ITusTempStore` moves into a library.
- **Bundling.** Hosts reference `OrchardCore.Application.Cms.Targets`, which brings in
  `OrchardCore.Application.Cms.Core.Targets`, which references the Media module and its five
  sibling modules.

From others' experience:

- OrchardCore issue #1023: a custom replacement for `OrchardCore.Users` worked until a module
  with a hard dependency on `OrchardCore.Users` (OpenID) re-enabled the stock module. Our case
  avoids that only if Crest's module carries the exact feature id `OrchardCore.Media` and the
  stock module is not present to be re-enabled.
- Orchard 3.0 changed Media's image processing (NetVips by default, ImageSharp optional, the
  contract made public). The replacement must keep providing `IImageProcessingEngine`'s
  pipeline or its own resizing.
- No published account was found of anyone replacing Media itself.

## Questions the spike answers

1. Can a host exclude the stock Media module from `Cms.Targets` (a direct reference with
   `ExcludeAssets="all"`), so its assembly is absent and Orchard does not discover it?
2. Does Orchard accept a feature with id `OrchardCore.Media` declared by a module with a
   different id, and resolve every `OrchardCore.Media` dependency to it?
3. With only that, do Seo, Html, Markdown and ContentFields enable, and do their media startups
   activate?
4. Do Media.Azure, Media.AmazonS3 and Media.ImageSharpV3 load once `ITusTempStore` is the only
   gap, and does moving it into a library close it?
5. Do the two indexing modules (PDF, OpenXML) work, since they need only the abstractions?
6. What does a recipe that enables `OrchardCore.Media` (Crest's setup recipe does) now enable?

## Steps

1. **Isolate.** Work in a git worktree of Crest.Host on a `spike/media-swap` branch, so Fruitful
   and the shared submodule checkout are untouched.
2. **Exclude.** Add `<PackageReference Include="OrchardCore.Media" ExcludeAssets="all" />` to the
   host. Build, then confirm `OrchardCore.Media.dll` is absent from the output and the stock
   feature is gone from the features list.
3. **Replace minimally.** Add a throwaway module `Crest.Files.Spike` declaring feature
   `OrchardCore.Media`, registering only `IMediaFileStore` (`DefaultMediaFileStore` over the
   tenant's file-system store) and `MediaOptions`.
4. **Probe.** Set up a tenant with a recipe enabling `OrchardCore.Media`, then enable Seo, Html,
   Markdown, ContentFields, the indexing modules, Media.ImageSharpV3, Media.Azure and
   Media.AmazonS3 one at a time, recording each result (enabled, startups active, load errors).
5. **Close the known gap.** In the Orchard fork, move `ITusTempStore` into
   `OrchardCore.Media.Core`, repack the local feed, and repeat the Azure and S3 probes.
6. **Inventory the rest.** List every service, route, shape, filter and handler the stock module
   registers that some dependent or existing content relies on (display drivers for
   `MediaField`, Liquid filters such as `asset_url`, shortcodes, the resizing middleware), to
   size what the real Crest module must provide.

## Pass criteria

- The stock module is absent and cannot be re-enabled by any dependency.
- Every dependent listed above enables against Crest's feature, with Azure and S3 needing at
  most the `ITusTempStore` move.
- The only Orchard changes needed are small and upstreamable.

## Upstream candidates

- Move `ITusTempStore` into a Media library (needed for this approach).
- Route Tus uploads through `FileCreationService` so `IFileEventHandler` (antivirus) runs on them
  (a security fix whatever we decide).

## Risks to watch

- Claiming `OrchardCore.Media` means Crest's module must track the stock module's public surface
  as Orchard evolves (new features, settings, recipe steps), or dependents drift out of sync.
- Recipes, deployment plans and admin menus that target the stock module's settings and
  endpoints stop working unless Crest provides equivalents.
- Third-party modules that reference the stock module assembly hit the same load failure as
  Azure and S3.

## Results (2026-10-05)

Run as a throwaway host in a scratch directory (`OrchardCore.Application.Cms.Targets` 3.0.2-local
from the shared local feed, one spike module `Crest.Files.Spike`), so no repository changed.

| Question | Result |
| --- | --- |
| 1. Exclude the stock module | **Yes.** A direct `PackageReference` to `OrchardCore.Media` with `ExcludeAssets="all"` removes `OrchardCore.Media.dll` from the output; the libraries and sibling modules stay. |
| 2. Feature id from another module | **Yes.** `OrchardCore.Media` resolved to `Crest.Files.Spike`; the stock module was never loaded. |
| 3. Seo, Html, Markdown, ContentFields | **Yes**, once the module registers `IMediaFileStore` (Seo's handler needs it; without it the tenant fails to start). |
| 4. ImageSharp, Azure, S3 | **ImageSharp: yes.** **Azure and S3: no**, as predicted: their `AzureBlobTusTempStore` and `S3TusTempStore` implement `ITusTempStore` from the stock module assembly, so Orchard's module discovery fails at startup (`GetExportedTypes`, assembly not found) even before anything is enabled. Excluded for the rest of the run. |
| 5. PDF and OpenXML indexing | **Yes**, once Crest's module also declares the stock sub-feature id `OrchardCore.Media.Indexing`. |
| 6. Recipes enabling `OrchardCore.Media` | **Yes.** The stock "Blank" setup recipe enabled Crest's feature. |

Findings beyond the questions:

- **Every stock sub-feature id is a contract.** Dependents name `OrchardCore.Media.Indexing`;
  Crest's module must declare whichever of the stock module's eight feature ids anything depends
  on (`OrchardCore.Media`, `.Indexing`, `.Indexing.Text`, `.Cache`, `.Slugify`, `.Security`,
  `.Tus`, `.SignalR`).
- **Services are a contract too.** Dependents resolve services the stock module registers
  (`IMediaFileStore` first). Step 6's inventory is the real size of the module.
- **The feed's label does not say which fork commit is in it.** `dev/dev.sh` packs the fork
  with a forced `-p:Version=3.0.2-local`, so hosts pin "3.0.2-local" while running the fork's
  code. The fork is deliberately versioned 4.0.0 (since 2026-08-06), to show it is not on
  Orchard's release line. The current feed was packed just before the fork's 2026-09-18 upstream
  merge, so some APIs differ from the fork's head (`DefaultMediaFileStore`'s constructor gained
  a `FileSizeHelper`). The audit in plans/media.md read the head.
- **Only one Orchard change is needed so far:** moving `ITusTempStore` into a Media library,
  for Azure and S3.

Not done, pending a decision: step 5 (making that move in the fork and repacking). The feed is
shared by every host, so repacking moves all of them onto the fork's head together. Proposed:
make the move on the fork's head (and offer it upstream), relabel the feed to a 4.0 version in
the dev scripts and every host's package pins, repack, set Crest's compatibility version to
4.0.0, then run each host's full suite.

Verdict: the swap works. The approach holds, with one small upstreamable Orchard change and a
module whose real work is reproducing the stock module's feature ids and services.

## Decisions (2026-10-05)

- **No change to the Orchard fork.** Storage becomes Crest's own provider interface (the icon and
  tax provider pattern), so Orchard's `OrchardCore.Media.Azure` and `OrchardCore.Media.AmazonS3`
  modules are left out of the hosts like the stock Media module, and the `ITusTempStore`
  coupling no longer matters. Local disk first; Azure and S3 providers later, on Orchard's
  storage libraries. The `ITusTempStore` move and the Azure filter edit were tried and reverted;
  the fork is untouched.
- **Feed relabelled to 4.0.0-local** in Fruitful (`dev/dev.sh`, `.devcontainer/postStart.sh`, all
  `Directory.Packages.props`), and Crest's compatibility and assembly versions set to 4.0.0, to
  match the fork's own 4.0.0. Crest.Host and Venti follow when they pull the Crest commit.
- Recorded as requirements 6–11 of plans/media.md.

## Full media functionality: results (2026-10-05)

Tested against the 4.0.0-local feed (repacked from the fork's head), in a throwaway host:
Orchard's stock `OrchardCore.Media`, `OrchardCore.Media.Azure` and `OrchardCore.Media.AmazonS3`
excluded, and a Crest-owned copy of the stock module's source built as assembly `Crest.Media`.

**Same module id, different assembly.** `Crest.Media` declares
`[assembly: Module(Id = "OrchardCore.Media")]`. Orchard supports this (`ModuleInfo.Id` lets a
module change its assembly name and keep its logical name), so areas, routes, static paths and
every feature id stay exactly as Orchard and its dependents expect.

**One gap in Orchard, fixed in Crest's project file.** Orchard's module build
(`OrchardCore.Module.Targets`) names embedded static assets and the module asset index after
`$(AssemblyName)`, while the runtime looks them up under the logical id. With id and assembly
name different, every `/OrchardCore.Media/...` asset returned 404. A small MSBuild target in
`Crest.Media.csproj`, running after Orchard's embedding step, renames both to the id (handling `/`
and `\` path separators). Worth offering upstream: let the targets use the module id.

| Behaviour | Result |
| --- | --- |
| Stock assemblies | Absent: only `Crest.Media` plus the Media libraries and sibling modules load |
| Blog setup recipe (media step, image fields) | Ran; recipe images written to the tenant's media store |
| `/media/...` serving | 200, correct content types |
| Resizing (NetVips) | 100×100 crop and WebP conversion produced; resized cache written. Needs a token unless `UseTokenizedQueryString` is off, and only configured `SupportedSizes`, as stock |
| Admin Media library page | 200, with its scripts and styles served |
| Media API | Listing, upload, serving the upload, folder creation: all 200 |
| Feature by feature | Cache, Slugify, Indexing, Indexing.Text, PDF, OpenXML, SEO, ImageSharp, Tus, SignalR, Security: all enable; site, media and admin keep working after each |
| Secure Media | Anonymous 404, signed-in admin 200, as stock |
| Resumable uploads (Tus) | Endpoint live (`Tus-Resumable: 1.0.0`, creation, termination, expiration) |

Verdict: full current media functionality is restored by a Crest-owned module, with no change to
Orchard. Azure and S3 come back later as Crest storage providers.
