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

**What exists (survey 2026-10-10).** The order today, host then tenant:
`ModularTenantContainerMiddleware` (matches the tenant by host and prefix, opens the shell
scope the whole request runs in, commits or cancels it at the end) →
`ModularTenantRouterMiddleware` (tenant prefix into `PathBase`) → the tenant pipeline: the
`IStartupFilter`s (this is where `BlazorAdminThemeMiddleware` runs: classifies admin, login
and member paths, **authenticates the cookie itself** for admin and member pages, runs the
route permission check, stamps the bucket and shifts `PathBase`) → `UseRouting()` → the
module startups by `ConfigureOrder` (`UseAuthentication` at −150; the workflows engine API
branch with its own authentication at −151; request localization at −100; Crest.Server's
endpoints, CORS and `UseAuthorization` in the default group) → `UseEndpoints`. So steps 1
and 2 exist; step 3 runs twice and in two places (the shell middleware before routing for
pages, `UseAuthentication` after routing for everything else); steps 4–6 do not exist, and
the site bucket is never stamped. Background work (`ModularBackgroundService`) enters
through a synthetic `HttpContext` with `Items["IsBackground"]` and an **empty principal**,
and a platform shortcut skips the whole tenant pipeline for it.

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

What exists: the cookie is per tenant with claims **baked at sign-in** by
`DefaultUserClaimsPrincipalProviderFactory` → `RoleClaimsProvider` (one role claim per role
plus the role's `Permission` claims from `RoleStore`, a cached `RolesDocument`; the admin
role gets no permission claims because `SuperUserHandler` succeeds everything); the
security-stamp validator runs at the Identity default of 30 minutes and is bumped by role
changes, so a permission change reaches a live session only then; `IAuthorizationService`
decides through handlers that each know a piece (`PermissionHandler` over the baked claims,
`RolesPermissionsHandler` for Anonymous and Authenticated, `SuperUserHandler`,
`ContentTypeAuthorizationHandler` for the per-type `View_{type}`/`ViewOwn_{type}` dynamic
permissions, `MemberPermissionCeilingHandler` as a deny-only ceiling, plus media, settings,
indexing and role handlers); the `Api` scheme is a forwarder to OpenIddict validation when
that feature is on, otherwise no bearer token can authenticate; there is **no permission
version** (only a SignalR "permissionsInvalidated" nudge to the UI). Scope logic lives in
four places that each re-derive view-any/view-own per type: `OptionSourceScopeResolver`
(pickers, the only one that also applies assignments through `CrestAssignmentIndex`),
`DefaultContentsAdminListFilterProvider` (stock admin lists), GraphQL `ContentItemFilters`,
and the members cookie enrichment (active organization claim and binding roles). **No
index or part ties a content item to an organization**; organization scope today is roles
per binding and assignments, nothing on the data.

