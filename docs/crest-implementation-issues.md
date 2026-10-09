# Crest implementation issues — decisions to make

A feasibility audit (2026-10-06) of the plans in these docs against the current Crest code and
the OrchardCore fork (`venti/modules/OrchardCore`). **Nothing found needs a change to
OrchardCore**: every hurdle is solvable in Crest through public seams.

**How to use this file.** Each open issue below has the context, the options and a
recommendation, then an **Answer** space. Answer in the file; the rulings are then moved into
their home docs and the issue leaves this file.

## Already ruled (moved to their home docs)

| Area | Ruled |
| --- | --- |
| Cross-cutting | X1 reset dev tenants until the first public release, upgrade steps after; X2 cleanup done; the naming rule ("Crest" only in module names) |
| Members | M1–M4 side and organization carried by every request (`X-Shell`, `X-Org`, organization slug in member URLs; missing marker denied); M5 switcher tenants are the accounts added on the device; M6 connection index `(UserId, OrgId, Class)`; M7 add/remove class; M8 one cache per session (tenant-set length, default 7 days), offline security; M9 closure table on YesSql; M10 associations as content items; M11 impersonation; M12 tenant-organization check; M13 permission editor summaries |
| Shells | S1 (with M1); S3 a disabled module is absent; S4 every tenant reserves the member prefix; S5 (with M5); S6 theme compatibility by longest module id; S7 the server provides the tenant base; S8 theme clients load per shell; open question on per-shell hostnames and servers recorded in shells-and-themes.md |
| Queries | Q1 permissions injected at run time, Power Query-style builder in the tenant shell, search engines as connections, external systems keep their own permissions; Q3 permission checked everywhere; Q4 paging, streaming as a special case, column types by aspect, shape fixed at build time; Q5 one assembly, protocols as features |
| Workflows | W1 new purpose and document names, dev tenants reset; W2 rename now (`ManageConnections`); W3 retries in the connection system; W4 connector registry to Queries; W7 and W9 dropped; W8 workflow history onto the audit system |
| Parties | P1 (with X1); P4 one index per bag element type; P5 one bag-element service, organization type is legal form only; P7 picker relationship handlers; P9 full NAICS as a global list; P10 contact writes publish |
| Media and audit | F6 resumable uploads land in quarantine; F7 diff check against stock; F8 cleanup done; A1 index on stock AuditTrail (fork question reopened in audit.md); A2 diffs with compressed checkpoints; A3 stock trimming, retention policies are Fruitful's; A5 both attributions first-class |

---

## Cross-cutting

### X3. Existing names that carry "Crest" (medium)

**Context.** Ruled: "Crest" appears only in module, assembly and package names, so the project
can be renamed by renaming its modules. Today about 320 types are named `Crest…`: services
(`CrestLoginService`, `CrestRouteAuthorizationService`, `CrestIconController`, …), the UI
component library (`CrestButton`, `CrestDataGrid`, `CrestTextBox`, …), and API routes sit under
`api/crest/`. Claim types, settings keys and permission names also carry it in places.

**Options.**
1. Rename everything now, before the first public release: internal types, the UI components
   (e.g. `CrestButton` → `Button` in a `Crest.Components` namespace), the route prefix
   (`api/crest/` → `api/`), claims and settings keys.
2. Rename internal types, claims, settings and permissions now; keep the UI component names
   and the route prefix, treating them as the product's public surface.
3. Rename only as code is touched, enforcing the rule for new code.

**Recommendation.** 1, before the first public release, while it is a mechanical change with
no outside users. Component names that would collide with HTML or Blazor names (`Button`,
`Form`) keep a short neutral prefix rather than the project name, decided per component.

**Answer:**



### A0. Fork AuditTrail after all? (medium)

**Context.** Ruled earlier: no fork — a Crest index on Orchard's stock AuditTrail. Two later
rulings may need changes to the stock save path: content history stored as diffs with
compressed checkpoints (instead of a full snapshot per save), and the workflow engine's own run
journal moving onto the audit system.

**Options.**
1. Stay unforked: replace the stock Contents audit handler with a Crest handler that writes
   diffs, and record workflow history through the stock `IAuditTrailManager` as ordinary
   events.
2. Fork AuditTrail (same module id, as with Media) to own the save path and storage.

**Recommendation.** 1 until a concrete need forces the fork: both rulings fit through the
stock handler seams.

**Answer:**



## Shells and themes

### S2. `/wfiso/_framework/dotnet.js` served with an empty MIME type (high, a bug)

**Context.** Under a tenant addressed by path prefix (`/wfiso/…`), the Blazor runtime file
`dotnet.js` comes back with no content type, so the browser refuses it and the admin shell
does not boot. The cause is unknown: something other than the static-asset endpoint answers
(Orchard's per-tenant static files, the host's `UseStaticFiles`, or a fingerprint mismatch).
`App.razor` also has no `<ImportMap/>`.

**Options.**
1. Diagnose now: compare response headers on prefixed and unprefixed URLs, in development and
   published builds, then fix the actual cause.
