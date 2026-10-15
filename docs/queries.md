# Queries — the query system, and the connections it consumes

`Grove.Queries` is **the query system**: querying data simply and safely, enough for Grove's own
consumers (audit activity streams first) and for downstream modules to extend. It owns typed,
paged rows, saved queries, local storage of results and its API. A query is any request through
a connection, reads and writes alike; a SQL query is a CRUD action on a database. The query
system is a tool for builders and administrators; business users and members never use it
directly.

Queries does not own connections. Every outgoing connection to an external system belongs to
**the connection system**: one module per protocol, `Grove.Http`, `Grove.Rest`, `Grove.Soap`,
`Grove.GraphQL` and later others (☐ to be created). Database connections belong to the data
layer (`Grove.Data`, `Grove.Data.YesSql`): the host database and external databases are SQL
connections there. Queries consumes connections from both and keeps no connection model of its
own ([Connections](#connections-the-protocol-modules-and-the-data-layer)).

Queries are one of the two kinds of **operation** in the operation registry, behind the one
access machinery ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery));
`Grove.Access` supplies the caller and the `ScopeSet` that run-time permission injection is
implemented with. Grove.Queries declares its own API surface (the management API
`api/grove/queries` and, as generated surfaces, the run endpoints of saved queries) and
registers it in Grove.Server's API endpoint registry
([architecture.md › Modules declare, Grove.Server registers and serves](architecture.md#modules-declare-groveserver-registers-and-serves)).

**Workflows consumes the same connections.** Its connector activities and webhook triggers use
connections from the protocol modules; Workflows only does the wiring inside
([Workflows on the connection system](#connections-the-protocol-modules-and-the-data-layer)).

Above them, downstream: the connectors for specific platforms and services (BigQuery,
Zoho Analytics, Fabric, a given REST API, …) are plugins of a downstream reporting module,
with a plugin registry other modules can hook into. Very downstream; not in the first
public release.

**Value types.** The value type registry (field type, editor, display primitive, query column
type, workflow type per value type) lives in `Grove.Data` (`Grove.Data.Types`); the query
system reads it.

**The display system binds through queries** ([blazor-display.md](blazor-display.md) ›
decision 4): `Item`, `User` and `Tenant` are built-in system queries from an in-process source,
and a slot is `{ query, parameters, path }`. That is why the query contract (typed, paged,
caller-scoped source; the system sources; parameters as bindings) came first, ahead of the SQL
rewriter, the builder and the connectors.

**What is built**: the structured query model compiled through YesSql with the caller's scope
conjoined for every table, the SQL source with parameters only and the scope rewriter, the
typed, paged, cancellable source contract through every caller, the `System` source (`item`,
`user`, `site`), query descriptors and the catalog, every run paged. Still to build: the
[Backlog](#backlog).

## The query system

Every question about queries is answered from **two aspects**, kept apart: **the builder**
(authoring a query, inside the tenant's shell) and **query runs** (executing a saved query
for whoever requests it: API, GraphQL, Liquid, feeds).

- **Queries are Grove's**: the platform's Queries and indexes, reworked in place (as the file
  system reworks Media), a query builder on the Queries page and `api/grove/queries`, and basic
  export of a query's results. Sharing, scheduled delivery and full reporting are built
  downstream on it. The bar: stock Grove is enough for the audit system to build custom filtered
  activity streams on it ([audit.md](audit.md) › Activity feeds), and for downstream reporting
  to extend it. The stock query feature ids and the `api/queries` and Liquid surfaces keep
  working for existing callers.
- **The contract** is `IQuerySource` in `Grove.Queries.Abstractions` (the platform's pattern
  for extension points other modules implement): typed, paged, cancellable, caller-scoped. The
  SQL, Lucene and Elasticsearch sources implement it. **Paging and cancellation** are in the
  contract: page tokens, a `CancellationToken`, a total where the source can give one. **Typed
  schema**: column names and types, so builders and GraphQL are not limited to string and
  integer.
- **Column types, by aspect.**
  - *Builder:* it knows the types of the datasets it works with (content types, parts,
    fields, bags, option lists, and the columns each step produces) from the content model,
    and from zero-row probes for free SQL steps. A saved query keeps a type cache for the
    builder, refreshed when the query changes and when any content type it touches is updated.
  - *Runs:* results carry their own column names and types, as the database returns them.
    Nothing needs a query's shape in advance.
- **The shape is fixed at build time.** What a query returns is decided when it is built, and
  only the permissions are injected when it runs. Callers cannot choose columns (stock GraphQL
  lets them pick among a query's columns, a projection that runs the whole query anyway and
  filters no rows). Hiding a column from some callers, if ever needed, is a build-time decision
  or a field permission, not a client choice.
- **GraphQL** creates, reads, updates and deletes queries and triggers runs, returning rows
  with their column metadata. It does not give each saved query its own GraphQL type (which is
  why stock needs a hand-written schema per query).
- **Permissions are injected at run time, for the user requesting the data.** A saved query is
  stored as written, with no permissions in it. Every time any user runs it, free SQL included,
  the server takes that user's caller (shell, organization, principal) and adds the `WHERE`
  clauses their permissions require for every table the query touches, before it runs. The
  same query returns different rows to different people; a member's `SELECT * FROM …` returns
  only what they may see, and an audit feed only events on records the caller may see. Each
  module registers, for the tables it owns, the scope providers that scope them (organization
  ownership, hierarchy, the file access index, audit dimensions). The rewrite goes through the
  SQL parser (`SqlParser`, which also rewrites table names for the tenant prefix): every
  reference to a scoped table, in joins, subqueries and unions, is replaced by its filtered
  form, so no path reaches the unfiltered table. A query that cannot be scoped safely (a table
  with no registered scope, a construct the parser cannot analyze) is refused rather than run
  unfiltered. A tenant admin's scope is the whole tenant, so their queries get no restricting
  clauses; there is no separate "unscoped" mode or permission. Counts and groupings run over
  the already-filtered rows.
- **There is always a caller**, and the per-query permission is checked everywhere, with no
  exceptions (Liquid's `query` filter, scripting's `executeQuery`, the Razor helpers included).
  Anonymous is a caller with the Anonymous permissions; work the system does on its own
  (background jobs, system flows) runs as a system principal with its own permission set.
- **The builder knows the content model**: content items, their parts and fields, bags and
  their elements, option pickers (keys, labels, categories) and the index tables behind them,
  so a query can reach any of them, and is scoped for each.
- **The builder works like Excel's Power Query (M)**, inside the tenant's shell, where the
  tenant's content types, enabled modules and the author's permissions exist: a query is built
  step by step, and at every step the builder shows what is available (the content types,
  parts, fields, bag elements, option lists and relationships in reach) with completion as you
  type.
- **Results paged, streamed only as a special case.** Paging is applied after the permission
  clauses are injected, through YesSql's own dialects, inside the tenant's shell; a stable order
  is added when the query has none, so pages never repeat or skip rows. Every source pages
  (default 100, cap 500, total and page token). Streaming (large exports and the like) reads
  through ADO.NET's provider-neutral `DbDataReader`, so no code is written per database type.
- **Extension points**: query sources (including those over the protocol modules' and the data
  layer's connections, below) and screens on the Queries pages, all registered by other modules.

### Scope enforcement

What keeps the scope rule unbypassable (tests in `test/Grove.Tests/Queries`):

- **The SQL rewriter** (`Sql/SqlParser.cs`): CTE names are scoped lexically (a non-recursive
  CTE's body sees the real table, so `WITH Document AS (SELECT * FROM Document)` is refused);
  table names are single-part only (a schema or database qualifier could read another tenant);
  functions come from an allow-list (no `query_to_xml`, `pg_read_file`, `dblink`, `LOAD_FILE`,
  `OPENROWSET`); a table source other than a named, derived or joined table is refused;
  `SELECT … INTO` does not parse; one statement per query.
- **`ReturnContentItems`** loads through the scoped `ContentItemIndex`
  (`Scoping/ScopedContentItems.cs`): an id the scoped index did not return is never loaded, and
  the Sql source requires a `DocumentId` column.
- **Lucene and Elasticsearch sources** take the caller's scope: content hits load through the
  scoped index; raw documents only for a caller whose content rule is All (anyone else gets
  403); paged like the rest.
- **The Sql source** wraps the statement as a derived table with dialect paging (on SQL Server
  an inner `ORDER BY` is kept legal but its order is not guaranteed).
- **The admin SQL runner** runs through `IQueryManager`, never committing, with cancellation.
- **Structured left joins** carry the joined table's scope in `ON`, so an out-of-scope match
  does not drop the left row; a None rule there is `AND 1 = 0`, not an empty query.
- **Drafts**: `ContentItemScopeProvider` gives a view-only type `Published` rows only, and
  `Published OR Latest` with preview or edit.
- Parameters bind case-insensitively; a parse failure is an error (400 on the API, a throw in
  Liquid or scripting), not an empty result; the System source checks scope before loading and
  its memo key includes `ReturnContentItems`; Liquid, scripting and deployment pass
  cancellation, and deployment exports every page; `RunQuery` runs only its descriptor's query
  and refuses when the descriptor's permission is not that query's.

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

## Connections: the protocol modules and the data layer

The connection system is the base the downstream connectors, the external data features,
Queries and Workflows' connectors stand on, so it is designed before them. There is one
connection system; Queries and Workflows consume it and keep no parallel set.

- **One module per protocol** (☐ to be created): `Grove.Http`, `Grove.Rest`, `Grove.Soap`,
  `Grove.GraphQL`, later others. Each owns, for its protocol: the connection model (endpoint,
  auth, credentials sealed per tenant), executing a request, the SSRF-guarded client, the token
  cache, rate limiting and the OAuth authorize/callback flow. The existing
  `Grove.Apis.GraphQL.Client` (a client for Grove's own GraphQL API) becomes or folds into the
  GraphQL connector (☐).
- **Database connections are the data layer's.** The host database and external databases are
  SQL connections in `Grove.Data` / `Grove.Data.YesSql`; the host connection carries the
  tenant's table prefix, the store's isolation level and the caller's scope.
- **Queries consumes connections.** Its sources (the `Grove.Queries.Sql` feature for SQL, one per
  protocol for the others) take a connection from the data layer or a protocol module, run the
  request through it, map the results into the query system's typed, paged rows, and store
  results locally when asked, so external data is queryable like any other and publishable
  through the Grove API under tenant and user scope and permissions.
- **The SQL source is dogfooded.** The host's own database is just another SQL connection:
  queries over the tenant's data run through the SQL source against it (tenant table prefix
  and caller scope applied), replacing the stock SQL query source rather than sitting beside it.
  External databases use the same source with a different connection.
- **Workflows on the connection system.** The connection model and its document, sealed secrets
  and tokens, the OAuth flow, the SSRF-guarded HTTP client and invoker, the token cache and rate
  limiting are the protocol modules'. What Workflows keeps is wiring: the activities (`Call
  connector`, `Poll connector`) as consumers of the protocol modules' connections, and inbound
  webhooks as triggers, which use the connection only to verify signatures through a narrow
  verifier without ever reading the secret. One Connections screen and one `ManageConnections`
  permission serve every consumer; both belong to the connection system, not to Queries or
  Workflows. Workflows' connection names change with no aliases: the OAuth callback route, the
  permission (`ManageGroveWorkflowConnections` → `ManageConnections`), the config section and the
  data-protection purpose; provider apps and settings are updated by hand. Secrets and tokens
  live under a new data-protection purpose and new document names in the connection system;
  there is no migration code (dev tenants are reset and connections re-entered). A secret that
  fails to decrypt is an explicit error, not a connection that silently looks unauthorized.
- **Retries belong to the connection system.** The connection system retries failed requests
  with backoff, honours `Retry-After` (429 included) and respects each connection's retry
  count, for every caller (workflows, query runs, search, reports). Connector activities
  default to no engine retry, so attempts never multiply; the attempts made are reported back
  to the caller, so run history still shows them. A long wait is never slept through on a
  worker: the connection system returns "retry after T" and the caller reschedules (a workflow
  resumes the activity at T). Today it is the reverse: the HTTP layer sends once, Workflows'
  `ConnectionResilienceStrategy` retries around the activity, `Retry-After` is honoured nowhere,
  and a refusal by Grove's own rate limiter is treated as permanent.
- **What the protocol modules and Queries' sources carry for external sources**: two
  execution models behind one interface (asynchronous jobs: submit, job id, poll, page; and synchronous streaming readers); retries
  with backoff that honour the source's rate limits; per-user delegated credentials where the
  source supports them, a service credential only where the tenant chooses it; caching scoped
  to the credential, never shared across users when the source applies its own row-level
  security; cost and quota guards (dry runs, bytes-billed caps, timeouts, per-source
  concurrency limits); results that can expire at the source; query-language and parameter
  metadata per source (SQL dialects, DAX, KQL); regional endpoints.
- **Search engines are connections; search is a consumer of Queries.** Lucene, Elasticsearch,
  Azure AI Search and later engines are modules that connect through the connection system (a
  remote engine through its protocol module) and register their query source with Queries, like
  any SQL or REST source. A separate Search module holds the in-app search UI components, the C# search interface and the search APIs, and runs
  through Queries the standard way, so it works with any registered search provider; run-time
  permission injection applies to search as to every query. A search engine indexing Grove data
  is set up with the permission system (permissions written into what it indexes, and filters
  applied in every search) so Grove data never leaks through it ([sharing.md](sharing.md) ›
  Open questions, F5). Until its engine becomes a connection, a search source that does not
  apply the caller's scope stays admin-only.
- **Where permissions live.**
  - **Data Grove owns** (its own database, and search engines indexing Grove data) is scoped
    by Grove's permission system, injected at run time as above.
  - **Truly external systems** (Salesforce, for example) keep their own permission sets;
    Grove is not their permission provider and stores none of their permissions. Grove
    stores only who may use a connection (a permission on the connection or connector), and
    passes the user's own credentials to the external system (OAuth per user, or whatever
    the system uses) so that system applies its own permissions to what it returns.
- **Plugin registry for connectors.** Connectors for specific platforms register as plugins
  against a protocol module; any module can add one. The registry is the connection system's;
  the connectors themselves are downstream. Workflows' connector descriptors and
  `IWorkflowConnectorProvider` belong here (☐ today in `Grove.Workflows.Abstractions`);
  Workflows lists connector operations from the connection system for its palette.

## Persistence: one system

Grove data goes through one data-access system, YesSql on the tenant's `ISession` and its unit
of work.

- **In the unit of work**: Members' user hierarchy (a YesSql closure table, documents and
  indexes, [parties.md](parties.md) › Hierarchies); every workflow engine store (definitions,
  instances, triggers, bookmarks, bookmark queue, execution log, activity execution records),
  YesSql-backed in `Grove.Workflows`. The engine's store factories throw "No X configured"
  unless the host supplies a store; it has no in-memory stores and no tenant management of its
  own (`TenantsFeature` and `TenantHeaders` remain for the runtime dispatchers).
- **Deliberately outside the session**:
  - `IndexingTaskManager` keeps its own connection. It is the upstream queue design: a
    deferred task appends the queue after the request's commit, so only committed changes are
    queued, and background indexers read it independently.
  - Read-only or batched side sessions: `ContentIndexingService` (a non-tracking read session
    across indexing batches, disposed with the service), the sitemap
    `DefaultContentItemsQueryProvider` (a non-tracking read whose items are cached) and
    `MoveAttachedMediaFieldsStepExecutor` (one session per batch in a bulk recipe migration,
    keyset paging).
- **`Grove.Global` is the one exception**: the only store outside a tenant, for host-wide
  reference data (geo, currencies, global lists). It uses the one migration system: a global
  migration is a `GlobalDataMigration` (a `DataMigration` with the global `Session` and its
  index providers), run by `DataMigrationSteps` (the Create/UpdateFrom convention shared with
  the tenant `DataMigrationManager`) and recorded in a `DataMigrationRecord` in the global
  store. `GroveGlobalSchemaState` keeps only the data version; its old per-schema versions
  seed the record once (`LegacySchemaName`, transitional). A host's own global schemas
  convert the same way. Test: `GlobalMigrationTests`.

## Backlog

- [ ] The query builder (Power Query-style, with the type cache and zero-row probes).
- [ ] Statement caching.
- [ ] `ReturnContentItems` through the gate.
- [ ] The read job.
- [ ] The connection system: the protocol modules (`Grove.Http`, `Grove.Rest`, `Grove.Soap`,
  `Grove.GraphQL`, with `Grove.Apis.GraphQL.Client` becoming or folding into the GraphQL one),
  the move of Workflows' connection model, secrets, OAuth, HTTP client, connector descriptors
  and `IWorkflowConnectorProvider` into them, retries in the connection system (deleting
  `ConnectionResilienceStrategy`), search engines as connections, the connector plugin registry.
- [ ] SQL connections in the data layer (`Grove.Data`, `Grove.Data.YesSql`): the host database
  and external databases; Queries' SQL source over them, the host connection replacing the
  stock SQL source.
- [ ] Queries' sources over the protocol modules' connections (typed paged rows, local storage
  of results).
- [ ] Grove's management surface (`api/grove/queries`, the Queries page) and the platform's
  `api/queries/{name}` are replaced by the declared and generated surfaces.

## Open questions

- **The data-layer module chain** (`Grove.Data`, connectors, query, API surfaces); see
  [blazor-display.md](blazor-display.md).
- **What the platform already offers natively** to build the connection system on, to be
  researched before designing, so nothing is reinvented.
- **Where the shared connection pieces live.** The Connections screen, `ManageConnections`, the
  connection document and the sealed-secret store are shared by every protocol module: in one
  of them (`Grove.Http`, which the others build on) or in a module of their own? And do the data
  layer's external SQL connections appear on the same screen?
- **Host database versus external SQL connections (Q6)**, in the data layer. The host
  connection must use the tenant's table prefix, the store's isolation level and the caller's
  scope; external SQL connections must not be prefixed or rewritten for the tenant, and use
  their own credentials.
  Options: (1) a connection kind, host or external, set by the system and not editable; (2) one
  kind with prefixing and scope as per-connection settings. Recommendation: 1; the host
  connection is created by Grove and cannot be pointed elsewhere, every other SQL connection is
  external.
- **Grove's Queries API (Q7).** `QueriesController` writes a query's settings as raw JSON
  without the source handlers (nothing validates them) and has no run, preview, results or
  schema endpoints, which the builder needs. Options: (1) each source validates its own
  settings through the contract, and run, preview, results and schema endpoints are added for
  the builder; (2) keep raw JSON for admins and validate at run time only. Recommendation: 1,
  as generated surfaces of the operation registry
  ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery))
  rather than more hand-written endpoints.
- **Other stores to discuss in depth**: the file-based stores (`IFileDocumentStore`,
  `FileContentDefinitionStore`, `FilePlacementsStore`), `IVolatileDocumentManager`
  (distributed-cache documents), the search modules' stores (Lucene, Elasticsearch),
  `tenants.json` (shell settings outside the database).