- [ ] **`ICallerContext`** built per request at step 4 from the authenticated identity and
  the server-side state, not from baked cookie claims: tenant, side (from the bucket; API
  calls confirm with `X-Shell`, member side carries `X-Org`, disagreement denied), organization
  (from `MemberOrgBindingIndex`), roles and their permissions (from `RoleStore`'s document),
  class ceiling, policy verdicts, culture. System and anonymous callers. Replaces the members
  cookie-enrichment plan.
- [ ] **The permission version**: a per-tenant counter in a document, bumped by
  `IRoleUpdatedEventHandler`, binding writes and policy writes; carried by every cached
  state entry and compared on use; the SignalR nudge becomes its client notification. The
  cookie keeps identity only; the security stamp stays for sign-out and credential change.
- [ ] **The server-side state cache** per (user, tenant, side, organization) under that
  version; the client's session copy (members.md) is fed from it and is never authority.
- [ ] **`IAccessDecision`** as the one implementation the platform's `IAuthorizationService`
  delegates to: the handlers above collapse into it (baked-claim lookup → caller roles;
  super user; Anonymous and Authenticated roles; the per-type dynamic permissions; the member
  ceiling; the access policies), answering allow, deny or deny-as-not-found. Existing
  `[Authorize]` and `AuthorizeAsync` callers keep working through it during the migration.
- [ ] **`IScopeProvider`** per table or content type, registered by its owner, and
  **`ScopeSet`** compiled per scope signature and provider versions, as SQL fragments and as
  in-memory filters. First providers, lifted from the four places above: content items by
  type (view-any / view-own with owner), assignments (`CrestAssignmentIndex`, narrowing
  only, "no requirements" ≠ "nothing matched"), organizations and bindings, media access.
  `OptionSourceScopeResolver`, `DefaultContentsAdminListFilterProvider` and the GraphQL
  filters are deleted once the providers exist.
- [ ] **Organization scope on data** (decision needed, below): the part or index that says
  which organization a content item belongs to, so a provider can scope by it.
- [ ] **A target with no scope provider is refused**, never served unfiltered.
- [ ] **`IAccessAuditor`**: every decision and execution, reads included. `AuditTrailManager`
  writes through the request's scoped session, so a denial on an exception path would roll
  back with it, and each event costs a document row, an index row and a site-settings read.
  The auditor therefore writes decisions in a child scope that commits on its own, and read
  executions through a batched writer into their own collection (decision needed on volume).
  Categories register the existing way (`IConfigureOptions<AuditTrailOptions>`).
- [ ] **The `AccessGate` middleware chain** as the tenant startup filter that
  `BlazorAdminThemeMiddleware` is today: it becomes the shell selector (all three buckets
  stamped, Site included) **and owns authentication** for every request (cookie or `Api`
  scheme per request kind, once), then builds the caller; `UseAuthentication` at −150 and
  the engine API's own authentication branch go. The four pipelines and the renderer take a
  `ScopedExecution`, never a raw session or content manager.
- [ ] **Background entry points enter the path**: a `RunAs(caller)` helper replaces the empty
  principal in `ModularBackgroundService`'s synthetic context and in the workflows'
  shell-scoped consumers, task handler and bookmark worker; the system caller is explicit
  there, and it is the **only** way a system caller comes to exist: built in process, never
  from a credential (Decisions › System actors). The platform's `IsBackground` shortcut around the tenant pipeline is reviewed so the
  gate still runs.
- [ ] **The `Api` scheme for machines**: OpenId validation enabled by the host recipe, or an
  opaque-key handler behind the same forwarder (machine-actors.md); either way the gate sees a
  machine caller.
- [ ] **The analyzer** in `Crest.Build` that refuses `ISession`, content-manager writes,
  `IDbConnectionAccessor` and `IQueryManager` outside `Crest.Access` and the registered
  sources.
- [ ] **The conformance suite**: fixtures (callers, organizations, roles, policies, rows) and
  expected verdicts, run against every pipeline and every generated surface; a differing
  answer fails the build. Its first fixtures are the four scope places above, so nothing
  loosens when they are deleted.
- [ ] **Caller lifetime rules** for circuits and websockets (per-operation build, stamp
  revalidation, teardown) and for workflow bursts (rebuilt per burst; `WorkflowUserContext`
  keeps identity and shell, drops the principal).
- [ ] **Two projects**: `Crest.Access.Abstractions` (contracts and `ScopeSet`) below
  `Crest.Data`, `Crest.Access` (caller builder, decision, cache, gate) beside `Crest.Queries`.

### 2. The query pipeline and the connection system ([queries.md](queries.md))

What exists: `DefaultQueryManager.ExecuteQueryAsync` is already the single choke point every
caller goes through (API, GraphQL, Liquid, scripting, Razor helpers, deployment, workflows),
resolving a keyed `IQuerySource` and doing no authorization. `SqlQuerySource` **renders the
SQL template through Liquid with the parameters as variables**, then parses with
`SqlParser` (the Cyqwel AST, a record-based rewriter `PlatformSqlRewriter` that already
prefixes table names, qualifies columns, fills parameter defaults and fixes limits per
dialect; `SelectStatement` exposes `Where`, so predicates can be inserted in `VisitSelect`,
including subqueries, CTEs and set-operation branches), then runs it with Dapper over
`IDbConnectionAccessor`; with `ReturnContentItems` it loads the items through the session
with no view check. There is **no table allow-list**: a query may select from the YesSql
`Document` table and read any JSON. Paging is only the parser's limit handling. Index tables
are the map indexes (`ContentItemIndex`, `CrestAssignmentIndex`, `MemberOrgBindingIndex`,
`AuditTrailEventIndex`, …). The per-tenant store, prefix and dialect come from
`ShellSettings` in `Crest.Data.YesSql`.

- [ ] **Parameters only, never templating.** SQL templates stop being Liquid-rendered;
  parameters bind as parameters. A Liquid-rendered statement is an injection surface and
  defeats the scope rewrite.
- [ ] **The source contract**: typed columns (from the reader's schema and the builder's type
  cache), page tokens, totals where available, cancellation, the ScopeSet applied before
  paging, a stable order added when none.
- [ ] **Scope injection in `PlatformSqlRewriter.VisitSelect`**: for every scoped table or
  alias in `From`, joins, subqueries, CTEs and set-operation branches, conjoin the ScopeSet's
  predicate into `Where`; a table with no provider refuses the query. **A table allow-list**:
  the index tables with providers; the `Document` table only through a provider that scopes
  by the owning index.
- [ ] **Statement compilation cached** per query version and scope signature; no per-request
  rewrite.
- [ ] **`ReturnContentItems` loads through the gate** (the content provider's predicate, not a
  per-item check after the fact).
- [ ] **The SQL connection over the host database** as the dogfooded connector, replacing
  the stock SQL source; dialect paging through YesSql's `ISqlDialect`.
- [ ] **The system sources**: item by route parameter, current principal, site settings;
  in-process, memoised per request (what blazor-display.md's slots need first).
- [ ] **The read job**: the asynchronous read pipeline on the durable background machinery;
  streaming through `DbDataReader`.
- [ ] **The connection system**: the model, sealed secrets, OAuth, retries with
  `Retry-After`, rate limits, the SSRF guard, moved down from Workflows; protocol features
  (`Sql`, `Rest`, `Soap`, `Rpc`); search engines as connections (Lucene and Elasticsearch
  sources become connections, their APIs retired).
- [ ] The rest of queries.md's list: the builder, column types by aspect, export, the
  plugin registry.

### 3. The registry

What exists: the engine's `ActivityDescriptor` comes from `IActivityProvider`s
(`TypedActivityProvider` for CLR activities, `WorkflowDefinitionActivityProvider` for each
published definition, which turns a definition's `Inputs`/`Outputs` into synthetic
`InputDescriptor`/`OutputDescriptor`s so a flow is itself a typed activity);
`IActivityDescriptorModifier` post-processes every descriptor (Crest's
`FieldDependencyDescriptorModifier` is the precedent for adding Crest data to
`CustomProperties`); `IActivityRegistryPopulator` runs the providers from Crest's
`PopulateRegistriesTask`. Beside it Crest keeps its **own** catalog
(`WorkflowRegistryCatalog` over `IWorkflowTriggerProvider`, `IWorkflowActivityProvider`,
`IWorkflowFlowProvider`, `IWorkflowConnectorProvider`, hook slots) of catalog entries mapped
onto adapter activity types, served at `api/crest/workflows/registry`. No descriptor kind
carries an output schema today.

- [ ] **Query descriptors are engine `ActivityDescriptor`s** emitted by a Crest
  `IActivityProvider`: one per saved query (from content) and one per code query (from
  attributes), with the query's parameters as `InputDescriptor`s, its columns as typed
  `OutputDescriptor`s, and the output schema (pageable, total, column types) in
  `CustomProperties` under a Crest key, the way field dependencies are carried. This gives
  the palette, `/descriptors/activities`, the studio's hint handlers and the options endpoint
  for free. The descriptor's `ClrType` is the one run-query activity.
- [ ] **The run-query activity**: executes its descriptor's query through the query pipeline
  with the run's actor; outputs the typed rows and a page token.
- [ ] **Content operations** (create, update, delete, publish, unpublish per type) as typed
  activities wrapping the content manager, so validation, handlers and the audit content
  handler keep firing; the only writes outside workflows.
- [ ] **One registry** (ruled, Decisions): Crest's catalog kinds (triggers, flows,
  connectors, hook slots) become engine descriptors with grouping and position in
  `CustomProperties`; the catalog's consumers move to the engine registry; the catalog goes.
- [ ] **Operation names per tenant**, mapped from the module's declared name, so a tenant can
  replace a shipped read with a saved query of the same name (the override model of
  blazor-display.md § 5a, applied to operations).

### 4. The action pipelines ([workflows.md](workflows.md))

What exists (survey 2026-10-10, `src/Crest.Workflows`): the engine runs the default
pipelines (`CrestWorkflowsFeature.cs:38`; workflow: heartbeat, engine exception handling,
persistent variables, exception handling, scheduler; activity: exception handling, execution
logging, notifications, log-persistence evaluation, `BackgroundActivityInvokerMiddleware` as
the terminal, which subclasses `DefaultActivityInvokerMiddleware`). Crest adds **no**
middleware today; its extension is service replacement in `Server/Features/CoreStartup.cs`
(`ICommitStateHandler` → `UnitOfWorkCommitStateHandler`, `IBackgroundActivityScheduler` →
`DurableBackgroundActivityScheduler`, `IBackgroundActivityInvoker` →
`ShellScopedBackgroundActivityInvoker`, the scheduled-task handler, the bookmark queue
worker, the background consumers). The two action pipelines already exist in code, unnamed:
the unit of work (`WorkflowUnitOfWork`, `UnitOfWorkCommitStateHandler`, `WorkflowHookRunner`,
`WorkflowAtomicityAnalyzer`, `WorkflowObjectLock`, the 409 filter) and durable background
(`WorkflowAfterCommit`, `WorkflowStimulusQueue`, `DurableBackgroundActivityScheduler`, the
shell-scoped consumers, task handler and bookmark worker). `WorkflowStateCommitted` is sent
**before** the YesSql commit; only `WorkflowAfterCommit.Enqueue` is after-commit.

- [ ] **The `AccessGate` as engine middleware.** Register through
  `AddCrestWorkflows(...)` (`CoreStartup.cs:73`) with `WithWorkflowExecutionPipeline` /
  `WithActivityExecutionPipeline`: a workflow-level gate inserted after `Reset` (the run's
  actor and decision, once per burst) and an activity-level gate inserted before the terminal
  invoker (decision and scope for each data activity). Keep the engine's default middleware
  list otherwise; `pipeline.Insert` rather than a rebuilt list, so engine updates do not
  silently drop it.
- [ ] **The actor rule.** `WorkflowUserContext` (`Server/Contexts`) today captures **every
  claim** of the principal into the instance input (`InputKeys.Actor`) and rebuilds a
  principal with `ToPrincipal()` for `WorkflowAuthorizer`. Change it to identity and shell
  only (user id, user name, tenant, authentication type); the gate builds the caller from
  that identity at every burst through `Crest.Access`, and `WorkflowAuthorizer` and
  `RequirePermission` ask `IAccessDecision` instead of a rebuilt principal. The five capture
  sites (`PlatformWorkflowManager`, `WorkflowHookRunner`, `WorkflowTriggerPublisher`,
  `ContentEventHandler`, the webhook controller) keep capturing, with the reduced shape.
  Published-as-system: an explicit definition property, permissioned
  (`ManageShippedWorkflows`), that makes the gate build the system caller instead.
- [ ] **Actors where none exists today.** Background scopes have no `HttpContext`, so
  `IWorkflowUserContextAccessor.Capture()` there yields Anonymous: timer, cron and delay
  runs, the bookmark queue and the background consumers. Rule: a resume takes the actor from
  the instance input; a timer- or cron-started flow runs as the system actor, declared on
  the definition; a flow with neither is refused by the gate.
- [ ] **HTTP-endpoint workflows through the request path.** `HttpWorkflowsMiddleware`
  (`engine/.../Crest.Workflows.Http`, mounted by `Server/Features/HttpStartup.cs` under the
  `CrestWorkflows:Http` base path, default `/workflows`) runs the flow inline in the request
  with its own `IHttpEndpointAuthorizationHandler` and captures **no** actor. It becomes a
  dispatch target of step 7 of the path: the gate has run, the caller is the request's, the
  trigger's `Authorize`/`Policy` inputs are replaced by the operation's decision, and the run
  is the request's unit of work. The base path becomes a generated-surface route prefix.
- [ ] **Data activities through the gate.** The run-query activity and the content
  operations (step 3) are the only data activities; `Contents/FieldActivities.cs`
  (`CopyFields`, `MoveFields`, which call `IContentManager` directly) are rewritten on content
  operations; `CallConnector`/`PollConnector` go through the connection system.
- [ ] **Name and host the two action pipelines as such**: the unit of work and durable
  background become the two named hosts in `CoreStartup`, with the queue hardening from
  workflows.md › Queues (partition by correlation id, idempotency keys; after-commit emission
  is already `WorkflowAfterCommit`). Anything that must be after-commit uses it, never
  `WorkflowStateCommitted`.
- [ ] **The engine API gate becomes a consumer of `IAccessDecision`.**
  `CrestWorkflowsApiSecurityMiddleware` (`Server/Security`) maps Crest permissions to engine
  claims and enforces the run gate per definition; it keeps the mapping but asks the one
  decision, and sits after the request path's steps 1–6 rather than carrying its own
  authentication branch.
- [ ] **Connectors into the connection system** (queries.md › Workflows on Queries): the
  model, sealed secrets, OAuth, `ConnectorHttp` (SSRF guard, redirects off, size limits),
  rate limiters, token cache and `ConnectionResilienceStrategy` move down; the activities
  stay as consumers; the webhook controller verifies through a narrow verifier.
- [ ] **Expressions**: the engine's Liquid handler replaced by the platform's parser and
  context; one Fluid version (2.40 platform, 2.31 engine today); the platform-side
  `LiquidWorkflowExpressionEvaluator` and `JavaScriptWorkflowScriptEvaluator` deleted with
  the stock tasks ported to `Input<T>`; Jint and Fluid limits configured in one place
  (blazor-display.md § 12).

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

- [x] **Organization scope on data.** Ruled (2026-10-10): **both.** An organization part
  with an index (`OrganizationId`) that every scoped type carries, filled by the owning
  module, is the row's owner; assignments (`CrestAssignmentPart` with an organization
  target) are grants. The hierarchy's subtree scoping (members.md) joins on the owner column.
