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

## The query system

- [ ] **Queries are Crest's** (ruling 2026-10-05): Orchard Queries and indexes, a query
  builder on the existing Queries page and `api/crest/queries`, and basic export of a
  query's results. Sharing, scheduled delivery and full reporting are built downstream on
  it.
- [ ] **Fork Orchard's Queries: simple querying, enough for Crest's own consumers** (ruling
  2026-10-06), the way the file module forks Media and audit forks AuditTrail. The bar is
  that stock Crest is enough for the audit system to build custom filtered activity streams
  on it ([audit.md](audit.md) › 5), and for downstream reporting to extend it. Orchard's
  contract (`IQuerySource.ExecuteQueryAsync(Query, IDictionary<string, object>)` returning
  untyped `Items`, results in one tenant `QueriesDocument`) is too thin even for that, so
  the fork adds:
  - **Paging and cancellation in the contract** — page tokens, a `CancellationToken`, a total
    where the source can give one.
  - **Results streamed or paged, not buffered** into one list.
  - **Typed schema** — column names and types, so builders and GraphQL are not limited to
    string and integer.
  - **Caller scope** — every execution carries the tenant, the user and their context, and
    the source filters by permission inside the query, never after paging (what audit
    feeds need). Today Liquid's `query` filter and the scripting `executeQuery` skip the
    per-query permission; the fork closes that.
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
  permission serve both.

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
- [ ] **Plugin registry for connectors.** Connectors for specific platforms register as
  plugins against a protocol layer; any module can add one. The registry is Crest's; the
  connectors themselves are downstream.

## Decisions needed

- [ ] **What OrchardCore already offers natively** to build the connection system on — to be
  researched later, before designing, so nothing is reinvented.
