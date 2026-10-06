# Crest implementation issues — decisions to make

A feasibility audit (2026-10-06) of the plans in these docs against the current Crest code and
the OrchardCore fork (`venti/modules/OrchardCore`; `/workspaces/OrchardCore` is an older copy
of the same fork). Six areas: members, shells, queries, workflows and connections, parties,
media and audit. Each issue says what is hard, where, and the options; each needs a ruling,
taken one at a time like the plan reviews. When an issue is ruled, the ruling goes into its
home doc and the issue leaves this file.

**Headline: nothing found needs a change to OrchardCore.** Every hurdle is solvable in Crest
through public seams (module forks keeping stock ids, index providers, cookie events,
authorization handlers, `ILoginFormEvent`, `IShellHost`, `IDbConnectionAccessor`). One design
blocker (M1) and several high-severity decisions remain.

Severity: **blocker** — the ruled design cannot be built as written; **high** — needs a
decision before its work starts; **medium** — a decision, or a known cost; **low** — a
cleanup.

## Order of work these suggest

1. Members: side resolver, claim stripping, side entry checks (M1–M3) — everything in
   "one account, both sides" hangs off them.
2. The tenant switcher privacy bug (M5) — a bug today, independent of the new design.
3. Queries: the scope design (Q1) together with the audit index (A1) — audit feeds depend on
   both.
4. Connections into Queries: names and migration first (W1, W2), then retries (W3).
5. Media: paths versus ids (F1) before any file-system work.
6. Parties: migration policy (P1) before reseeding anything.

## Cross-cutting

- [ ] **X1. Migration policy (high).** Crest migrations are "fresh-install repeatable, no
  UpdateFrom steps — a dev tenant is reset" (`Crest.Parties/Server/Migrations/PartiesMigrations.cs`;
  [architecture.md](architecture.md): "Pre-release: no compatibility code"), and list seeding
  only adds new keys, never updates (`Crest.ContentPartLists/Services/CrestContentPartListRules.cs`
  `PlanSeed`). Many rulings change shipped data: contact-kind types, the NAICS reseed, the
  class-as-set index, moving connections out of Workflows, the hierarchy rewrite. Options:
  (a) keep "reset dev tenants" until the first public release and say so in each plan;
  (b) add deferred backfill tasks per change; (c) start `UpdateFrom` steps now. The first
  public release is the line after which (a) stops being possible.
- [ ] **X2. Stale paths and plan references in code and docs (low).** Code comments cite
  `plans/*.md` (no plans folder exists); several docs cite `/workspaces/OrchardCore` instead
  of the fork in `venti/modules/OrchardCore`; `workflows.md` and `shells-and-themes.md` link
  to themselves for open work; `BlazorAdminThemeMiddleware` comments mention "Phase 8" and a
  `LegacyHost`. One cleanup pass.

## Members — "one account, both sides"

- [ ] **M1. Nothing in a request says which side it is for (blocker).** The class ceiling
  (`Crest.Members/Server/Services/MemberPermissionCeiling.cs`) fails on the single
  `UserClass` claim baked at sign-in, not on the request. The member WASM calls
  `/api/crest/...` and content APIs with no shell prefix
  (`Crest.Members/member-wasm/Services/MemberShellContext.cs`), and
  `BlazorAdminThemeMiddleware` strips `{shellBase}/api|_framework|...` and returns before it
  stamps the shell bucket. Orchard MVC endpoints never pass through Crest's middleware, so
  the side must be set at the cookie-validation layer, before any authorization handler.
  Options: (a) a side marker on every request — each shell's HttpClient sends a header,
  validated and turned into a side claim; a missing marker for a dual user resolves to the
  narrow side or is refused; (b) separate API route families per side; (c) a side-scoped
  cookie (contradicts the one-cookie ruling); not Referer/Origin sniffing.
- [ ] **M2. Staff role claims leak into the member side (high).** The one cookie carries the
  dual user's staff role and permission claims; `MemberCookieEventsConfiguration` only adds
  binding claims. The admin role makes Orchard's super-user handler succeed on everything,
  and the ceiling blocks only ceilinged names. Options: on a member-side request strip role
  and permission claims from the principal and add only the binding's (after Orchard's
  security-stamp refresh, which copies claims additively); or turn the member-side ceiling
  into an allow-list. On the staff side, omit org claims.
