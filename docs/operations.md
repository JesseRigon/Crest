# Operations — one registry, one request path, four pipelines, one access machinery

**Status: plan, nothing implemented (2026-10-10).** The backbone of the application: how
every read and write in Crest, from a UI button, a picker, a content list, a Liquid slot, a
workflow step, a REST, GraphQL or RPC call, a webhook or an external connector, goes through
one system. This document is the task list; the designs it builds on are
[queries.md](queries.md) (the query pipeline and the connection system),
[workflows.md](workflows.md) (the action pipelines: units of work, queues, hooks),
[members.md](members.md) (the caller: side, organization, ceiling, policies) and
[blazor-display.md](blazor-display.md) (what binds to operations).

Priorities, in order: speed, runtime adaptability, ease of maintenance.

## Rulings (2026-10-10)

- **One registry.** The workflow engine's descriptor model (`ActivityDescriptor`,
  `InputDescriptor`, declared outputs, versions, `/descriptors/*`) is the registry of every
  operation, extended with a **query** kind. Queries are the read kind, workflows the action
  kind; workflows call queries, never the reverse ([blazor-display.md](blazor-display.md)
  › decision 4).
- **One request path.** Every request (page, API, slot, trigger, webhook, scheduled job,
  machine client) follows the same eight steps; only dispatch differs.
- **Four pipelines** at the end of the path: read and action, each synchronous and
  asynchronous. The ACID and queue guarantees built for workflows apply to every operation.
- **One access machinery** (`Crest.Access`) in front of all four: caller, decision, scope,
  audit. Nothing reaches data except through it; the surfaces are generated from the
  registry so they cannot check permissions on their own.
- **The caller is built per request on the server**, from the authenticated identity plus
  server-held state cached under the tenant's permission version; the client's session copy
  is for the UI only and never authority.
- **A read never writes; an action never serves a page; the workflow runtime never runs a
  synchronous read for a page.**
- **`Crest.Scripting` is removed**; workflows are the only code path for tenant logic
  ([blazor-display.md](blazor-display.md) § 12).

## Why there are nine surfaces today

The survey of 2026-10-10 found nine distinct paths by which data is read or written (table
under Migration), each with its own permission handling and almost none with caller scope.
Four reasons:

1. **The platform has no data-access layer below the web layer.** `IContentManager`,
   `ISession` and `IQueryManager` do no authorization; each controller checks for itself.
2. **Module autonomy was a design goal**: each OrchardCore module defines and enforces its
   own permissions; there was never a contract for "the caller's scope over this table".
   Liquid and scripting got no checks because "authoring is the gate".
3. **Crest added one `api/crest/*` controller per screen beside the platform**, under the
   no-changes rule that preceded the fork, copying the one-permission-per-controller pattern
   twenty-five times.
4. **Scope arrived after the controllers**: the organization scope, the class ceiling and
   the access policies were designed after most endpoints existed, so the only scoping built
   is the pickers' resolver.

The fix is structural: one place answers the question, and the surfaces are generated.

## The request path

```text
1. Tenant shell      the platform's tenant router: which tenant, which shell container,
                     the tenant prefix stripped into PathBase.
2. Theme shell       the shell selector (shells-and-themes.md › Shell dispatch): admin, site
                     or member bucket from the request, stamped; the shell base into
                     PathBase. A background entry point (timer, queue consumer, job) carries
                     its bucket and tenant explicitly and enters here too.
3. Authentication    the cookie or the Api scheme (bearer, machine-actors.md) → an identity,
                     or anonymous.
4. Caller context    ICallerContext built for this request: side from the bucket (API calls
                     confirm it with X-Shell, the member side carries X-Org), organization,
                     roles, class ceiling, policy verdicts, from the server-side state cache.
                     Markers that disagree with the bucket: denied, never defaulted.
5. Decision          IAccessDecision for the operation (a page is an operation too: its route
                     entry's permission sets; a module @page's route permission).
6. Scope             the ScopeSet for this caller, compiled and cached.
7. Dispatch          to one of the four pipelines (or, for a page, to the renderer, whose
                     slots and actions re-enter at step 5 as operations: decision and scope
                     run for each; the caller is built once per request).
8. Audit             the auditor records the decision and the execution.
```