- [x] **What is audited.** Ruled (2026-10-10): **actions always; reads selectively.** Every
  action (unit of work, durable background) and every denial is recorded. Reads are recorded
  when impersonation is active, when they go through an external connection, when they are
  read jobs (exports, bulk reads), and for types or fields a tenant flags as sensitive;
  ordinary scoped reads are not. This matches what standard systems do (change logs always;
  read-access logging opt-in per sensitive object, as SAP's read access logging, Dynamics'
  optional read audit and CloudTrail's opt-in data events; HIPAA-style regimes turn the
  opt-in on). **Read logging can also be switched fully on** as a tenant setting, and when
  it is, logs are **per user request**, one event per operation, no sampling. Volume is
  handled by **retention settings**, not by recording less: short by default, extended per
  tenant where a legal regime (HIPAA and the like) requires it. The read events go to their
  own collection through a batched writer; trimming runs per collection on its own schedule.
- [x] **Who authenticates.** Ruled (2026-10-10): **everything authenticates, in this
  order:** tenant first (which pages even exist is a tenant question), then the theme shell
  from the URL, then the user (anonymous is authenticated as the Anonymous caller, since it
  is the first check on use and on the share settings of content, pages and media), then
  permissions. The gate owns authentication before routing; `UseAuthentication` goes.