- [ ] **M3. Side entry is checked only at sign-in (medium).** With one cookie, any signed-in
  staff-only user can open member pages, because `MemberLoginSurfaceGate` runs at login only.
  Entry checks ("holds staff" for admin, "has a binding" for the portal) belong in the M1
  resolver, per request.
- [ ] **M4. Active organization is per cookie, not per tab (high).** `MemberSessionService`
  writes the active org into the ticket and re-signs the cookie, so switching org in one tab
  switches every tab, and the re-sign races parallel requests. Options: accept "last switch
  wins" and document it; or carry the active org in the per-tab side marker (M1), validated
  against bindings, with no cookie write. Either way Orchard's cache ticket store must be on
  (it is optional in the Users manifest); assert it.
- [ ] **M5. The tenant switcher matches by username and boots every tenant (high — a bug
  today).** `Crest.Server/Controllers/AppController.cs` `GetAvailableTenantsAsync` runs on
  every manifest load, opens a scope on every tenant (starting dormant shells) and lists a
  tenant when any user there has the same **username** — another person's account shows as
  yours. Options: link accounts by a verified identity (email, or the tenant-SSO subject),
  cached in the Default tenant; scan only running shells; build the list lazily when the
  switcher opens. Organizations for the unified switcher come cheaply from bindings. The
  switch itself ignores `RequestUrlHost` (host-based tenants break), offers admin only, and
  does not re-authenticate across tenants (see S5).
- [ ] **M6. Class becomes a set (medium).** `UserClassIndex` holds one `Class`; the POCO
  holds one value; `MemberService`, `UserClassConversionService` and
  `MemberImpersonationService` query `Class == Member`; `MembersMigrations` is at version 1.
  Options: a multi-row map index (one row per class, indexed on UserId + Class; watch
  duplicate rows in joins) with a reader for old documents; or flags in one column (loses a
  clean WHERE). The class claim becomes multi-valued or gives way to the side claim.
- [ ] **M7. Conversion contradicts the ruling (medium).** `UserClassConversionService` and
  `{userId}/convert-to-staff` clear bindings and allow member→staff only; the ruling is
  add/remove a class, keeping bindings when staff is added. Code and the built-state
  sections of [members.md](members.md) need updating together.
- [ ] **M8. Per-request cost of enrichment and access policies (medium).**
  `MemberCookieEventsConfiguration` already runs a member query and role-claim lookups on
  every authenticated request with an active org; the access-policy seam would add more.
  Options: a short-TTL per-(user, org) cache invalidated through a version token the policy
  modules bump; or evaluate policies at sign-in and org switch only, plus a validity
  timestamp. Define "session load" as "first request after the TTL". A refusal clears member
  claims without signing the user out of the staff side.
- [ ] **M9. Relational hierarchy rewrite (medium-high).** `UserHierarchyService` is a
  node-per-binding tree with a materialized path, written outside the ambient session with
  Dapper. Several managers make it a DAG: rewrite to manager→report edges plus a closure
  table (ancestor, descendant, depth, root); inserting an edge adds the cross product of
  ancestors and descendants; deleting needs recomputation (several paths) or path counts;
  the cycle check runs in the same transaction, and concurrent edge writes need serializable
  isolation or a per-root lock (SQLite deadlocked before). The chain becomes every path; the
  member's place under the org root needs rethinking; `HierarchyPathMath` goes. A rebuild
  command doubles as repair.
- [ ] **M10. Associations against the root guard (medium).** Associations cross staff and an
  organization, so they cannot sit in the root-guarded closure table. Options: a separate
  associations table (type, from, to) with no root column and visibility only through a
  per-type resolver — keeps the reports-to guard intact; or content items (slower on the hot
  path). Locked module types plus an editable tenant list is a normal registry pattern.
- [ ] **M11. Impersonation under one account (medium).** `MemberImpersonationService` requires
  the target to be class Member; impersonating a dual user would carry staff claims unless M2
  strips them; `StopAsync` re-signs with empty properties, losing the staff session's state.
  The side marker must say "member" during impersonation, and audit records both people.
- [ ] **M12. The tenant's own organization has no members (low).** Needs a guard in the
  binding writes, the stamp path and the portal register endpoint (which accepts any
  Organization), via a Parties seam that identifies the tenant's organization (see P6).
