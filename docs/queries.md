# Queries — the query system and the connection system

**Status (2026-10-10): the query pipeline landed in its first form** as step 2 of
[workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery): the structured query model compiled through YesSql with
the caller's scope conjoined for every table, the SQL source with parameters only and the
scope rewriter, the typed, paged, cancellable source contract through every caller, the
`System` source (`item`, `user`, `site`), query descriptors and the catalog, every run
paged. Not yet: the builder, statement caching, `ReturnContentItems` through the gate, the
read job, the connection system. Crest's management surface (`api/crest/queries`, the
Queries page) and the platform's `api/queries/{name}` still exist until the generated
surfaces replace them.

One module, `Crest.Queries`, with two parts:

1. **The query system** — querying data simply and safely, enough for Crest's own consumers
   (audit activity streams first) and for downstream modules to extend.
2. **The connection system** — every connection to an external system, by protocol:
   `Crest.Queries.Sql`, `.Rest`, `.Soap`, `.Rpc` and later others. A query is any request
   through a connection, reads and writes alike — a SQL query is a CRUD action on a
   database. The query system is a tool for builders and administrators; business users
   and members never use it directly.

**Ruling 2026-10-06: Workflows' connectors merge into Queries, and Workflows depends on
Queries.** All external actions go through one system; Workflows only does the wiring
inside. See The connection system › Workflows on Queries.

Above them, downstream: the connectors for specific platforms and services (BigQuery,
Zoho Analytics, Fabric, a given REST API, …) are plugins of a downstream reporting module,
with a plugin registry other modules can hook into. Very downstream; not in the first
public release.

> **Ruling 2026-10-09:** the value type registry (field type, editor, display primitive, query
> column type, workflow type per value type) lives in `Crest.Data` (`Crest.Data.Types`); the
> query system reads it. The data-layer module chain (`Crest.Data`, connectors, query, API
> surfaces) is still to be decided; see [blazor-display.md](blazor-display.md) › Rulings.

> **Ruling 2026-10-09 (sequencing):** the display system binds everything it reads through
> queries ([blazor-display.md](blazor-display.md) › decision 4): `Item`, `User` and `Tenant`
> are built-in system queries from an in-process source, and a slot is `{ query, parameters,
> path }`. The query **contract** (typed, paged, caller-scoped source; the system sources;
> parameters as bindings) therefore moves ahead of the Templates rework; the SQL rewriter, the
> builder and the connectors follow it.

> **Ruling 2026-10-10:** queries are one of the two kinds of **operation** in the operation
> registry, behind the one access machinery; the surfaces (REST, GraphQL, RPC, Liquid, pickers)
> are generated from that registry. The build order is [workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery) › Tasks
> (step 2 is this document's pipeline and connection system; step 1, `Crest.Access`, supplies
> the caller and the ScopeSet that "permissions injected at run time" below is implemented
> with). This document stays the design; the items below are its task list. The survey of what
> exists today (the single choke point `DefaultQueryManager`, the Cyqwel AST rewriter that can
> take the scope predicates, the Liquid-rendered SQL templates to remove, the missing table
> allow-list) is recorded in workflows.md › Operations › step 2.

## The query system

Every question about queries is answered from **two aspects**, kept apart: **the builder**
(authoring a query, inside the tenant's shell) and **query runs** (executing a saved query
for whoever requests it — API, GraphQL, Liquid, feeds).

- [ ] **Column types, by aspect** (ruling 2026-10-06).
  - *Builder:* it knows the types of the datasets it works with — content types, parts,
    fields, bags, option lists, and the columns each step produces — from the content model,
    and from zero-row probes (the query run with no rows returned) for free SQL steps. A
    saved query keeps a type cache for the builder, refreshed when the query changes and
    when any content type it touches is updated.
  - *Runs:* results carry their own column names and types, as the database returns them.
    Nothing needs a query's shape in advance.
  - **The shape is fixed at build time** (ruling 2026-10-06). Queries are prebuilt; what a
    query returns is decided when it is built, and only the permissions are injected when it
    runs. Callers cannot choose columns (stock GraphQL lets them pick among a query's
    columns, a projection that runs the whole query anyway and filters no rows — stock checks
    only "may this caller run this query"). Hiding a column from some callers, if ever
    needed, is a build-time decision or a field permission, not a client choice.
  - *GraphQL* creates, reads, updates and deletes queries and triggers runs, returning rows
    with their column metadata. It does not give each saved query its own GraphQL type (stock
    Orchard does, which is why it needs a hand-written schema per query); that is replaced.