- [x] **One registry.** Ruled (2026-10-10): the engine's registry (`IActivityRegistry`,
  from Elsa) is the registry. Crest's `WorkflowRegistryCatalog` and its providers
  (`IWorkflowTriggerProvider`, `IWorkflowActivityProvider`, `IWorkflowFlowProvider`,
  `IWorkflowConnectorProvider`, hook slots) are migrated onto it: each catalog kind becomes
  engine descriptors with grouping and position in `CustomProperties`, and every consumer of
  the catalog (the registry page, `api/crest/workflows/registry`, the trigger publisher, the
  stock-activity and connector-operation providers) reads the engine registry instead. The
  catalog is deleted when its last consumer moves.
- [x] **HTTP-endpoint workflows.** Ruled (2026-10-10): generated **under `/workflows/`**,
  consolidated so a caller knows it is calling an action; later
  `/workflows/{group}/{name}` once workflows have groups and an organization system. For now
  the engine's existing base path and `HttpEndpoint` handling stay as they are, behind the
  gate.
- [x] **Structured queries compiled through YesSql, not SQL text.** Ruled (2026-10-10).
  A tenant-authored query is a step list (source index or content
  type, filters, joins, projections, sort, page) compiled through YesSql's `SqlBuilder` and
  dialects into parameterised SQL with the tenant prefix; the ScopeSet's predicates are added
  as filter steps for every table the plan touches, so injection is impossible by
  construction and the AST rewriter is not needed for tenant data. SQL text survives only for
  external SQL connections, where the external system applies its own permissions and the
  author is an administrator. Reads and writes then share one stack: writes through the
  content manager onto the YesSql session, reads through the builder onto the same store.
  A Liquid template names query operations only, so it is read-only by kind. The external
  SQL connector keeps SQL text for now and **its injection fix is deferred** (parameters
  only there too, later).