- [ ] **M13. Permission editor details (medium, scope).** The effective per-module summary
  must show the admin role as "everything" (Orchard's super-user handler grants without
  permission claims) and be computed per side for dual users.
- [ ] **M14. Stale constants (low).** `MembersConstants.Routes` still describes portal pages in
  the admin bucket at `/members/login`; the code has a separate member shell.

## Shells and themes

- [ ] **S1. API calls lose the side (high — same root as M1).** One fix serves both: decide
  M1, then have the strip branch in `BlazorAdminThemeMiddleware` stamp the shell bucket for
  observability (not as the trust boundary).
- [ ] **S2. `/wfiso/_framework/dotnet.js` served with an empty MIME type (high, cause
  unknown).** Under a url-prefixed tenant `dotnet.js` and `.wasm` rely on `MapStaticAssets`
  in the tenant pipeline; something else answers (candidates: Orchard's per-tenant static
  files, the host's `UseStaticFiles`, a fingerprint mismatch). `App.razor` has no
  `<ImportMap/>`. Options: diagnose with headers on prefixed and unprefixed URLs, dev and
  published; add `<ImportMap/>`; map `_framework/{**path}` as a tenant endpoint with a
  content-type provider that knows `.wasm`.
- [ ] **S3. Every tenant gets every module's pages and assemblies (medium-high).**
  `Crest.Server/Startup.cs` maps every `*.BlazorWasm`/`*.Client` assembly in the output, the
  routing has no feature filtering, and the lazy manifest is built from the host's
  references. A tenant without a module still routes its pages (whose APIs 404) and
  downloads its assemblies. Options: filter the route table and lazy loading by enabled
  features (an assembly→feature map, cached per shell); expose enabled modules in the
  manifest for the client; or accept the over-download (no secret is exposed; the API
  authorizes).
- [ ] **S4. `/members` is claimed on every tenant (medium).** `MemberRouteComponentTableProvider`
  is registered unconditionally and the middleware matches the member prefix with no
  feature check, so a tenant without Members gets member redirects and loses site content at
  that path. Gate it on the Members feature, or move the registration into it.
- [ ] **S5. Switching tenants from the browser (medium-high).** See M5; also the target URL
  must come from the shell settings (prefix or host, absolute when the host differs), and a
  cross-tenant switch needs sign-in in the target tenant (tenant SSO, staff-side only) — the
  unified tenant-and-organization switcher is unbuilt.
- [ ] **S6. Theme compatibility credits other modules' contracts to "Crest" (medium, probable
  bug).** `ShellCompatibilityService` matches assemblies by `StartsWith(extensionId + ".")`, so
  `Crest.Members.Member.BlazorWasm` and `Crest.ContentPartLists.BlazorWasm` count as feature
  "Crest" (always enabled) — every tenant appears to need a member theme. Match by exact
  module id or the assembly's feature metadata.
- [ ] **S7. Member pages under a prefixed tenant compute the wrong tenant base (low-medium,
  probable bug).** `CrestWebAssemblyHost` peels admin/login suffixes off the document base;
  a member base keeps `/t2/members`, so cross-shell links from member pages break under a
  url-prefixed tenant.
- [ ] **S8. One eager bundle for the three theme clients (medium, known).** Admin, site and
  member clients all load first; documented open work, a performance cost.

## Queries and the connection system

- [ ] **Q1. Permission filtering inside admin-written SQL (high — the hard design part).**
  The stock SQL source renders Liquid, parses with a real SQL AST parser
  (`OrchardCore.Queries` `SqlParser`, SELECT-only, rewrites table names with the tenant
  prefix) and runs Dapper with no caller input. Row-level filtering of arbitrary SQL cannot be
  proven. Options: (a) declared scope — templates must use scope parameters, with a validator;
  (b) AST injection — tables in a registered "scoped tables" catalog (audit indexes, the file
  access index) are wrapped with a mandatory predicate built from the caller scope, failing
  closed on unknown tables, subqueries or CTEs; (c) an allow-list of tables and views for
  non-admin callers. Recommended in the audit: (b) + (c), free-form SQL admin-trusted only,
  and audit feeds on a structured filter compiled to SQL rather than free SQL.