2. Add `<ImportMap/>` and map `_framework/{**path}` as a tenant endpoint with a content-type
   provider that knows `.js` and `.wasm`, without diagnosing first.

**Recommendation.** 1 — a debugging session, then the fix it points to.

**Answer:**



## Queries and the connection system

### Q2. Search sources and the old query contract (medium)

**Context.** Ruled: search engines (Lucene, Elasticsearch, Azure AI Search) are connections,
and a Search module runs through Queries the standard way. Orchard's search modules today
implement the stock query contract (`IQuerySource`: no paging, no caller scope, results in
one list), which lives in a library Crest cannot change.

**Options.**
1. Crest's new contract (paged, cancellable, typed, scoped) sits beside the stock one; the
   query manager prefers it and wraps stock sources. Each search engine gets a Crest
   connection adapter implementing the new contract, so search is scoped like everything
   else; the stock sources stay only for compatibility.
2. Keep the stock search sources as they are, usable by admins only, until a search engine
   is set up with the permission system.

**Recommendation.** 1, with 2 as the interim: until a search engine's adapter applies
permissions, its queries are admin-only.

**Answer:**



### Q6. Host database versus external SQL connections (low)

**Context.** The SQL connector is dogfooded against the host database. That connection must
use the tenant's table prefix, the store's isolation level and caller-scope injection;
external SQL connections must not be prefixed or rewritten for the tenant, and use their own
credentials.

**Options.**
1. A connection kind: host or external, set by the system, not editable.
2. One kind, with prefixing and scope as per-connection settings.

**Recommendation.** 1 — the host connection is created by Crest and cannot be pointed
elsewhere; every other SQL connection is external.

**Answer:**



### Q7. Crest's Queries API (low)

**Context.** `QueriesController` writes a query's settings as raw JSON without the stock
source handlers (so nothing validates them), lists only stock-contract sources, and has no
run, preview, results or schema endpoints — which the builder needs.

**Options.**
1. Each source validates its own settings through the new contract; the API lists sources of
   both contracts; add run, preview, results and schema endpoints for the builder.
2. Keep raw JSON for admins; validate at run time only.

**Recommendation.** 1.

**Answer:**



## Workflows and the connection move

### W5. Background throughput (medium)

**Context.** Connector background jobs run on **one worker per tenant**, so one slow external
call blocks that tenant's other background work. The lock that keeps two units of work off
the same object is **file-based**, so it only works on one server. Poll state is **one
document per tenant** updated without a lock, so two polls at once can lose an update.

**Options.**
1. Fix the poll-state race now (a record per connection, or a lock); decide worker count and
   a database-backed distributed lock when there is a second server or real traffic.
2. Decide everything now: several workers per tenant and a database-backed lock from the
   start.
3. Leave all of it until measured.

**Recommendation.** 1 — the race is a bug; the rest is capacity, decided when measured
(tracked in [speed.md](speed.md)).

**Answer:**



### W6. Moving the designer onto Crest components (high, size)

**Context.** Ruled: the workflow designer (the forked Elsa Studio) moves onto Crest
components last, keeping what the Elsa designer UI offers; the canvas and code editor stay
foreign. The audit found 104 of 119 designer files use MudBlazor (the plan counts 98). Studio
ties editing to the publish link (without it, edit is read-only). MudBlazor is loaded only
when the designer opens (about 2.6 MB), so the cost of keeping it is two UI stacks and a
designer not themed, localized or tested like the rest of Crest — not startup speed.

**Options.**
1. Staged, as planned: shared pieces first (property-panel hints and shared components, 37
   files), then the editor, then the list and instance viewers; MudBlazor's theme reads
   Crest's tokens until it is gone.
2. Keep MudBlazor permanently, themed from Crest's tokens; build only new designer pages on
   Crest components.
3. Rewrite the designer on Crest components from scratch, reusing only the canvas.

**Recommendation.** 1, still last. The new pages (hook attachment, hook and webhook insight,
per-process metrics) are built on Crest components now, independent of the move; the
edit-without-publish fix comes in the first stage. If the move stalls, it stops at option 2.

**Answer:**



## Parties

### P2. Code switches on contact kind keys (high)

**Context.** Ruled: one kind list with a hidden type per kind (phone, e-mail, web, social,
messaging, other). Today `PartyMapper` and the primary-contact rules hard-code the e-mail and
phone/mobile kind keys; "social" and "messaging" are kinds now but become types; contact
values are only checked for being non-empty; a kind without a type becomes "Uncategorized"
and would not roll up; the data lock is enforced in the controller, not the service or seed.

**Options.**
1. Code reads a contact's type, never its kind key; every seeded kind gets a type with a data
   lock; values are validated per type; a kind without a type is refused; the lock is
   enforced in the service.
2. Keep kind keys in code; types only for reports.

**Recommendation.** 1.

**Answer:**



### P3. Preferred contact methods versus types (high)

**Context.** Ruled: preferred contact methods are an ordered list on a Person (e.g. text, then
e-mail). But "text" and "call" are both type "phone", so the preference is about a channel or
action, which the type list cannot express. And with finer kinds (landline vs mobile),
"preferred within a kind" changes meaning.