- [ ] **Queries are Crest's** (ruling 2026-10-05): Orchard Queries and indexes, a query
  builder on the existing Queries page and `api/crest/queries`, and basic export of a
  query's results. Sharing, scheduled delivery and full reporting are built downstream on
  it.
- [ ] **Rework the platform's Queries: simple querying, enough for Crest's own consumers** (ruling
  2026-10-06), in place in the platform (since the hard fork), as the file system reworks Media. The bar is
  that stock Crest is enough for the audit system to build custom filtered activity streams
  on it ([audit.md](audit.md) › 5), and for downstream reporting to extend it. Orchard's
  contract (`IQuerySource.ExecuteQueryAsync(Query, IDictionary<string, object>)` returning
  untyped `Items`, results in one tenant `QueriesDocument`) is too thin even for that, so
  the rework adds:
  - **Paging and cancellation in the contract** — page tokens, a `CancellationToken`, a total
    where the source can give one.
  - **Permissions are injected at run time, for the user requesting the data** (ruling
    2026-10-06). A saved query is stored as written, with no permissions in it. Every time any
    user runs it — free SQL included — the server takes that user's shell, organization and
    principal (the per-request principal built from `X-Shell` and `X-Org`,
    [shells-and-themes.md](shells-and-themes.md#the-member-portal-login-sessions-sides)) and adds the `WHERE` clauses their permissions require for every
    table the query touches, before it runs. The same query returns different rows to
    different people; a member's `SELECT * FROM …` returns only what they may see, and an
    audit feed only events on records the caller may see. Each module registers, for the
    tables it owns, the clauses that scope them (organization ownership, hierarchy, the file
    access index, audit dimensions). The rewrite goes through the SQL parser (`SqlParser`,
    which already rewrites table names for the tenant prefix): every reference to a scoped
    table — in joins, subqueries and unions — is replaced by its filtered form, so no path
    reaches the unfiltered table. A query that cannot be scoped safely (a table with no
    registered scope, a construct the parser cannot analyze) is refused rather than run
    unfiltered. A tenant admin's scope is the whole tenant, so their queries simply get no
    restricting clauses — no separate "unscoped" mode or permission. Counts and groupings
    (`COUNT`, `GROUP BY`) run over the already-filtered rows, so every caller's totals reflect
    only what they may see.
  - **The builder knows the content model** (ruling 2026-10-06): content items, their parts
    and fields, bags and their elements, option pickers (keys, labels, categories) and the
    index tables behind them — so a query can reach any of them, and is scoped for each.
  - **The builder works like Excel's Power Query (M)**, inside the tenant's shell, where the
    tenant's content types, enabled modules and the author's permissions exist (rulings
    2026-10-06): a query is built
    step by step, and at every step the builder shows what is available to use — the
    content types, parts, fields, bag elements, option lists and relationships in reach —
    with completion as you type, rather than asking the author to know the schema.
  - **Results paged, streamed only as a special case** (ruling 2026-10-06). Paging is applied
    after the permission clauses are injected, through YesSql's own dialects (which already
    write `OFFSET/FETCH` or `LIMIT` per database), inside the tenant's shell — never
    bypassing shells, permissions or YesSql; a stable order is added when the query has none,
    so pages never repeat or skip rows. Streaming (large exports and the like) reads through
    ADO.NET's provider-neutral `DbDataReader`, so no code is written per database type.
  - **Typed schema** — column names and types, so builders and GraphQL are not limited to
    string and integer.
  - **Caller scope** — every execution carries the tenant, the user and their context, and
    the source filters by permission inside the query, never after paging (what audit
    feeds need). Today Liquid's `query` filter, the scripting `executeQuery` and the Razor
    helpers skip the per-query permission; the rework **checks it everywhere, with no
    exceptions** (ruling 2026-10-06). There is always a caller: anonymous is a caller with
    the Anonymous permissions, and work the system does on its own (background jobs, system
    flows) runs as a system principal with its own permission set — two different things.
  - **Extension points** — query sources, the connection protocols below and screens on the
    Queries pages, all registered by other modules.

  Keeps the stock query feature ids and the `api/queries` and Liquid surfaces working for
  existing callers.

## System queries and slot bindings

Everything a page or a component reads comes through a query; a **slot** on a component is a
data binding `{ query, parameters, path }` ([blazor-display.md](blazor-display.md) ›
decision 4), never a service call of its own. Three queries are built in, served by the
in-process `System` source and memoised per request:

| Query | Answers | Notes |
| --- | --- | --- |
| `item` | the content item the route resolved | the route parameter is the binding |
| `user` | the caller: name, roles, side, organization, class | facts, never permissions |
| `site` | site settings a surface may show: the site name, the branding renditions (login, sidebar, favicon, email), the design-system tokens | the anonymous-readable subset is decided by the query's permission, so the login page reads it without a special endpoint |

Rules that follow:

- **One accessor, no per-surface wiring.** A surface that shows the site name or a logo
  binds a slot to `site` with the rendition name as the path (`site › branding.sidebar`);
  the client keeps the query's result in its session copy the way it keeps any query result,
  and SSR runs the same query in process. Nothing reads site settings or media paths
  directly for display.
- **Empty is a value.** A rendition that is not set comes back empty and the component
  falls back (login: the site name as text; sidebar: the default mark; favicon: the static
  icon). Setting a rendition fills every surface on the next render because every surface
  binds the same query.
- **Paths resolve inside the query.** `site` returns URLs resolved through the media file
  store at run time; the stored value is the media path, never an absolute URL.
- **Writes are actions.** Uploading a rendition or renaming the site is a workflow action
  behind `ManageSettings` and Media's own checks; no slot binds a write.

The branding plan itself (which renditions exist, where the files live, the settings group,
the login page as a page) is [branding.md](branding.md).

## The connection system

Ruled 2026-10-06; **to be moved up in the plan** — it is the base the downstream connectors,
the external data features and now Workflows' connectors stand on, so it needs designing
before them.

- [ ] **The SQL connector is dogfooded.** The host's own database is just another SQL
  connection: queries over the tenant's data run through `Crest.Queries.Sql` against it
  (tenant table prefix and caller scope applied), replacing Orchard's stock SQL query
  source rather than sitting beside it. External databases use the same connector with a
  different connection.
- [ ] **Workflows on Queries.** What `Crest.Workflows` has today moves down into Queries:
  the connection model and its document, sealed secrets and tokens, the OAuth
  authorize/callback flow, the SSRF-guarded HTTP client and invoker, the token cache, rate
  limiting. What stays in Workflows is wiring: the activities (`Call connector`, `Poll
  connector`) as consumers of Queries connections, and inbound webhooks as triggers, which
  use the connection only to verify signatures with its secret. Retries are split so they
  never compound: Queries retries at the transport level (failed request, `Retry-After`),
  the engine retries activities. Existing connections migrate before the first public
  release, while that is cheap. One Connections screen and one `ManageConnections`
  permission serve both. Renamed now with no aliases (ruling 2026-10-06): pre-release, only
  Workflows' own connection names change — the OAuth callback route, the permission
  (`ManageCrestWorkflowConnections` → `ManageConnections`), the config section and the
  data-protection purpose. Provider apps and settings are updated by hand. Secrets and
  tokens move under a new data-protection purpose and new document names in Queries (without
  "Crest"); there is no migration code — dev tenants are reset and connections and their
  authorizations re-entered (ruling 2026-10-06). A secret that fails to decrypt is an explicit
  error, not a connection that silently looks unauthorized as today.
- [ ] **Retries belong to the connection system** (ruling 2026-10-06). Today it is the
  reverse: the HTTP layer sends once and Workflows' `ConnectionResilienceStrategy` retries
  around the activity; `Retry-After` is honoured nowhere, and a refusal by Crest's own rate
  limiter is treated as permanent. Instead the connection system retries failed requests
  with backoff, honours `Retry-After` (429 included) and respects each connection's retry
  count, for every caller — workflows, query runs, search, reports. Workflows' strategy is
  deleted and connector activities default to no engine retry, so attempts never multiply;
  the attempts made are reported back to the caller, so run history still shows them. A
  long wait is never slept through on a worker: the connection system returns "retry after
  T" and the caller reschedules (a workflow resumes the activity at T).
- [ ] **One assembly, many features** (ruling 2026-10-06). `Crest.Queries` is one module and
  package; `Sql`, `Rest`, `Soap`, `Rpc` and later protocols are features a tenant enables.

- [ ] **Protocol layers, one feature each:** `Crest.Queries.Sql` (SQL connections over
  ADO.NET-style providers), `Crest.Queries.Rest`, `Crest.Queries.Soap`, `Crest.Queries.Rpc`,
  others later. Each owns, for its protocol: the connection model (endpoint, auth,
  credentials sealed per tenant), executing a request, mapping results into the query
  system's typed, paged rows, and storing results locally when asked, so external data is
  queryable like any other and publishable through the Crest API under tenant and user
  scope and permissions.
- [ ] **What the layers must carry for external sources** (from the 2026-10-06 platform
  research): two execution models behind one interface — asynchronous jobs (submit, job
  id, poll, page) and synchronous streaming readers; retries with backoff that honour the
  source's rate limits; per-user delegated credentials where the source supports them, a
  service credential only where the tenant chooses it; caching scoped to the credential,
  never shared across users when the source applies its own row-level security; cost and
  quota guards (dry runs, bytes-billed caps, timeouts, per-source concurrency limits);
  results that can expire at the source; query-language and parameter metadata per source
  (SQL dialects, DAX, KQL); regional endpoints.
- [ ] **Search engines are connections; search is a consumer of Queries** (ruling
  2026-10-06). Lucene, Elasticsearch, Azure AI Search and later engines are modules that
  register their connections and connector source with the connection system, like any SQL
  or REST connector. A separate Search module holds the in-app search UI components, the C#
  search interface and the search APIs, and runs through Queries the standard way, so it
  works with any registered search provider; run-time permission injection applies to
  search as to every query. Orchard's search modules today plug their own query sources into
  Queries; under this split they become search-engine connections. How permission injection
  is implemented for search is still to design: a search engine indexing Orchard data must be
  set up with the permission system (permissions written into what it indexes, and filters
  applied in every search) so Orchard data never leaks through it.
- [ ] **Where permissions live: Crest's data versus truly external systems** (ruling
  2026-10-06).
  - **Data Crest owns** — its own database, and search engines indexing Crest data — is
    scoped by Crest's permission system, injected at run time as above.
  - **Truly external systems** (Salesforce, for example) keep their own permission sets;
    Crest is not their permission provider and stores none of their permissions. Crest
    stores only who may use a connection (a permission on the connection or connector), and
    passes the user's own credentials to the external system — OAuth per user, or whatever
    the system uses — so that system applies its own permissions to what it returns.