Steps 1–6 are one middleware chain hosted once, in `Crest.Access`, where the shell
selector's startup filter runs today (before routing).

### Caller lifetime

The server-side state the caller is built from is cached per (user, tenant, side,
organization) with the tenant's **permission version**, bumped by every role, binding and
policy write and compared on every use, so a decision is always against current rights.

| Lifetime | Caller built | Decision and scope |
| --- | --- | --- |
| HTTP request | once, at step 4 | per operation |
| Interactive circuit or websocket (Blazor Server, SignalR) | per operation, since the connection outlives any one instant; full revalidation on the security-stamp interval; torn down on sign-out or a stamp change | per operation |
| Workflow burst | the actor snapshot says **who**; the caller is built again at the start of every burst, so a right lost between bursts fails the next one | per data activity |
| Background job | enters the path at step 1 as the actor it was written with; built then, never from the job payload | per operation |

Nothing serialized into a workflow instance, a job or a client carries rights; only identity.

## The four pipelines

| | Synchronous (answers in the request) | Asynchronous (answers later) |
| --- | --- | --- |
| **Read** | **Query pipeline.** Resolve → compile the statement with the ScopeSet (cached per query version and scope signature) → execute through the connection → page after scoping with a stable order → typed rows → cache key `(operation, version, parameters, scope signature, culture)`. Non-SQL sources apply the ScopeSet in memory before paging. A page render, a picker and an API `GET` use it. | **Read job.** The same compiled, scoped query as a durable background job: exports, large result sets, streaming, slow external sources. The caller gets a job id; the result arrives by bookmark, notification or download. Never used by a page render. |
| **Action** | **Unit of work.** One burst: one shell scope, one session, one transaction; inline hooks as child instances; a failed required attachment fails the unit and the request answers 409; per-object serialization by the object lock; atomicity derived, never asserted. A content operation, a short workflow and an API `POST` run here. | **Durable background.** The burst bookmarks and commits; the job is written in that unit, so it exists only if the unit committed; it runs afterwards in its own child scope with the connection's auth, resilience, rate limit and an idempotency key, and hands back by bookmark. The event side: stimuli after commit, partitioned by correlation id, at-least-once with idempotency keys. Connectors, approvals, timers, webhooks and long-running flows run here. |

Rules across the four: the gate runs once per request before dispatch, and a workflow's
data activities re-enter decision and scope with the run's actor; the actor rule (a run acts
as the caller who started it unless the definition is published as system, an explicit,
permissioned property; an HTTP-endpoint workflow acts as the request's caller); asynchronous
entry points enter at step 1 with tenant and bucket explicit, as the system actor; connectors
move into the connection system so an external read is a read job and an external write a
durable background action.

## Tasks

Each step compiles and runs on its own. Pre-release rules apply: no compatibility code, dev
tenants reset ([architecture.md](architecture.md)). The order is the dependency order.

### 1. `Crest.Access`: the access machinery

- [ ] **`ICallerContext`** built per request at step 4: tenant, side, organization, roles,
  class ceiling, policy verdicts, culture; system and anonymous callers; the members.md rules
  for `X-Shell` and `X-Org` (denied when they disagree with the bucket) move here from their
  planned place in Members.
- [ ] **The server-side state cache** per (user, tenant, side, organization) carrying the
  tenant permission version; bumped by role, binding and policy writes; compared on every
  use. The client's session copy (members.md) is fed from it and is never authority.
- [ ] **`IAccessDecision`**: one implementation over the platform's permission system, the
  class ceiling and the access policies; answers allow, deny or deny-as-not-found so no
  surface chooses.
