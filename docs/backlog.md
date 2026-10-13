# Backlog: ruled but not built

Items the docs record as decided, with no open question against them, that the code does not
have yet (swept 2026-10-13). Each line names the doc that holds the ruling; the ruling there is
the spec. Mark an item done here when it lands, and move the detail into its doc's "What landed".

Left out on purpose, because a question is still open against them: the connection system and
the Queries API (queries.md Q6, Q7; workflows.md "what the platform offers for connections"),
audit feeds at volume (A4), the designer UI and its move (blazordesigner.md, workflows.md W6),
design systems as content (part vs fields), cross-tenant translation suggestions, the global
editing UI (which UI first), the geocoder, approvals, the server extraction (S1 and S4 are
proposals, S2 and S3 open), shells on their own hostnames, user settings, and the parked
security audit and sandboxed custom elements (blazor-display.md).

Status: ☐ open, ◐ partial, ☑ done.

## Small fixes

- ☑ `ShellCompatibilityService` matches the longest module id, not `StartsWith(id + ".")` (shells-and-themes.md)
- ☑ The server provides the tenant base to the WASM host instead of the client peeling the shell suffix (shells-and-themes.md)
- ☑ Currency lookup asks the providers before the culture table (types.md; host bugs list #12)
- ☑ The Iconify cache is not a hosted service (host bugs list #7)
- ☑ Member self-registration sends FirstName/LastName (host bugs list #1)
- ☑ The global store needs no host call (`AddCrestGlobalStore`) (globals.md)
- ☑ Global schema versioning moves onto `DataMigration` (queries.md)
- ☑ One `CrestTenant` model, not two (regions.md)
- ☑ `option_label` / `option_list` Liquid filters (content-items.md)
- ☑ "DBA" out of the organization-type seed; `convert-to-staff` removed (parties.md)
- ☑ Platform modules' unused activity display drivers and views deleted (workflows.md)
- ◐ Host references out of Crest's test solution and comments; a publishing gate test (architecture.md #4) - left: item-classification list move, pre-publish review
- ☑ A Crest-owned party role or fixture (architecture.md #3)
- ☑ Location data removed from PNG, WebP and HEIC uploads (media.md)
- ☑ An admin editor for the tenant's upload scanning settings (media.md)
- ☑ Machine actors: OpenId in the setup recipe, antiforgery skipped for bearer requests, the client id in the workflow actor (machine-actors.md)
- ☐ Shortcodes: retired into Liquid (blazor-display.md, shortcodes.md)

## Access (access.md, permissions.md)

- ☑ Anonymous actors: machine identity and roles on connections, the "accepts anonymous runs" flag, login and registration triggers run as the user the event names
- ◐ Members onto the extension system (resource mapper, scope provider on the org binding index, `OrganizationScopeProvider` out of Server). Left: the member ceiling denies `ManageMembers` before the mapper, so member admins need a ruling (access.md)
- ◐ Parties onto the extension system (mapper, scope provider, route permissions off `ICrestRoutePermissionProvider`). Left: the mapper, whose rules are not ruled (access.md)
- ◐ Page and route gates move to Access (`ICrestRoutePermissionProvider`, `CrestRouteAuthorizationService`, `AssignmentScopeProvider`; `AdminFilter` goes). Left: `AssignmentScopeProvider` (S3 open), `AdminFilter` (goes with the MVC admin)
- ☐ Admin controllers' per-type content authorization goes; the scope is the filter
- ☑ Version bumps only for rights writes; implied-by resolved from the permission instance; the revocation check run (the check is in the suite; it runs with the tests at the end)
- ◐ Recorded defects: SSRF guard on messaging and HTTP activities, per-definition narrowing of engine instance reads, `UsersController` scoped. Left: instance-read narrowing (which list narrows a read is not ruled)
- ☑ Per-part permissions as scope providers (permissions.md, ruling 2026-10-10): the seam; the first rule comes with the first type that needs one

## Display (blazor-display.md, widgets.md)

- ◐ Widget replacements (widgets.md): rich text, figure, forms, validation, search box landed; left: per-item page body (storage not ruled), visibility rules (needs an expression form), captcha, Facebook, a media picker editor
- ◐ Step 1 leftovers: route scanner reads the catalogs ☑, page regions as zone keys ☑, editor facet entries ☑, primitives annotated (seven more; icon, markdown, layout hosts and editors left, reasons in blazor-display.md)
- ◐ Step 2 leftovers: tree cache (versioned and validated, see decision 12; the unfiltered cache is left); display events for bound nodes ☑
- ◐ Step 7 remainder: `IHtmlDisplay`, Razor/Liquid shape engines, tag helpers and the markup fallback retired: body shapes got components; the retirement waits on the stock MVC pages (~1,250 Razor views) getting component pages
- ◐ Decision 6: zone keys as registry entries with a schema; single or multiple instances per page: keys, contributions, validation and the single-instance check landed; left: priority reordering through overrides and a `Zone` component node (designer zone settings)
- ◐ Decision 9 / § 6a: designed pages (page record and route-table provider): record, store, API, recipe step, data route provider and site rendering landed; admin and member pages are not routed (no shell component renders a data-defined page)
- ☑ Decision 10: the default permitted-fields `Content` template
- ◐ Decision 12: client ETag trees, unfiltered server cache: ETags, client-held trees and the shared anonymous cache landed; the unfiltered per-page cache needs the builder's per-viewer decisions moved into the node filter
- ◐ § 10: per-page permission sets (Design, Schema, Data, Publish): per designed page landed; Publish per component waits for component drafts
- ◐ Module homes: `Crest.Data.Types` ☑ (registry; readers not wired), `Crest.Global` → `Crest.Data.Global` ☑ (Fruitful needs the repack), `Crest.Components.Primitives` / `.Templates` skipped (open question in blazor-display.md)
- ◐ § 12: one expression engine: the engine's Liquid runs through the platform's, one Fluid version; `Crest.Scripting` removal waits for workflows.md's Retire step, limits wait for the parked sandbox

## Workflows (workflows.md)

- ◐ Operations step 1 remainder: caller lifetime rules for circuits and websockets ☑, read logging as a tenant setting ☑, conformance fixtures for organizations and policies ☑. Left: OpenId validation in the host recipe (the same work as the Machine actors line above)
- ◐ Step 3: typed per-type content operations ☑ (Copy/Move fields write through them). Left: one registry (`WorkflowRegistryCatalog` folded) and per-tenant operation names, both waiting on a ruling (workflows.md › Tasks › 3)
- ☑ Step 4: queue partitioned by correlation id, idempotency keys (unit id + slot); the object version in the payload is not built (no raiser names one)
- ◐ `ICrestAntiforgery`; the hook attachment page ☑ and webhook insight; per-process metrics. Left: `ICrestAntiforgery` (ruled to come with the designer move, W6 open); webhook insight and per-process metrics (ruled as audit feeds, which wait on run history in audit)

## Audit (audit.md)

- ☑ Steps 1–4: impersonation actor, session and impersonation events, non-content write events, the audit index, content history as diffs with checkpoints, workflow run history in audit, audit pages (2026-10-13)
- ◐ Step 5: feeds' data side (indexed record, parties, owner, organization, both attributions) built; feed definitions as saved queries wait on A4

## Sharing (sharing.md)

- ☑ Phase 3: business roles and the role registry (`RoleKind`, derived roles)
- ☑ Phase 4: Share panel, rules screens, access indicators, derived-role picker
- ☑ System accounts; delete/move manager checks; comments; items following a folder chain

## Media (media.md)

- ☑ CDN URL signers; Azure and S3 storage on the file system (2026-10-13; not verified against live cloud accounts or CDNs)
- ☑ Private resize cache; multi-path inheritance; organization and personal drives created automatically; `FileDriveSettings` screen (2026-10-13)
- ☑ Module-generated files through the pipeline with exemptions (2026-10-13)
- ◐ Drive-aware picker; Secure Media mapping gaps (2026-10-13: the Blazor media field editor and both gaps; left: the classic Razor media picker and the HTML/Markdown editors' insert still use library paths)
- ☑ Portal notifications; viewer pane and add-on seam; drive browser (2026-10-13)

## Shells and themes (shells-and-themes.md)

- ☑ A disabled module is absent per tenant; theme clients and UIs load per shell
- ☑ Member password-reset and external-login pages; side-checking login gate
- ☑ Organization slugs in paths and an organization picker; one device-local tenant switcher; impersonation under one account
- ☑ Shell registry (replaces `RouteBucket` and `BlazorAdminThemeMiddleware`); render mode declared by the page
- ☑ One routing system on the Blazor side: the site resolved through the route table, `ContentPage` is "/" only, legacy-owned paths forwarded to MVC (ruling 2026-10-13)

## Parties (parties.md)

- ☑ Legal entity names; parent organization picker; the tenant's own organization
- ☑ Class-as-set connection index; the binding record as the single source; closure-table hierarchy
- ☑ Association content type; bag-element service; contact-point and organization-name indexes
- ☑ NAICS reseed; user-list class filter; Person creation for external logins

## Content items (content-items.md)

- ☑ Picker relationship handlers; hierarchical Category; Fluid sandboxing with data scope

## Architecture (architecture.md)

- ☑ X3: "Crest" only in module and package names (~200 renamed 2026-10-13; stored names, platform clashes and the component library kept — list in architecture.md › Naming)
- ☑ #5 standalone host test (`test/Crest.Tests.Standalone`)
- ◐ #6 generic pieces into Crest: currency option source, money DTO, exchange-rate seam, `Crest:DataRoot`, gap-free numbering, `dev-lib.sh`, workflow roles done; `KindCatalog<T>` + kind menu done, kind "All" page/pane open; workflow browser checks and the content-part-list import not started

## Other

- ◐ Branding: renditions and settings ☑, `branding.*` in the site query ☑, settings editor group ☑, head favicon and login/tenant-chip logos ☑; left: the shipped Login template and login as a workflow action (the admin and member shells do not render data-defined pages yet), the site theme's layout and email bindings (branding.md)
- ☑ Denomination (types.md)
- ☑ Queries index and SQL runner, Deployments pages as Blazor pages (admin-routes.md)
- ◐ Regions: maps as runtime rules ☑; postal codes and boundaries: the loaders ☑, the rows wait on the data-sourcing and licence decision (regions.md)