- [ ] **Q2. The new contract sits beside the stock one (medium).** Lucene and Elasticsearch
  implement the stock `IQuerySource` from the abstractions library, which cannot change.
  Option: a Crest interface (paged, cancellable, typed, scoped, streaming) that the Crest
  manager prefers, wrapping stock sources (in-memory paging, cancellation between calls).
  Stock sources get no caller scope unless wrapped — mark them admin-only.
- [ ] **Q3. Closing the Liquid/JS permission gap breaks some templates (low, policy).** The
  Liquid `query` filter, `executeQuery` and the Razor helpers execute without a permission
  check. Checking them breaks templates that run anonymously or in background work.
  Options: a "trusted/system" scope flag per query, with a migration note.
- [ ] **Q4. Streaming and typed schema (medium).** YesSql has paging and cancellation but no
  streaming; streaming means reading a `DbDataReader` through `IDbConnectionAccessor`.
  Paging SQL wraps the query as a derived table, which needs an ORDER BY. Column types are
  known only after running (`GetSchemaTable`); cache them on the query.
- [ ] **Q5. Fork mechanics (medium).** Exclude the stock module (`ExcludeAssets="all"`, as for
  Media); keep the area `OrchardCore.Queries`, the `Admin` controller and `Create` action
  (Lucene and Elasticsearch views link to them), the feature ids, the `Sql` source name and
  the `QueriesDocument` type name. Crest code that uses module-assembly types:
  `OrchardCore.Queries.Permissions.ManageQueries` (Crest.Server — switch to `QueryPermissions`)
  and `SqlParser` (copy it into `Crest.Queries.Sql`). Decide whether `Crest.Queries.*` are
  features of one assembly or separate assemblies (separate isolates plugins).
- [ ] **Q6. Host database versus external SQL connections (low).** The dogfooded host
  connection takes the table prefix from the store configuration and keeps the store's
  isolation level; external connections must not be prefixed. A connection-kind flag (host,
  external).
- [ ] **Q7. Crest's Queries API bypasses source validation (low).** `QueriesController` writes
  `Properties` as raw JSON without the stock handlers, and discovers sources only through
  `IQuerySource`. The fork needs per-source validation and discovery across both contracts,
  plus execute/preview, results and schema endpoints for the builder.

## Workflows and the connection move

- [ ] **W1. Stored names tie data to `Crest.Workflows` (high; cheap now).** Secrets are
  sealed with the data-protection purpose `Crest.Workflows.Connections`; changing it makes
  every secret and token undecryptable (and `ReadTokens` swallows the failure). The
  connection and poll-state documents are stored under their type names. Options: a one-shot
  migration that unprotects with the old purpose, re-protects with the new and copies the
  document; or keep the old purpose and type names forever behind a shim.