- [ ] **`IScopeProvider`** registered per table or content type by its owning module;
  **`ScopeSet`** compiled per scope signature (hash of roles, organization, policy verdicts)
  and provider versions, cached; the same predicates as SQL fragments and as in-memory
  filters. First providers: content items by type and owner, assignments, organizations and
  bindings, media access.
- [ ] **A target with no scope provider is refused**, never served unfiltered.
- [ ] **`IAccessAuditor`**: every decision and execution (operation, caller, resource,
  verdict, scope signature, duration), reads included, through the audit system.
- [ ] **The `AccessGate` middleware chain** (steps 1–6) hosted once where the shell selector's
  startup filter runs; the four pipelines and the renderer take a `ScopedExecution`, never a
  raw session or content manager.
- [ ] **The analyzer** in `Crest.Build` that refuses `ISession`, content-manager writes and
  `IQueryManager` outside `Crest.Access` and the registered sources.
- [ ] **The conformance suite**: fixtures (callers, organizations, roles, policies, rows) and
  expected verdicts, run against every pipeline and every generated surface; a differing
  answer fails the build.
- [ ] **Caller lifetime rules** for circuits and websockets (per-operation build, stamp
  revalidation, teardown) and for workflow bursts (rebuilt per burst; `WorkflowUserContext`
  keeps identity and shell, drops the principal).
- [ ] **Where `Crest.Access` sits**: below `Crest.Data` (decision needed, below).

### 2. The query pipeline and the connection system ([queries.md](queries.md))

- [ ] **The source contract**: typed columns, page tokens, totals where available,
  cancellation, the ScopeSet applied before paging, a stable order added when none.
- [ ] **The SQL connection over the host database** (the dogfooded connector), replacing the
  stock SQL source; tenant prefix and scope applied through the SQL parser.
- [ ] **Statement compilation with the ScopeSet**, cached per query version and scope
  signature; no per-request rewrite.