- [ ] **Plugin registry for connectors.** Connectors for specific platforms register as
  plugins against a protocol layer; any module can add one. The registry is Crest's; the
  connectors themselves are downstream. Workflows' connector descriptors and
  `IWorkflowConnectorProvider` move here from `Crest.Workflows.Abstractions` (ruling 2026-10-06);
  Workflows lists connector operations from Queries for its palette, and inbound webhooks
  verify signatures through a narrow verifier without ever reading the secret.

## Hardening (2026-10-11)

An audit of the pipeline against the scope rule found ways round it; all fixed the same day
(compiled, tests in `test/Crest.Tests/Queries`, not run):

- **The SQL rewriter** (`Sql/SqlParser.cs`): CTE names are scoped lexically (a non-recursive
  CTE's body sees the real table, so `WITH Document AS (SELECT * FROM Document)` is refused);
  table names are single-part only (a schema or database qualifier read another tenant);
  functions come from an allow-list (no `query_to_xml`, `pg_read_file`, `dblink`,
  `LOAD_FILE`, `OPENROWSET`); a table source other than a named, derived or joined table is
  refused; `SELECT … INTO` does not parse; one statement per query.
- **`ReturnContentItems`** loads through the scoped `ContentItemIndex`
  (`Scoping/ScopedContentItems.cs`): an id the scoped index did not return is never loaded,
  and the Sql source requires a `DocumentId` column (`SELECT 42 AS DocumentId` loaded any
  document before).
- **Lucene and Elasticsearch sources** take the caller's scope: content hits load through the
  scoped index; raw documents only for a caller whose content rule is All; paged like the rest.
- **Every source pages** (default 100, cap 500, total and page token): the Sql source wraps the
  statement as a derived table with dialect paging (on SQL Server an inner `ORDER BY` is kept
  legal but its order is not guaranteed).
- **The admin SQL runner** runs through `IQueryManager`, never committing, with cancellation.
- **Structured left joins** carry the joined table's scope in `ON`, so an out-of-scope match no
  longer drops the left row; a None rule there is `AND 1 = 0`, not an empty query.
- **Drafts**: `ContentItemScopeProvider` (version 2) gives a view-only type `Published` rows
  only, and `Published OR Latest` with preview or edit.
- Smaller: parameters bind case-insensitively; a parse failure is an error (400 on the API),
  not an empty result; the System source checks scope before loading and its memo key
  includes `ReturnContentItems`; Liquid, scripting and deployment pass cancellation, and
  deployment exports every page; `RunQuery` runs only its descriptor's query and refuses when
  the descriptor's permission is not that query's.

Behaviour that changes for callers: a raw-document Lucene or Elasticsearch query is a 403 for
anyone without the All rule; a Liquid or scripting query with a parse error throws.

## Persistence: one system

Ruling direction (2026-10-11): Crest data goes through one data-access system, YesSql on the
tenant's `ISession` and its unit of work. This is the state after the first consolidation pass.

**Folded into the unit of work**

- Members' user hierarchy (`UserHierarchyService`, table `CrestUserHierarchy`): it used to run
  Dapper on its own connection and transaction, as deferred tasks that swallowed failures. It
  now runs on the session's connection and transaction (`ISession.BeginTransactionAsync()` for
  writes, `CreateConnectionAsync()` plus `CurrentTransaction` for reads). The writes run inline
  in member creation, binding add and remove, portal registration and class conversion; a
  failure cancels the whole unit. The table stays a plain table, not a YesSql index: the node
  id is an identity embedded in the materialized path. No schema change, so no migration.
