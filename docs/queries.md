# Queries — the query system and the connection system

**Status: not started.** What is built today is Crest's thin management surface over
Orchard Queries: `api/crest/queries` (CRUD and the source list, behind `ManageQueries`)
and the Queries page (`Crest.AdminTheme/wasm/Pages/Queries.razor`). Execution still goes
through Orchard's `api/queries/{name}`.

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
    [members.md](members.md)) and adds the `WHERE` clauses their permissions require for every
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
  `IWorkflowConnectorProvider` move here from `Crest.Workflows.Domain` (ruling 2026-10-06);
  Workflows lists connector operations from Queries for its palette, and inbound webhooks
  verify signatures through a narrow verifier without ever reading the secret.

## Decisions needed

- [ ] **What OrchardCore already offers natively** to build the connection system on — to be
  researched later, before designing, so nothing is reinvented.