- [ ] **The system sources**: item by route parameter, current principal, site settings;
  in-process, memoised per request (what blazor-display.md's slots need first).
- [ ] **The read job**: the asynchronous read pipeline on the durable background machinery.
- [ ] **The connection system**: the model, sealed secrets, OAuth, retries with
  `Retry-After`, rate limits, the SSRF guard, moved down from Workflows; protocol features
  (`Sql`, `Rest`, `Soap`, `Rpc`); search engines as connections.
- [ ] The rest of queries.md's list: the builder, column types by aspect, export, the
  plugin registry.

### 3. The registry

- [ ] **Query descriptors** beside activity descriptors: the same `InputDescriptor`s, plus a
  declared output schema; a provider that reads saved queries; attributes for code queries.
- [ ] **The run-query activity**: a query appears once and shows in the palette, the slot
  picker, the picker backend and the API from that entry.
- [ ] **Content operations** (create, update, delete, publish, unpublish per type) as
  code-defined actions wrapping the content manager, so validation, handlers and the audit
  content handler keep firing; the only writes outside workflows.
- [ ] **Operation names per tenant**, mapped from the module's declared name, so a tenant can
  replace a shipped read with a saved query of the same name (the override model of
  blazor-display.md § 5a, applied to operations).

### 4. The action pipelines ([workflows.md](workflows.md))

- [ ] **The `AccessGate` as engine middleware** before the activity invoker; every run and
  every data activity gated.
- [ ] **The actor rule**: run as caller by default; published-as-system as an explicit,
  permissioned definition property; HTTP-endpoint workflows as the request's caller.
- [ ] **Data activities through the gate**: run query, content operations, call connector;
  the content activities that bypass it today (`CopyFields`, `MoveFields`) reworked.
- [ ] **The four-pipeline split made explicit** in the engine's hosting: unit of work and
  durable background as the two action pipelines, with the queue hardening from
  workflows.md › Queues (after-commit emission, partition by correlation id, idempotency).
- [ ] **Connectors into the connection system** (queries.md › Workflows on Queries): the
  activities stay as consumers; webhooks verify through a narrow verifier.
- [ ] **Expressions**: the engine's Liquid handler replaced by the platform's parser and
  context; one Fluid version; the platform-side evaluators deleted; Jint and Fluid limits
  configured in one place (blazor-display.md § 12).

### 5. Move the reads

- [ ] `api/crest` lists (content items, content types, groups, navigation, menus, media,
  users, roles, tenants, features) re-expressed as query operations, module by module.
- [ ] **The pickers**: the option-sources scope resolver's logic becomes scope providers; the
  controller becomes the generated surface of the queries each picker binds; the user
  provider gets scope.
- [ ] Members and Parties reads; the member binding routes check the caller's organization
  through scope, not a global permission.
- [ ] This is the window where two paths exist; one module per change keeps it short.

### 6. Move the writes

- [ ] Content create, update, delete, publish as content operations.
- [ ] Members and Parties writes as actions.
- [ ] Machine clients: the `Api` scheme and bearer tokens on the generated routes
  ([machine-actors.md](machine-actors.md)); the gate sees a machine caller.

### 7. Generate the surfaces

- [ ] **REST + OpenAPI**: one route per operation (`GET` for queries, `POST` for actions),
  parameters and schema from the descriptor.
- [ ] **GraphQL** regenerated from the registry: a field per query, a mutation per action.
- [ ] **RPC** (JSON-RPC; gRPC later).
- [ ] **The Liquid slot binding** `{ query, parameters, path }` → a query operation.
- [ ] **The picker backend** and **the workflow palette** from the registry.
- [ ] A surface adds protocol concerns only (negotiation, batching, rate limits).

### 8. Retire

- [ ] The platform's `api/content`, the stock GraphQL content schema and per-query types,
  `api/queries`, the Lucene and Elasticsearch APIs (search is a connection).
- [ ] The Liquid `query` filter and the Razor query helpers.
- [ ] `Crest.Scripting` and every global method provider.
- [ ] The hand-written `api/crest/*` controllers as each generated route lands.

## The surfaces today, and their fate

| Path | Today | Fate |
| --- | --- | --- |
| `api/crest/*` (about 25 controllers) | one permission per controller, no scope on lists | generated routes over operations (5–7) |
| `api/crest/option-sources` | one gate; content scoped by a local resolver; user provider unscoped | scope providers (1); generated (7) |
| Platform `api/content` | `AccessContentApi` + resource checks | retired (8) |
| GraphQL | execute permission, per-item filters, hand-written query types | regenerated (7) |
| `api/queries/{name}` | per-query permission; sources unchecked; 404 on deny | retired; queries are operations (3, 8) |
| Lucene, Elasticsearch APIs | one permission; no per-item check | retired; search is a connection (8) |
| Liquid `query` filter, scripting | no checks | slot binding through the gate (7); scripting removed (8) |
| Workflow connectors, content activities | no check at execution; publish is the boundary | gate in the pipeline, actor rule, connectors into the connection system (4) |
| Members, Parties | cookie-enriched principal; global checks on org routes | caller context carries side and organization (1); routes generated (5–6) |

## Decisions needed

- [ ] **Where `Crest.Access` sits in the module chain.** Below `Crest.Data` (it scopes data,
  so data depends on it) or beside it. Recommendation: below; the chain becomes core → access
  → data → connections → query → surfaces, with workflows on query.
- [ ] **System actor permission set.** What a system workflow may do that no user may, and
  who may publish a workflow as system. Recommendation: a `System` role editable only by
  tenant admins; publishing as system requires `ManageShippedWorkflows`.
- [ ] **Per-part permissions** ([permissions.md](permissions.md)) as scope providers on
  content fields: the seam in step 1, the first rule when a type needs it.
- [ ] **What OrchardCore offers natively for connections** (carried from queries.md), to be
  researched before step 2 designs the connection model.