- [x] **Where `Crest.Access` sits in the module chain.** Ruled (2026-10-10): the
  abstractions split below. The chain is core → data →
  connections → query → surfaces, with workflows on query. `Crest.Access` has two halves
  that want opposite places: the **contracts** (`ICallerContext`, `IAccessDecision`,
  `IScopeProvider`, `ScopeSet`, `ScopedExecution`) must sit **below `Crest.Data`**, because
  the data layer hands out `ScopedExecution` instead of raw sessions and the analyzer
  enforces that; the **implementation** must sit **above `Crest.Data`**, because building a
  caller reads roles, bindings and policies from the store. Recommendation: the platform's
  usual split, `Crest.Access.Abstractions` below `Crest.Data` and `Crest.Access` beside
  `Crest.Queries`, so the chain is core → access contracts → data → connections → access
  implementation and query → surfaces.
- [x] **System actors.** Ruled (2026-10-10): the **tenant system actor acts as a tenant
  admin**, always; an **organization system actor acts as that organization's admin**,
  always (background work started on behalf of an organization, such as its scheduled flows,
  runs with the organization admin's scope and ceiling). Publishing a workflow as system
  requires `ManageShippedWorkflows`. **Only the system can use the system credentials**
  (ruling 2026-10-10): a system caller is never produced from a request, a token, a cookie
  or a job payload; it exists only where the path's step 1 admits a background entry point
  or a definition published as system, built in process by `Crest.Access` with no
  credential a user could present. There is no system user account, no system role a user
  can be given, and no API scheme that yields the system caller. Impersonating the system
  is therefore impossible by construction, and the audit records the definition or job that
  ran as system, never a user.
- [ ] **Per-part permissions** ([permissions.md](permissions.md)) as scope providers on
  content fields: the seam in step 1, the first rule when a type needs it.
- [ ] **What OrchardCore offers natively for connections** (carried from queries.md), to be
  researched before step 2 designs the connection model.