- Workflow engine stores (already done): every store (definitions, instances, triggers, bookmarks, bookmark
  queue, execution log, activity execution records) is YesSql-backed in `Crest.Workflows`.

**Removed as dead**

- `src/Crest.Workflows/reference/`, which was in no solution or project.
- The engine `Crest.Workflows.KeyValues` module.
- The engine's tenant management: `ITenantStore`, `MemoryTenantStore`, `StoreTenantsProvider`,
  the management features and the `Tenants` endpoints. `TenantsFeature` and `TenantHeaders`
  stay, because the runtime dispatchers use them.
- The engine's in-memory and no-op stores, `MemoryStore<T>` and `AddMemoryStore`. The store
  factories now throw "No X configured" unless the host supplies a store.
- EF Core, Sqlite, Npgsql, MySql, Oracle, Pomelo, SqlClient, Elastic, Redis, Azure identity
  and Testcontainers package versions in the engine's `Directory.Packages.props`.

**Deliberately outside the session**

- `IndexingTaskManager` keeps its own connection. It is the upstream queue design: a deferred
  task appends the queue after the request's commit, so only committed changes are queued, and
  background indexers read it independently. A bug that inserted the whole queue once per
  category is fixed.
- Read-only or batched side sessions: `ContentIndexingService` (a non-tracking read session
  across indexing batches, now disposed with the service), the sitemap
  `DefaultContentItemsQueryProvider` (a non-tracking read whose items are cached) and
  `MoveAttachedMediaFieldsStepExecutor` (one session per batch in a bulk recipe migration;
  its keyset paging no longer also skips rows).