- [ ] **W2. Routes, permission and config keys are public or persisted (medium).** The OAuth
  callback URL is registered at external providers; `ManageCrestWorkflowConnections` is
  stored in role grants; the `CrestWorkflows:Connectors` config section (`AllowPrivateNetworks`
  is set in venti's dev settings and tests) would silently stop applying. Options: rename now
  with a migration that re-grants, and keep old routes and the old config section as aliases
  for one release.
- [ ] **W3. Retries are the opposite of the ruling (high).** Today the engine retries
  (`ConnectionResilienceStrategy` through `IResilientActivityInvoker`) and the transport sends
  once; `Retry-After` is not honoured anywhere, and a rate-limit refusal is treated as
  non-transient. Options: (a) Queries owns transport retries with backoff and `Retry-After`,
  the strategy is removed and activities default to no engine retry, with attempt counts
  reported back for the journal; (b) keep the strategy and pin transport attempts to one.
  (a) matches the ruling.
- [ ] **W4. The connector registry moves with the connections (medium).** Connector
  descriptors and `IWorkflowConnectorProvider` live in `Crest.Workflows.Domain`; the
  connections controller resolves through Workflows' catalog. They move to Queries' plugin
  registry; the OpenAPI-operation palette entries become a Workflows consumer of a Queries
  listing. Inbound webhooks need a narrow verifier (`IConnectionSecretVerifier`) rather than
  the raw secret.
- [ ] **W5. Background throughput (medium).** Connector jobs run on one worker per shell, so
  one slow call (or transport backoff) blocks the shell's other background work; the object
  lock is file-based (single host); poll state is one document per tenant with an unlocked
  read-modify-write. Decide worker count and locking before connection traffic grows.
- [ ] **W6. The designer move is larger than counted (high, size).** 104 of 119 designer
  Razor files use MudBlazor (the doc counts 98 files). Studio ties editing to the publish link
  (edit without publish shows read-only) — fix in the transition. Options: stage it as
  planned (shared UIHints and components first); or keep Mud permanently behind a
  token-driven theme as the cheaper fallback. The hook page, hook insight and metrics can be
  built as Crest pages now, independent of the designer move.
- [ ] **W7. Engine patch list (medium).** Crest-specific edits to the vendored engine are
  described only in prose; keep a `PATCHES.md` before the next upstream diff.
- [ ] **W8. Insight data gaps (medium).** No per-hook or per-webhook history API and no
  metrics endpoint; inbound webhooks run flows inside the request; rejected posts are only
  logged. A small delivery record, or correlation through the instances.
- [ ] **W9. "Logic reroute" (low).** No Elsa feature by that name; candidates are
  flowchart decision/switch outcomes or retry/fallback paths. Needs the outline's meaning.

## Parties

- [ ] **P1. Changing shipped kinds and seeds (high).** See X1: types on contact kinds, the
  NAICS reseed and the name-kind list cannot reach existing tenants under the current policy.
- [ ] **P2. Code branches on contact kind keys (high).** `PartyMapper` and
  `PartyContactRules.Primary` hard-code e-mail and phone/mobile keys; "social" and
  "messaging" are kinds today but would become types; contact values are not validated
  beyond non-empty; a kind left without a Category becomes "Uncategorized" and would not roll
  up; data-lock enforcement sits in the controller, not the service or seed path. Options:
  key the mapper and primary rules on type; seed Category with a data lock; validate per type
  in `AddContactPoint`.
- [ ] **P3. Preferred contact methods versus types (high).** The ruled preference (text, call,
  e-mail) is a channel/action list, but text and call are both type "phone". And finer kinds
  change what "preferred within a kind" means. Options: a small locked channel list (call,
  text, e-mail, …) with an ordered picker on Person; or order the person's contact points
  directly.
- [ ] **P4. Bag data needs its own indexes (medium).** Stock picker indexes do not descend
  into bags; every queryable facet (contact type, legal-name kind and primary, reports by
  type) needs a Crest map index, one row per element, with Published and Latest columns — the
  proven `PartyPositionIndex` pattern.
- [ ] **P5. Legal names need their own write surface (medium).** The contacts service covers
  exactly two bags. Options: generalize it into a bag-element service parameterized by bag,
  element type and kind list. Overlap: `parties.organization-type` already seeds "DBA", which
  duplicates the "DBA / trade name" name kind — keep organization type as the legal form only.
- [ ] **P6. The tenant's own organization (high).** Creating it needs a deferred task (early
  migrations cannot seed content) writing a site-settings reference; Members needs a Parties
  seam to recognise it (M12); positions, role pickers and the Organizations pane would list
  it; its addresses overlap the tenant's Locations (decide: Locations are premises, the
  organization's addresses are legal); `TaxIdentifier` is a single value; its required title
  is a second name beside `ISite.SiteName` that can drift — decide which one wins or how they
  stay in step.
- [ ] **P7. Parent organization (medium).** Content-picker type restrictions are UI-only;
  validate on the server (the `RequireOrganizationAsync` pattern), refuse cycles in a content
  handler so generic editors cannot bypass it, and add the SQL picker-indexing dependency so
  "children of X" is a query (or own a parent index).
- [ ] **P8. Party merge cannot find references (medium, deferred).** References are spread over
  other modules' role pickers, bag-contained positions, portal user links, member bindings
  and site settings; nothing lets a role declare how it references a party. A
  party-reference provider seam when merge is designed.
- [ ] **P9. NAICS size (medium).** Options live in one list document loaded whole per read;
  the full NAICS set is 1,000+ options and Category is flat. Seed sector level only, or make it
  a global list.
- [ ] **P10. Draft versus published writes (low-medium).** Contact and position writes load
  the latest version and save without publishing; `AddressGeoNodeIndex` has no Published
  column, so tax could read draft addresses; writes are last-write-wins on the whole party.

## Media and audit

