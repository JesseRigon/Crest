# Backlog: decided but not built

Items the docs record as decided, with no open question against them, that the code does not
have yet. Each line names the doc that holds the design; that doc is the spec. Mark an item
done here when it lands, and update its doc to describe what exists.

Left out on purpose, because a question is still open against them: the connection system
(the protocol modules, the data layer's SQL connections, queries.md › Connections) and the
Queries API (queries.md Q6, Q7; workflows.md "what the platform offers for connections"),
audit feeds at volume (A4), the designer UI and its move (blazordesigner.md, workflows.md W6),
design systems as content (part vs fields), cross-tenant translation suggestions, the global
editing UI (which UI first), the geocoder, approvals, the option picker's home (server.md S2),
shared screens' defaults and setup (MVC/views.md), shells on their own hostnames, user
settings, and the parked security audit and sandboxed custom elements (blazor-display.md).

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

## Registries (architecture.md › Modules declare, Crest.Server registers and serves)

- ☐ **One page registry** (admin-menu.md › The page registry): each page declares its route,
  shell, permission and menu placement (parent path, caption, position, icon) once; the route
  table, the route gate and the admin menu's default tree derive from it. The per-module
  `IRoutePermissionProvider`s, the navigation providers for native pages and
  `IWebAssemblyRouteProvider` retire. The admin menu becomes a view over the registry plus the
  defined layer (tenant renames, hides, moves, custom placements and links, by stable entry id)
  plus the user hide layer; the caption-matching provider import and sync go. Page permissions
  per tenant and per user resolve through the access model, one permission for menu and gate.
  Legacy MVC pages are declared entries pointing at their legacy route until ported; dynamic
  items (New › *content type*) are a registry source.
- ☐ **The dual-layer API registry** (MVC/api.md P5, P15): the declared layer from module code
  through `IApiContributor`, built at shell build and following feature enablement; the defined
  layer from data through a dynamic endpoint data source invalidated on publish. The workflow
  engine's `HttpEndpoint` routes leave its private `IRouteTable` and become defined entries;
  generated operation endpoints (P14) are defined entries too.
- ☐ **APIs out of Crest.Server** into their declaring modules, registered through
  `Crest.Server.Abstractions`; Crest.Server keeps only the hosting plumbing (server.md § 1,
  MVC/api.md P1-P9).

## Access (access.md, permissions.md)

- ☑ Anonymous actors: machine identity and roles on connections, the "accepts anonymous runs" flag, login and registration triggers run as the user the event names
- ☑ Members onto the extension system (resource mapper, scope provider on the org binding index, `OrganizationScopeProvider` out of Server); member admins through `ManageOrgMembers`
- ☑ Parties onto the extension system (mapper, scope provider, route permissions off `ICrestRoutePermissionProvider`)
- ◐ Page and route gates move to Access (`ICrestRoutePermissionProvider`, `CrestRouteAuthorizationService`, `AssignmentScopeProvider`; `AdminFilter` goes). Left: `AdminFilter` (goes with the MVC admin). `AssignmentScopeProvider` is retired
- ☑ Admin controllers' per-type content authorization goes; the scope is the filter (content-items list; the MVC list goes with the MVC admin)
- ☑ Version bumps only for rights writes; implied-by resolved from the permission instance; the revocation check run (the check is in the suite; it runs with the tests at the end)
- ☑ Recorded defects: SSRF guard on messaging and HTTP activities, per-definition narrowing of engine instance reads, `UsersController` scoped. Instance-read narrowing is the View list
- ☑ Per-part permissions as scope providers (permissions.md): the seam; the first rule comes with the first type that needs one
- ☑ The permission model (permissions.md): sets, groups, overrides, the projection, the editors, Fruitful's groups folded in (Fruitful compiles after the repack)
- ☑ Who manages the permission model and the organization owner (permissions.md): CRUD sets, the hierarchy bound, the default hierarchy, the owner record, protection, owner-locked ceiling, transfer. The owner panel is on the admin organization page; staff with ManageOrgMembers reach every organization

## Display (blazor-display.md, widgets.md)

- ☑ Widget replacements (widgets.md): rich text, figure, forms, validation, search box, the per-item page body (`DisplayTreePart`), visibility rules, the captcha, `FacebookPlugin` and the media picker editor
- ☑ Step 1 leftovers: route scanner reads the catalogs, page regions as zone keys, editor facet entries, primitives annotated (icon, markdown, layout hosts, colour and media editors; the form editors stay unregistered as a parallel set, reason in blazor-display.md)
- ◐ Step 2 leftovers: tree cache (versioned and validated, see decision 12; the unfiltered cache is left); display events for bound nodes ☑
- ☐ Razor → Blazor component parity audit: every Razor view, shape template and widget has a Blazor component equivalent, reusing `Crest.Components` (blazor-display.md step 7; MVC/views.md, MVC/shape-engine.md, widgets.md)
- ☐ The Liquid templates updated to display trees and optionalities (blazor-display.md step 7)
- ◐ Step 7 remainder: content displays (fields with localized labels, title, bag, list with its pager, taxonomy, geo point) and editors (BagPart, user picker) on the tree; MVC Menu admin, audit content screen and Flows BuildEditor deleted; left: the ContentFields/Flows edit views (still rendered by the MVC Contents Create/Edit and the Demo module), LiquidPart (HTML by design), admin-only shapes (go with TheAdmin), the MVC screens (~1,250 Razor views)
- ◐ Decision 6: zone keys as registry entries with a schema; single or multiple instances per page: keys, contributions, validation and the single-instance check built; left: priority reordering through overrides and a `Zone` component node (designer zone settings)
- ◐ Decision 9 / § 6a: designed pages (page record and route-table provider): record, store, API, recipe step, data route provider and site rendering built; admin and member pages are not routed (no shell component renders a data-defined page)
- ☑ Decision 10: the default permitted-fields `Content` template
- ◐ Decision 12: client ETag trees, unfiltered server cache: ETags, client-held trees and the shared anonymous cache built; the unfiltered per-page cache needs the builder's per-viewer decisions moved into the node filter
- ◐ § 10: per-page permission sets (Design, Schema, Data, Publish): per designed page built; Publish per component waits for component drafts
- ◐ Module homes: `Crest.Data.Types` ☑ (registry; readers not wired), `Crest.Global` → `Crest.Data.Global` ☑ (Fruitful needs the repack), `Crest.Components.Primitives` / `.Templates` skipped (open question in blazor-display.md)
- ◐ § 12: one expression engine: the engine's Liquid runs through the platform's, one Fluid version; `Crest.Scripting` removal waits for workflows.md's Retire step, limits wait for the parked sandbox

## Workflows (workflows.md)

- ◐ Operations step 1 remainder: caller lifetime rules for circuits and websockets ☑, read logging as a tenant setting ☑, conformance fixtures for organizations and policies ☑. Left: OpenId validation in the host recipe (the same work as the Machine actors line above)
- ◐ Step 3: typed per-type content operations ☑ (Copy/Move fields write through them). Left: one registry (`WorkflowRegistryCatalog` folded) and per-tenant operation names, both waiting on a decision (workflows.md › Tasks › 3)
- ☑ Step 4: queue partitioned by correlation id, idempotency keys (unit id + slot); the object version in the payload is not built (no raiser names one)
- ◐ `ICrestAntiforgery`; the hook attachment page ☑ and webhook insight; per-process metrics. Left: `ICrestAntiforgery` (comes with the designer move, W6 open); webhook insight and per-process metrics (as audit feeds, which wait on run history in audit)

- ☑ The View list (`Crest.Access.View`): the ceiling, the access API, the engine API instance operations decided per definition, the instance list narrowed (workflows.md › Ownership and permissions; access.md defect closed). Left: Studio's access editor (with the designer UI list)

## Audit (audit.md)

- ☑ Steps 1–4: impersonation actor, session and impersonation events, non-content write events, the audit index, content history as diffs with checkpoints, workflow run history in audit, audit pages
- ◐ Step 5: feeds' data side (indexed record, parties, owner, organization, both attributions) built; feed definitions as saved queries wait on A4

## Sharing (sharing.md)

- ☑ Phase 3: business roles and the role registry (`RoleKind`, derived roles)
- ☑ Phase 4: Share panel, rules screens, access indicators, derived-role picker
- ☑ System accounts; delete/move manager checks; comments; items following a folder chain

## Media (media.md)

- ☑ CDN URL signers; Azure and S3 storage on the file system (not verified against live cloud accounts or CDNs)
- ☑ Private resize cache; multi-path inheritance; organization and personal drives created automatically; `FileDriveSettings` screen
- ☑ Module-generated files through the pipeline with exemptions
- ◐ Drive-aware picker; Secure Media mapping gaps (done: the Blazor media field editor and both gaps; left: the classic Razor media picker and the HTML/Markdown editors' insert still use library paths)
- ☑ Portal notifications; viewer pane and add-on seam; drive browser

## Shells and themes (shells-and-themes.md)

- ☑ A disabled module is absent per tenant; theme clients and UIs load per shell
- ☑ Member password-reset and external-login pages; side-checking login gate
- ☑ Organization slugs in paths and an organization picker; one device-local tenant switcher; impersonation under one account
- ☑ Shell registry (replaces `RouteBucket` and `BlazorAdminThemeMiddleware`); render mode declared by the page
- ☑ One routing system on the Blazor side: the site resolved through the route table, `ContentPage` is "/" only, legacy-owned paths forwarded to MVC
- ☑ Step 7 waves 4–5 (shells): native tenant create/edit/setup; OpenId applications, scopes, server and validation; account security, email confirmation, external sign-in completion, OpenId consent/logout and per-shell error pages as Blazor pages; email bodies as templates. Setup is open (MVC/views.md § Setup); SiteTheme's `Layout.cshtml` and the staff login, registration, password reset and two-factor screens stay until MVC/views.md wave 2
- ☑ Step 7 waves 1 and 3 (architecture): native Email/SMTP with test send, audit trail settings, background tasks (run now), notifications list, role clone, user unlock/confirm/verification/external sign-ins, content clone/save draft/discard draft/versions, user registration/reset/change-email settings, custom settings on the editor tree, the content definition editor (types, parts, fields; settings as JSON) and index profiles; the MVC controllers, drivers and views they replace deleted (blazor-display.md › step 7). Left: Email.Azure settings page, the Users list shapes/filters (unused), the index providers' profile drivers, structured settings forms, browser checks for these pages

## Parties (parties.md)

- ☑ Legal entity names; parent organization picker; the tenant's own organization
- ☑ Class-as-set connection index; the binding record as the single source; closure-table hierarchy
- ☑ Association content type; bag-element service; contact-point and organization-name indexes
- ☑ NAICS reseed; user-list class filter; Person creation for external logins
- ☑ Role bases (organization and person roles), Individual organizations, portal promotion, organization-derived business roles, organization-role transaction parties
- ☑ Assignments retired; organization ownership part, index and scope moved into Crest.Parties (access.md › Members, server.md S3)
- ☑ Parties UI: add role, Individual badge and conversion, owner panel, invite to portal, settings section

## Content items (content-items.md)

- ☑ Picker relationship handlers; hierarchical Category; Fluid sandboxing with data scope

## Architecture (architecture.md)

- ☑ X3: "Crest" only in module and package names (stored names, platform clashes and the component library kept — list in architecture.md › Naming)
- ☑ #5 standalone host test (`test/Crest.Tests.Standalone`)
- ☑ #6 generic pieces into Crest (the workflow browser checks are written, not yet run)

## Other

- ◐ Branding: renditions and settings ☑, `branding.*` in the site query ☑, settings editor group ☑, head favicon and login/tenant-chip logos ☑; left: the shipped Login template and login as a workflow action (the admin and member shells do not render data-defined pages yet), the site theme's layout and email bindings (branding.md)
- ☑ Denomination (types.md)
- ☑ Queries index and SQL runner, Deployments pages as Blazor pages (admin-routes.md)
- ◐ Regions: maps as runtime rules ☑; postal codes and boundaries: the loaders ☑, the rows wait on the data-sourcing and licence decision (regions.md)