**Options.**
1. A small locked list of channels (call, text, e-mail, messaging, …) and an ordered picker on
   Person; each channel says which contact types can serve it.
2. Order the person's own contact points directly (this number first, then this address).

**Recommendation.** 1 — the preference survives a changed number, and "text" can pick the
person's preferred mobile.

**Answer:**



### P6. The tenant's own organization (high)

**Context.** Ruled: it exists before the first user (created during tenant setup), is
referenced from site settings, and members on it are a tenant setting (default blocked).
Still open:
- positions, role pickers and the Organizations list would show it like any customer;
- its addresses overlap the tenant's Locations;
- `TaxIdentifier` is one value, but a business can have several tax ids;
- its title is a second name beside `ISite.SiteName` that can drift.

**Options.**
1. Show it, marked "your organization", where relevant and exclude it from customer-facing
   pickers; Locations are premises, the organization's addresses are legal and mailing;
   tax ids become a typed list (like legal names); the title follows `ISite.SiteName`
   (editing one updates the other).
2. Hide it from all lists; keep one tax id; title and site name independent.

**Recommendation.** 1.

**Answer:**



### P8. Party merge cannot find references (medium, later)

**Context.** Merging two parties must rewrite every live reference to the merged one. They
are spread over other modules' role pickers, bag-contained positions, portal user links,
member bindings and site settings, and nothing lets a module declare how its types reference
a party. Merge itself is designed later.

**Options.**
1. A party-reference provider: each module registers how its types reference a party; merge
   asks every provider.
2. One index of every picker pointing at a party, maintained on save.

**Recommendation.** 1, designed with merge.

**Answer:**



## Media and audit

### F1. Paths versus ids (high — decide before the file system)

**Context.** Orchard's `MediaField` stores only file **paths**, and everything around it
works in paths (Liquid `asset_url`, shortcodes, SEO, search indexing, GraphQL, Crest's media
API). A rename or move breaks stored paths, and with hard links one file has several paths.
media.md says links address ids, yet `/media/...` URLs come from the tree.

**Options.**
1. Paths become stable aliases: a path → placement table, with redirects on rename and move,
   resolved inside the forked file store. Old content keeps working.
2. Placement URLs carry the id; stored paths are rewritten (dev tenants reset, per X1).
3. Both: new content stores ids, old paths resolve through the alias table.

**Recommendation.** 2 for stored data (fields store placement ids) plus 1 for incoming URLs
(old and pretty paths redirect to the placement), since dev tenants reset anyway.

**Answer:**



### F2. Serving, resizing and the CDN assume public files by path (high)

**Context.** Orchard serves `/media/...` through middleware that resolves the path, resizes
images and serves static files, with a per-top-folder role check for secure media. Private
bytes must never land in the public resize cache.

**Options.**
1. The access check runs first, before resolution and resizing; public files keep the fast
   path and CDN caching.
2. Crest's own resize cache, keyed by placement and size, behind the same check.

**Recommendation.** 1, with 2 for private files (public resized copies stay cacheable).

**Answer:**



### F3. Access-index size and recalculation (high)

**Context.** Access is precomputed per placement and principal. Rules read metadata and time,
a rule high in the tree recalculates its whole scope, hard-linked folders multiply inherited
chains, and removing access must take effect before it is reported done.

**Options.**
1. Store only explicit and rule decisions; resolve inheritance through an ancestor closure (a
   join, not a row per inherited pair).
2. Recalculate in batches in the background, with a deny-first fence so removals apply at
   once.
3. Hard links for files only in the first version.

**Recommendation.** 1 and 2 together, with folders allowed to hard-link (as ruled) once 1
exists.

**Answer:**



### F4. Folder hard links and the loop gate (medium-high)

**Context.** With folder hard links the tree becomes a graph: the loop gate must check every
placement's ancestors, two moves at once can race into a loop, and folder counts and quotas
become ambiguous (one folder in two places).

**Options.**
1. Serialize placements and moves per tenant (one at a time).
2. Check a precomputed ancestor closure inside the same transaction.

**Recommendation.** 2, with 1 as the guard against races; quotas count content once,
whatever its placements.

**Answer:**



### F5. Search filtering (high)

**Context.** Ruled: search engines are connections and a search engine indexing Crest data
must be set up with the permission system. That means writing access into what is indexed
and filtering every search; each principal or rule change reindexes the affected documents.

**Options.**
1. One Crest search path (the Search module through Queries) with the filter always applied;
   direct index access forbidden.
2. Coarse filtering in the index plus an exact check afterwards — which breaks exact counts,
   so only where counts don't matter.

**Recommendation.** 1.

**Answer:**



### A4. Audit feeds at volume (high)

**Context.** Stock audit listing counts every page and pages by offset, which slows at volume,
and it is admin-only. Feeds need keyset paging (by time and event id), and permission
injection that no caller can skip — which comes from the query fork.

**Options.**
1. Build the feed API directly on YesSql now (keyset paging, scope applied), and move it onto
   the query system when the fork exists.
2. Wait for the query fork and build feeds only on it.

**Recommendation.** 1.

**Answer:**