- [ ] **F1. Paths versus ids (high — decide before the file system).** Orchard's `MediaField`
  stores only paths, and Liquid `asset_url`, shortcodes, SEO, indexing, GraphQL and Crest's
  `MediaController` all work in paths. A rename or move breaks stored paths, and hard links
  give one file several paths. [media.md](media.md) contradicts itself (links address ids; the
  drive tree serves `/media/...`). Options: (a) paths become stable aliases — a
  path→placement table with redirects on rename and move, resolved inside the forked
  `IMediaFileStore`; (b) placement URLs carry the id and stored paths are rewritten by a
  migration; (c) both — new content by id, old content through the alias map.
- [ ] **F2. Serving, resizing and the CDN assume public files by path (high).** The resolver
  middleware, image processing and static files run per path; Secure Media checks roles per
  top folder. Private bytes must never reach the public resize cache. Options: the access
  check runs before resolution and resizing; or a Crest resize cache keyed by placement and
  size behind the same check.
- [ ] **F3. Access-index size and recalculation (high).** Objects × placements × principals,
  rules over metadata and time, whole-scope recalculation for high rules, hard-linked folders
  multiplying inherited chains, and "removal completes before reporting done". Options: store
  only explicit and rule decisions and resolve inheritance through an ancestor closure (a join,
  not N rows); batch recalculation in the background with a deny-first fence so removals
  apply immediately; allow hard links for files only in the first cut.
- [ ] **F4. Folder hard links and the loop gate (medium-high).** The tree becomes a DAG: the
  gate is an ancestor query across all placements; concurrent moves can race into a loop;
  counts and quotas become ambiguous. Options: serialize placements and moves per tenant; or
  check a precomputed ancestor closure in the same transaction.
- [ ] **F5. Search filtering (high).** Access must be written into search documents and
  filtered in every search path (Lucene, Elasticsearch, GraphQL, Orchard's search module);
  every principal or rule change reindexes the affected documents. Options: one Crest search
  wrapper with direct index access forbidden; or coarse in-index filtering plus an exact
  check — which breaks exact counts, so only where counts don't matter.
- [ ] **F6. Resumable uploads skip scanning (high).** Tus completion writes straight to the
  store (`CreateFileFromStreamAsync`), bypassing `FileCreationService` and its handlers. The
  fork changes completion to write to quarantine with a pending placement, and a verdict job
  commits or rejects.
- [ ] **F7. Maintaining the Media fork (medium).** 139 files, 8 features, about 70
  registrations; deployment steps write paths; the asset-name MSBuild workaround. Keep a
  diff-against-stock check, and decide which stock features to drop (SignalR, remote
  publishing, GraphQL media assets). Crest's `MediaOptionsController` and media-profile models
  use stock types that the replacement must keep. Tenant media icons live under media paths
  (collides with drive files outside the media tree).
- [ ] **F8. "One URL per file" leftovers (low).** After hard links, requirement 75 still says
  the index is per object; media.md says "Audit: reuse, extend" while audit.md says "likely
  fork".
- [ ] **A1. Audit needs no fork — an index on stock AuditTrail (medium, a reframing).** Stock
  `AuditTrailEvent` is one document per event with one index (event, category, correlation,
  user, created). Crest can register its own index provider on the same collection for
  record, parties, owner, organization and impersonated member, and use
  `IAuditTrailEventHandler` for attribution. Fork only to change the save path or admin UI.
  Decide: index on stock, or fork.
- [ ] **A2. Content audit events are full snapshots (medium).** Volume grows with every save;
  parties and owner would be indexed from the snapshot. Consider trimming or compressing.
- [ ] **A3. Trimming loads everything (medium).** Stock trimming lists every expired event
  and deletes one by one, with no per-category retention (retention is an open decision).
  A batched, category-aware trim.
- [ ] **A4. Feed queries at volume and in-query security (high).** Stock admin listing counts
  and offsets every page, admin-only. Feeds need keyset paging (created, event id), a mandatory
  scope wrapper no caller can skip, and they depend on Q1. Option: build the feed API on
  YesSql first and move it onto the query system when the fork exists.
- [ ] **A5. Attribution by audience (low-medium).** Stock sets the user from the request
  principal (the member during impersonation). Store both staff and functional attribution
  on each event and let each feed project the right one.