**Open rulings**

- [x] `Crest.Global` and its own store. **Ruled 2026-10-11: kept as the one exception** - the
  only store outside a tenant, for host-wide reference data (geo, currencies, global lists).
  To do: its schema versioning (`ICrestGlobalSchema`, `CrestGlobalSchemaState`) moves onto
  `DataMigration`, so there is one migration system.
- [ ] The file-based stores (`IFileDocumentStore`, `FileContentDefinitionStore`,
  `FilePlacementsStore`). To discuss in depth (2026-10-11).
- [ ] `IVolatileDocumentManager` (distributed-cache documents). To discuss in depth.
- [ ] The search modules' stores (Lucene, Elasticsearch). To discuss in depth.
- [ ] `tenants.json` (shell settings outside the database). To discuss in depth.

## Decisions needed

- [x] **Search sources and the old query contract (Q2).** Ruled by landing (2026-10-10,
  option 1): `IQuerySource` itself became the new contract (typed, paged, cancellable,
  caller-scoped) and the SQL, Lucene and Elasticsearch sources were updated with it; the
  contract stays in `Crest.Queries.Abstractions`, the platform's pattern for extension points
  other modules implement. A search source that does not yet apply the caller's scope stays
  admin-only until its engine becomes a connection.
- [ ] **What OrchardCore already offers natively** to build the connection system on — to be
  researched later, before designing, so nothing is reinvented.
- [ ] **Host database versus external SQL connections (Q6).** The SQL connector is
  dogfooded against the host database. That connection must use the tenant's table prefix,
  the store's isolation level and the caller's scope; external SQL connections must not be
  prefixed or rewritten for the tenant, and use their own credentials. Options: (1) a
  connection kind, host or external, set by the system and not editable; (2) one kind with
  prefixing and scope as per-connection settings. Recommendation: 1; the host connection is
  created by Crest and cannot be pointed elsewhere, every other SQL connection is external.
- [ ] **Crest's Queries API (Q7).** `QueriesController` writes a query's settings as raw
  JSON without the source handlers (nothing validates them) and has no run, preview, results
  or schema endpoints, which the builder needs. Options: (1) each source validates its own
  settings through the contract, and run, preview, results and schema endpoints are added
  for the builder; (2) keep raw JSON for admins and validate at run time only.
  Recommendation: 1, as the generated surfaces of [workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery) step 7
  rather than more hand-written endpoints.
