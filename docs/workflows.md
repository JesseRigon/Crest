# Workflows

How Crest's workflow service works: what a workflow is here, who may do what to one, how
a flow gets started, how a run commits (or does not), and how a downstream module builds on
top of it. Workflows are the **action** kind of operation: every read and write in Crest goes
through one registry, one request path, four pipelines and one access machinery, described,
with its task list, in [Operations](#operations-one-registry-one-request-path-four-pipelines-one-access-machinery).
Design not yet built is under [Still to build](#still-to-build) and open questions under
[Open questions](#open-questions); the flowchart of a run's life is [workflows.mmd](workflows.mmd).

## What it is

`Crest.Workflows` is the tenant's workflow service and its only workflow engine. The engine is
a vendored fork of Elsa 3 (activities, flowcharts, bookmarks, bursts, the designer); platform
modules register engine activities and raise registry triggers like any Crest module. The
designer (the forked Studio, Blazor WebAssembly) is loaded on demand into the Crest admin.

What the system does:

1. **Connects modules to each other without references.** A stage change in one module
   creates a party role; a posted document starts a tenant's own flow. Modules register
   activities, triggers and flows; the definition holds the connection.
2. **Connects to Crest modules.** Content events, users and roles, e-mail, notifications:
   everything the platform already does is a palette item, not a re-implementation.
3. **Connects to external services.** Outgoing HTTP calls with stored credentials, incoming
   signed webhooks, polling on a schedule, retries and rate limits, without code.
4. **Is authored by tenants in the Crest admin**, with drafts, publishing and versions.
5. **Is multi-tenant and permissioned like everything else:** the platform's permissions and
   roles through the one access machinery, per-shell data and execution, shipped definitions
   migrated by code.

**Where it lives.** Workflows is part of the application layer
([architecture.md](architecture.md)): `Crest.Workflows` in the Crest repository, engine
included. The engine API prefix is `crest-workflows/api` and activity type names are
`Crest.Workflows.*`. A downstream module's activities, hook attachments and shipped flows are
that module's own.

Workflows is a **registry** like Parties and the other core registries: modules register
what they contribute (triggers, activities, hook slots, shipped flows, connectors) through
contracts in `Crest.Workflows.Abstractions`, and never reference the engine module. The
registry is read at `GET api/crest/workflows/registry`.

| A module registers | Through | Example |
| --- | --- | --- |
| Triggers it raises | `IWorkflowTriggerProvider`, raised with `IWorkflowTriggerPublisher.PublishAsync(key, correlationId, payload)` | `party.role-created` |
| Activities (palette entries) | `IWorkflowActivityProvider` + an engine `Activity` class | `Create party role`, `Resolve party` |
| Hook slots and system attachments | `IWorkflowHookSlotProvider`, `IWorkflowHookAttachmentProvider`, run with `IWorkflowHookRunner.RunAsync(slot, correlationId, payload)` | `flow.hook` |
| Shipped flows | `IWorkflowFlowProvider` (definition JSON embedded in the module) | a module's `Data/workflows/*.json` |
| Connectors | `IWorkflowConnectorProvider` | `http`, `webhook` |

Trigger payloads are **ids and scalars only**; an activity re-reads the object through its
own registry service.

### Workflows builds on top of the module APIs

Workflows builds on the API surfaces modules declare and register, not beside them: automated
e-mails, invoice-to-bill, budgeting, approval flows from downstream applications call the
operations and APIs their modules registered. A module's domain API is never replaced by, or
moved into, a workflow endpoint ([architecture.md › Workflows builds on top](architecture.md#workflows-builds-on-top)).

Workflows hosts nothing itself. Crest.Server's API registry has two layers
([architecture.md › Two layers in each registry](architecture.md#two-layers-in-each-registry)):

- **Declared**: from module code, built when the tenant shell builds, following feature
  enablement, never stored in the database. Crest.Workflows' management API
  (`api/crest/workflows/*`: definitions' access, registry, hooks, connections, approvals,
  pending, triggers) is declared like any module's and registered there.
- **Defined**: data in the tenant database, changeable at runtime, served by Crest.Server
  through a dynamic source invalidated on publish. A published workflow's `HttpEndpoint`
  routes are defined entries in the Server's API registry, as are the operation endpoints
  generated from the operations registry, not a private table of the engine's.

**Generated surfaces.** A module declares its API; the implementation behind a declared route
may be generated from the operations registry (a query or an action), and a generated route is
a registry entry like any other ([Generate the surfaces](#7-generate-the-surfaces)).

In code: `WorkflowHttpEndpointSource` (`Server/Api/WorkflowHttpEndpoints.cs`) is the defined
source. It lists the routes of every `HttpEndpoint` trigger and waiting bookmark of the tenant
(the engine's `IHttpEndpointRoutes`) under the `CrestWorkflows:Http` base path (default
`/workflows`), plus a catch-all there that answers 404, and each route's request goes to the
engine's `HttpWorkflowRequestHandler` with the route template that matched. When the engine
indexes an added or removed `HttpEndpoint` trigger or bookmark, `NotifyHttpEndpointRoutesChanged`
tells `WorkflowHttpEndpointsInvalidator`, which invalidates the source; a reload that yields the
same routes leaves the tenant's endpoints alone. The engine's management API
(`crest-workflows/api`, § The engine API) is the declared contributor `WorkflowsManagementApi`,
behind the engine's `ApiSecurityMiddleware`.

## Architecture

```
 Crest admin (Blazor WASM, Radzen)
 ├─ Workflows pages: Definitions, Instances (Studio, lazy), Connections, Approvals, Hooks (Crest)
 └─ Designer: the forked Studio (MudBlazor), loaded on demand in its own container
        │  engine API (minimal APIs) at ~/crest-workflows/api, tenant cookie + antiforgery,
        │  Crest permissions mapped onto the engine's per-endpoint grants
        ▼
 Crest.Workflows  (a core registry)
 ├─ engine/   vendored elsa-core 3.6.0 (renamed Crest.Workflows.*)
 ├─ Server/   the integration: stores on YesSql per shell, definitions as content items,
 │            the API gate, the registry, ownership, access, connectors, approvals
 ├─ Contents/ content triggers and tasks (feature Crest.Workflows.Contents)
 ├─ Abstractions/ the contracts modules bind to (descriptors, IWorkflowTriggerPublisher, constants)
 ├─ blazor-wasm/ + studio-host/   the admin pages and the lazily loaded Studio container
 └─ designer/ vendored elsa-studio 3.6.0
```

**The engine is Elsa 3.6, vendored and renamed** (`engine/`, `designer/`; `UPSTREAM.md` and
the `UPSTREAM-COMMIT` files record the upstream tag and commits). Upstream is a diff source,
not a dependency: no `Elsa*` package exists in the graph; namespaces, assembly ids, activity
type ids (`Crest.Workflows.WriteLine`), the API prefix (`crest-workflows/api`) and the
YesSql collections (`CrestWorkflows_*`) are renamed. Licences: elsa-core and elsa-studio
MIT, the original integration BSD-3-Clause, Jint (BSD-2),
Fluid (MIT), Polly (BSD-3), MudBlazor (MIT). At every upstream diff, refuse anything not MIT /
Apache-2.0 / BSD.

**Crest.Workflows is core.** Every module that registers or raises anything depends on it
(Parties and every downstream module that registers declare the feature); there is no no-op
mode. Crest's UI and API core never references it; the generic pieces the module needed from
Crest (late service containers, lazy module pages, JS components, antiforgery access) are
Crest features any module can use.

**Where code lives**

| Piece | Where |
| --- | --- |
| Contracts modules bind to: `WorkflowTriggerDescriptor`, `WorkflowFlowDescriptor` (+ `WorkflowOwnership`), `WorkflowActivityDescriptor`, `WorkflowConnectorDescriptor`, the provider interfaces, `IWorkflowTriggerPublisher`, `IUnitBoundary`, `WorkflowsConstants` (routes, permissions, roles, property keys) | `Abstractions/` |
| Engine wiring, stores, publisher, API gate, permissions, ownership guard, access handler and API, registry catalog/importer/controller, triggers, connectors, approvals, navigation | `Server/` |
| Content triggers and tasks | `Contents/` |
| Admin pages, `StudioHost`, the canvas node stand-in, `crest-lazy-assemblies.txt` | `blazor-wasm/` (always loaded, thin) |
| Studio's own container (`StudioServices.BuildAsync`), cookie API handler, views | `studio-host/` (lazy) |
| Activities on core objects | the module that owns the object (Parties: CreatePartyRole, ResolveParty) |
| Triggers a registry raises | the owning module's service or content handler |
| Shipped flows | the owning module's `Data/workflows/*.json`, embedded |

## Engine

**One engine.** `CoreStartup` (`Order = int.MaxValue`, last by feature dependency and then
by order) wires the engine into the tenant container. A platform module that contributes
activities or triggers references `Crest.Workflows.Abstractions` (the contracts) and the
engine's `Crest.Workflows.Core` and `.Engine` (the activity base classes and the module
builder), and from a startup gated on the `Crest.Workflows` feature registers an
`IWorkflowActivityProvider` / `IWorkflowTriggerProvider` for the registry and calls
`services.ConfigureCrestWorkflows(w => w.AddActivitiesFrom<Startup>())`. Triggers are
raised through `IWorkflowTriggerPublisher` from the module's own handlers. There is no stock
workflow module, bridge or second evaluator: the platform's activities are engine activities
(§ Platform activities).

### The engine API

The management API the designer, the admin pages and the browser checks consume:
`crest-workflows/api`, 73 operations (86 route and verb pairs; definitions, instances, journal,
descriptors, bookmarks, events, tasks, activity tests, features, resilience, the package
version), written as ASP.NET minimal-API handlers in
`engine/src/modules/Crest.Workflows.Api/Endpoints`, one static class per area
(`WorkflowDefinitionEndpoints`, `WorkflowInstanceEndpoints`, `DescriptorEndpoints`,
`ActivityExecutionEndpoints`, `RunEndpoints`, `PlatformEndpoints`, `ResilienceEndpoints`;
execute, export and import of definitions and instances have their own files), gathered by
`EngineApiEndpoints.MapEngineApi(prefix)`, which the declared contributor
`WorkflowsManagementApi` calls on the registry's group (api.md › The seam: the group carries the
antiforgery and exception filters). The Expressions.JavaScript and Resilience modules' endpoints
live there too; no module registers endpoint assemblies.

- **Binding is explicit.** Every handler parameter names its source: `[FromRoute]`,
  `[FromQuery]` (an `[AsParameters]` class for the list queries, each property attributed),
  `[FromForm] IFormFileCollection` for the two file imports (`.DisableAntiforgery()`, since the
  framework's form check would double the group filter's), `[FromServices]` for services. A
  JSON body is an `EngineBody<T>` parameter: the engine API's one `BindAsync` source, which
  deserializes through the engine's `IApiSerializer` (the polymorphic activity JSON, expando
  inputs and converters the host's JSON options do not know); an empty body binds a new `T`, a
  malformed one is answered by `EngineBodyFilter` on the group with the error contract's 400.
  Responses are written through the same options (`EngineResults.Json`), never the host's.
- **Errors are the error contract's** (api.md): `ApiProblems.Validation` problem details, the
  engine's non-field messages under the empty key `""`; a bare 404, 403 (a status, never the
  scheme's Forbid), 204 and empty 200 where the engine answered so. The designer reads the
  `errors` map through Refit's `ValidationApiException`. Routes, verbs, request and response
  shapes and status codes are the engine's own; `EngineResults` is the one answer helper.
- **Validation** is the request DTO's, on the shared ruleset (api.md › Validation): the DTOs
  with rules derive from `Validated<T>` (the instance list's page, page size and status names,
  its timestamp filter columns; bulk cancel's definition id with version options; dispatch's
  input shape), run by the registry's validation filter before the handler, the `errors` map
  keyed by wire name. A body bound as `EngineBody<T>` is validated through the wrapper, which
  runs the body's rules under the body's own keys. The domain checks (not found, not published,
  the publisher's validation errors) stay in the handlers as the error contract's 400s.
- **Authorization** is a policy per endpoint, `RequireAuthorization(EngineApiPolicies.X)` /
  `.AllowAnonymous()` (bookmark resume, the public event trigger, the package version and the
  response simulator, as before): `EngineApiPolicies` names one policy per set of engine
  permission names (`read:workflow-definitions`, `read:* | read:activity-descriptors`, ...),
  satisfied by a `permissions` claim naming any of them or `*`, registered by
  `WorkflowsApiFeature`. The names are what `ApiSecurityMiddleware` grants per request
  (§ Ownership and permissions › The API gate). The read-only guard (`NotReadOnlyPolicy`, the
  management option and a definition's read-only or system flag) is resource-based and stays
  in the handlers.

**Tenant citizenship.** One engine per shell: every engine service, store and hosted service
lives in the tenant container. Definitions are `WorkflowDefinition` content items
(`WorkflowDefinitionPart`; drafts and versions are the content system's); instances, stored
triggers, bookmarks, execution logs and activity execution records are YesSql collections with
per-shell migrations. The file lock directory is `<data root>/Sites/<tenant>/locks`;
`MediatorOptions.JobWorkerCount = 1` per shell; `JobRunnerHostedService` and
`ActivateTenants` are started from a deferred task on activation and stopped on
termination; `LocalScheduler` (timers, cron) is enabled per shell and disposable so a
released shell's timers stop. Containers are disposed synchronously, so the engine's
async-only tenant service is replaced (`SyncDisposableTenantService`) and a scan test pins
the async-only surface of the engine assemblies.

**Definitions store and publisher.** `YesSqlWorkflowDefinitionStore` maps
`WorkflowDefinitionPart` ↔ `WorkflowDefinition`; `ContentItemWorkflowDefinitionPublisher`
publishes through the content manager (a save with `publish: true` on a new definition
stores the draft first). The engine's definition notifications are raised once, by
`WorkflowDefinitionContentHandler` (Retracted with the definition as unpublished, so the
indexer drops its triggers). Both call the ownership guard (below). No store commits
mid-run: they flush, and the burst's shell scope commits ([Units of work](#units-of-work-how-a-run-commits)).

**Fork changes worth knowing** (engine): `FindWorkflowInput<T>` (default when a key is absent;
`GetWorkflowInput` throws, which would fault flows started from the API, a timer or a webhook);
`LocalScheduler : IDisposable`; `BookmarkQueueWorker.Stop()` tolerates a worker that never
started (a tenant deactivated after a failed setup); `ResumeWorkflowAsync` virtual with
`BuildResumeOptionsAsync` factored out; `WorkflowStateExtractor` keeps undeclared input across
bursts; `ActivityDescriber` carries `[Activity(RunAsynchronously = true)]` onto the descriptor;
`StimulusSenderOptions.QueueUnmatchedBroadcastStimuli` is off (a broadcast nobody listens to is
not written to the bookmark queue on every content save). Designer: Radzen 11 and ASP.NET
10.0.9 (Crest's versions), the ILocalizer clash aliased, `data-testid` on Save/Publish/Run, the
ClientLib webpack bundles built by `designer/Directory.Build.targets` when missing (gitignored).

**Expressions.** The engine's Liquid handler uses the platform's parser and context
(`PlatformLiquidTemplateManager`, one Fluid version everywhere); JavaScript is Jint.

**Speed** (checked in the engine source): the definition JSON is parsed once and cached
(`IWorkflowDefinitionCacheManager`, enabled) into a graph of compiled C# activities; only
the walk between nodes is interpreted. The costs are JavaScript/Liquid expressions (Jint,
per run) and persistence (instance state, execution records, journal). Dials that exist:
commit strategies (`IWorkflowCommitStrategy`, `IActivityCommitStrategy`),
`LogPersistenceMode` per activity, literal/typed inputs instead of script.

**Performance dials.** The engine's default workflow commit strategy is none: a burst
persists once, at its end, through the commit handler, which is exactly the unit model, so no
strategy is configured; an activity-level strategy a designer picks is a flush within the unit,
never a commit. Log persistence stays at the engine default (inputs and outputs journaled): the
posting flows' journal is the audit trail. Compile-on-publish is not done: the graph
materializer's cache (`UseCache`) already keeps parsed graphs, and nothing measured points at
graph interpretation. Measure before compiling.

**After-commit dispatch.** Stimuli are never sent through the engine's `IStimulusDispatcher`
(its background worker is a hosted service of the tenant container, which the host never
starts, and its scopes carry no `ShellScope`); `WorkflowStimulusQueue` sends them as
deferred work, one child scope per stimulus, after the raising scope's session commits
([Queues](#queues)).

**Extend the engine, never duplicate it.** The vendored engine has an in-process mediator
(`INotificationSender`/`ICommandSender`, `CommandStrategy.Background` onto an in-memory
channel), dispatchers over it (`IEventPublisher`, `IStimulusDispatcher`, `IWorkflowDispatcher`),
a durable bookmark queue (stimuli that arrive before their bookmark), background activities
(bookmark → out-of-band run → captured outputs → resume), commit strategies, a Polly-based
resilience feature, scheduling, distributed locks. None of it knows the host's transaction.
Crest adds what it lacks: ownership, the registry, hooks, units, field dependencies, approvals,
connections, the after-commit queue and `RegistryTrigger` (thin glue over `IStimulusSender`).
Two-phase calls are the engine's background activities, and connector retries are the
engine's resilience feature (`ConnectionResilienceStrategy`; moving into the connection
system, [Connectors](#connectors)). Scheduled tasks, the background command and notification
channels and the bookmark queue worker run in shell scopes (`ShellScopedRunScheduledTaskHandler`,
`ShellScopedBackgroundConsumers` started at activation, since the host never starts a tenant
container's hosted services; `ShellScopedBookmarkQueueWorker`), so every entry point is a unit.

## Ownership and permissions

Every definition has an **ownership tier**, kept in its custom properties:

- **system**: code's. Shipped by a module, read-only for every user including the
  administrator, upgraded in place when the module bumps its version. The posting flows.
- **shipped**: a template a module ships that the tenant may edit. Editing marks it
  *forked*; a forked copy is not upgraded until it is **reset** to the shipped version
  (`POST api/crest/workflows/registry/flows/{key}/reset`), which keeps the tenant's edits in
  the version history. A flow shipped as a draft is turned on by publishing it.
- **tenant**: the tenant's own.

Permissions are integrated with the platform's grant system, not a separate ACL.

**Permission set** (`Server/Permissions.cs`; names in `WorkflowsConstants.Permissions`):
`ViewCrestWorkflows`, `EditCrestWorkflows`, `PublishCrestWorkflows`, `RunCrestWorkflows`,
`ManageShippedCrestWorkflows`, `ManageCrestWorkflowConnections` (☐ to become the connection
system's `ManageConnections`, [queries.md › Connections](queries.md#connections-the-protocol-modules-and-the-data-layer)), each implied by the `ManageWorkflows`
umbrella, so existing roles keep working. Approvals are decided per task by role or permission
(`all=true` listing needs `ManageWorkflows`). Pages and controllers take the specific
permission: Definitions and Instances pages, the registry and the triggers list → View;
Connections page and API → Manage connections; triggers reindex → Publish. The stock
stereotypes give Administrator **and Editor** `ManageWorkflows`, so the finer set ships as two
roles (also the provider's stereotypes): `WorkflowEditor` (View, Edit, Publish, Run, admin
panel) and `WorkflowViewer` (View, admin panel), created on install when the tenant has no role
of that name (`WorkflowRolesFeatureEvents`).

**Samples.** The feature `Crest.Workflows.Samples` (off by default) registers the trigger
`sample.content-published` (every published item; payload ContentItemId, ContentType,
DisplayText), a system flow `samples.content-published` and a shipped draft
`samples.template`; the browser checks in `src/Crest.Workflows/tests/playwright/checks/` run on
them.

**The API gate** (`Security/ApiSecurityMiddleware`, on the engine API branch, after the
request path's gate): no gated caller → 403; without *View workflows* → 403; the GET form of
execute → 405; then the permissions the caller holds, asked of the one decision, are mapped
onto the engine's per-endpoint permission names (`EngineApiPolicies`, § The engine API) as a
transient claims identity for the request (claims
already on the principal are stripped first) (`EnginePermissions`: View → every `read:*` name;
Edit → `write:`/`delete:workflow-definitions`, refresh/reload, activity tests; Publish →
`publish:`/`retract:workflow-definitions`; Run → `exec:`, `trigger:event`, `tasks:complete`,
`write:`/`cancel:`/`delete:workflow-instances`). The gate evaluates the matched endpoint's own
policy itself, so a missing permission is a 403 with a reason, never a login redirect.
Antiforgery on a write is the registry group's endpoint filter, as for every API. Starting
a definition by hand (`execute`, `dispatch`, `bulk-dispatch`) is authorized against that
definition's Run list; instance operations go through `EngineInstanceAccess` (below). A
`WorkflowChangeDeniedException` thrown deeper down becomes a 403 with its message. Nothing is
cached in the cookie; a role change applies on the next request, and the member ceiling
applies.

**Ownership tiers** (`WorkflowFlowDescriptor.Ownership` + `Version`; custom properties
`Crest.Ownership`, `Crest.FlowVersion`, `Crest.Forked` next to `Crest.FlowKey`;
enforced by the store, not the UI):

| Tier | Who changes it | Upgrades |
| --- | --- | --- |
| `system` | Code only. No user, the super user included, can save, publish, retract or delete it; stored `IsReadonly`, so the engine's endpoints refuse first and Studio opens it read-only. Always published: the importer re-publishes one found unpublished. | A newer `Version` in the descriptor is imported as a new version of the same definition at the next activation. Running instances finish on theirs. |
| `shipped` | Users with *Manage shipped workflows*. The first user save (or version revert) marks `Crest.Forked`. Never deleted. | Unforked copies upgrade like system ones (published if the copy was). A forked copy is left alone; the registry reports `outdated`; `POST api/crest/workflows/registry/flows/{key}/reset` re-imports the shipped JSON as a new version, fork mark cleared, the tenant's edits in version history. |
| `tenant` | Whoever holds Edit/Publish and passes the flow's access lists. | None. |

`WorkflowOwnershipGuard` (`Server/Registry`) decides on the *stored* definition's
properties, never the incoming model's, stamps the stored ownership back onto what is saved
and marks the fork; the importer's writes run inside `WorkflowSystemScope` (AsyncLocal) and
are exempt. It is called from the publisher (GetDraft, SaveDraft, Publish, Retract, Revert)
and the store's Delete. GetDraft is guarded because the content system's `DraftRequired` read
creates and saves a new draft version; a refusal after it would leave a stray draft as the
latest version. It decides for the gated caller and refuses a change with none.

**Per-flow access: three lists.** Custom properties `Crest.Access.Edit`, `Crest.Access.Run`
and `Crest.Access.View` list role names and user names (a user name is the per-user override);
`GET/PUT api/crest/workflows/definitions/{id}/access` reads and sets them
(`WorkflowDefinitionAccessModel`). `WorkflowDefinitionAccessCeiling` is a ceiling inside the
one decision: it vetoes Edit, Manage shipped and Publish when the Edit list is non-empty and
names neither the caller nor one of their roles, Run against the Run list, and instance reads
against the View list; it acts only when the resource is a `WorkflowDefinitionAccessResource`,
so `AuthorizeAsync(user, permission, resource)` and a direct `DecideAsync` give the same answer
everywhere (guard, gate, linker, access API). The super user and the system are never
narrowed. Edit and Run do not imply View. A definition with no View list is tenant-wide: every
`ViewCrestWorkflows` holder reads its instances even when they can neither edit nor run it.

`EngineInstanceAccess` (`Server/Security`), called by the API gate, decides every engine
instance operation with the definition(s) it reaches as the resource: get, journal (the posted
filter too), variables, execution state, export and the activity-execution reads take View;
cancel, delete, variables writes, the bulk cancel and delete, a bookmark resume (the instance
its token names), a task completion (the bookmarks the task's stimulus hashes to) and an event
trigger (the triggers and bookmarks the event hashes to, or the instance named) take Run; a bulk
action that names nothing takes Run on every definition. The instance list is narrowed in the
request itself (the definition filter intersected with, or set to, the definitions whose View
list admits the caller; a filter left empty is refused), so the engine never sees the question.
The instance import is not narrowed (its instances come in files; the dispatcher stamps the
actor).

**Studio follows the same decisions.** `WorkflowDefinitionLinker` replaces the engine's static
linker: a definition's write links (`publish`, `retract`, `delete`, `import`,
`update-references`) are present only for what the acting user may do to that definition, and
Studio's read-only decision is "no `publish` link". Until the UI transition, Studio ties
editing to the `publish` link, so Edit without Publish reads as read-only.

### Running identity

**A run acts as the caller who started it.** The acting user at trigger time is snapshotted as
**identity only** (`WorkflowUserContext`: user id, user name, tenant, authentication type;
workflow input `Actor`, not `User`, which stock Users events use); `WorkflowCallerResolver`
builds the caller from it again at the start of every burst through `Crest.Access`, so a right
lost between bursts fails the next one, and `IWorkflowAuthorizer`, `RequiredPermission` on the
Crest and content triggers, `Require permission` and `Validate user` ask the one
`IAccessDecision` for the burst's caller, never the input.

- **Run as system.** A definition published with `Crest.RunsAsSystem` (a property that takes
  *Manage shipped workflows* to set, enforced by `WorkflowOwnershipGuard`) runs as the tenant
  system actor instead. Timers, cron runs, the bookmark queue and background jobs resolve the
  same way; a flow with neither an actor nor the system property is refused by the gate.
- **HTTP endpoints.** An HTTP-endpoint workflow runs as the request's caller
  (`IHttpWorkflowInputContributor`); `HttpWorkflowEndpointAuthorizationHandler` asks the
  decision for Run plus the Run list.
- **The actor is sealed.** The `Actor` input is never read as the client wrote it: every
  capture site (trigger publisher, content events, hooks, HTTP endpoints, webhooks) writes it
  through `WorkflowActorSeal` (tenant data protection), fresh for a day; the gate binds it to
  the instance on the run's first burst, and every later burst opens only the actor bound to
  its own instance. A dispatch (`ActorStampingWorkflowDispatcher`, wrapping the engine's
  dispatcher) stamps the dispatching caller's actor on a new run and strips any actor from a
  resume. So an actor supplied through the API's dispatch, an author's `DispatchWorkflow`
  input, an instance import or a resume's input is refused, and a sealed actor copied from one
  run opens for no other.
- **Per burst.** A run's **first** burst inherits a caller already set for the scope (a
  request, a hook child's parent), **except the system caller**, which only a definition
  published as system may run as (a flow triggered by a background task or by a system flow
  does not become the tenant super user); every **later** burst runs as the run's bound actor,
  never as whoever resumed it (an approver, an HTTP resume).
- **Anonymous actors.** Webhooks run as the connection's **application caller**
  (`WorkflowConnectionCaller`, name identifier `connection:{key}`): a connection carries
  `Roles` (`WorkflowConnection.Roles`), read on every build by
  `WorkflowConnectionCallerContributor`, so a later burst holds what the connection holds then;
  adding a role to a connection takes `AssignRoleToUsers` for that role; a role change or a
  deleted connection with roles bumps the permission version; the signature check stays its
  authentication. Login and registration triggers (`user.logged-in`, `user.created`,
  `user.confirmed`) act as the user the event names when the request is anonymous
  (`IWorkflowTriggerPublisher.PublishForUserAsync`; an administrator creating a user stays the
  actor). Anonymous runs are opt-in per definition (`Crest.AcceptsAnonymousRuns`,
  `WorkflowsConstants.AcceptsAnonymousRunsProperty`, mirrored on `WorkflowDefinitionPart`,
  publishing it takes `ManageShippedWorkflows`) and only for runs starting at an `HttpEndpoint`
  trigger (a public page, and the public endpoints that serve specific public media and
  content); a backend API endpoint never accepts an anonymous run. A privileged step in an
  anonymous flow means publishing it as system. `WorkflowAccessGateMiddleware` refuses every
  other anonymous burst with the reason (`WorkflowCallerResolver.AnonymousRefusal`).

The security token (`ICrestAntiforgery`, the API behind the cookie) stays until the designer
moves onto Crest components. Machine identities (client credentials, API keys) are
[machine-actors.md](machine-actors.md).

## Registry

A core registry, shaped like Parties: providers contribute descriptors, the catalog merges them
by key (first registration wins, ordered by position then key), the API and pages read the
catalog. The catalog merges lazily: content handlers raise triggers through the publisher,
which takes the catalog. (The catalog is to fold into the engine's registry,
[One registry](#design-decisions).)

- **Triggers**: `WorkflowTriggerDescriptor(Key, DisplayName, Object, Description, Position)`
  from `IWorkflowTriggerProvider`; raised through `IWorkflowTriggerPublisher.PublishAsync`,
  which queues the stimulus to fire after the current unit commits (refuses unregistered
  keys so a typo fails loudly). Input to the flow: `TriggerKey`, `Payload` (ids and scalars
  only), `Actor`, `StimulusId`; correlation id = the object's id. The `Registry trigger`
  activity (`RegistryTrigger`, ports Done/Denied/Skipped) subscribes by key, with an optional
  `RequiredPermission` and an optional **payload filter** (one `Key = value` per line,
  case-insensitive equality; a miss ends on Skipped).
- **Activities**: `WorkflowActivityDescriptor(Key, DisplayName, Object, ActivityType,
  Description, Position, Category, IsTrigger, Inputs)` from `IWorkflowActivityProvider`;
  the activity class lives in the owning module and is registered with the engine from that
  module's startup (`AddActivitiesFrom`); `Inputs` presets a generic activity for a purpose.
- **Flows**: `WorkflowFlowDescriptor(Key, DisplayName, Object, DefinitionJson, Position,
  Publish, Ownership, Version)`: the engine's export JSON, embedded under the module's
  `Data/workflows`, imported per tenant by `WorkflowFlowImporter.SyncAsync` right after the
  activity registry is populated (the JSON names activity types). `Publish: false` ships a
  flow as a draft the tenant opts into (anything that changes business data on its own).
  `WorkflowFlowLookup` finds a tenant's copy by `Crest.FlowKey`.
- **Connectors**: `WorkflowConnectorDescriptor(Key, DisplayName, AuthKind, BaseUrl,
  Description, Position)` from `IWorkflowConnectorProvider`; see [Connectors](#connectors).
- **Extension points.** `Raise trigger` (`RaiseTrigger`, Done/Failed) publishes a registered
  key after this flow's unit commits, with its correlation id and payload by default;
  Crest.Workflows registers the generic `flow.raised`. `Hook` runs a slot's attachments
  inside the unit ([Units of work](#units-of-work-how-a-run-commits));
  `WorkflowHookSlotDescriptor` and `WorkflowHookAttachmentDescriptor` are the registry's slot
  and system-attachment contracts, listed by the registry API and `api/crest/workflows/hooks`.
- **API:** `GET api/crest/workflows/registry` (triggers, activities, flows with definition
  id, published, ownership, shipped/installed version, forked, outdated; connectors);
  `POST .../registry/flows/{key}/reset`; `POST api/crest/workflows/registry/sync`
  (ManageShipped) re-runs the activation sync and answers what it did per flow (Install,
  Republish, Upgrade, Keep; the decision is `WorkflowFlowImporter.Decide`, unit-tested branch by
  branch); `GET api/crest/workflows/triggers[?definitionId=]` and
  `POST .../triggers/{definitionId}/reindex` (diagnostics over the stored triggers).

Gotchas: a trigger node indexes only with `customProperties.canStartWorkflow = true`
(shipped JSON and API clients must set it); the engine's save endpoint needs
`variables/inputs/outputs/outcomes` as empty collections; body-less requests must not carry
`Content-Type: application/json`; a flowchart connection from a trigger is port `Done` →
`In`; list inputs accept a plain JSON array.

## How a flow starts

A flow starts from a **trigger** node, or by hand, or as a **hook attachment**, or as a
**composable activity** inside another flow.

- **Registry trigger** (`RegistryTrigger`): a registered trigger key, an optional payload
  filter (`Kind = invoice`, one `Key = value` per line, all must match → otherwise the
  `Skipped` port), an optional required permission for the acting user (otherwise `Denied`).
  The payload and the acting user arrive as workflow input (`Payload`, `Actor`,
  `TriggerKey`, `StimulusId`).
- **Platform triggers**: the Users module raises `user.created`, `.updated`, `.deleted`,
  `.enabled`, `.disabled`, `.confirmed`, `.logged-in` and `.logged-out` (payload UserId,
  UserName, Email, Roles as a comma list, Provider on login; correlation = user id) from its
  event handlers and both login paths. Content events are the Contents feature's triggers
  below. Platform tasks are engine activities in their own modules (§ Platform activities).
- **Content triggers** (`Crest.Workflows.Contents`): created, published, updated,
  deleted, ... with a content-type filter.
- **Timers and cron**, **webhooks** (`Webhook received`: HMAC-signed posts to
  `api/crest/workflows/webhooks/{connection}/{hook}`), **approvals** (`Request approval`
  parks the flow until a role member or permission holder decides at
  `api/crest/workflows/approvals`).
- **HTTP endpoints** (the engine's `HttpEndpoint` trigger, [above](#workflows-builds-on-top-of-the-module-apis)).
- **Raise trigger**: a flow raises a registered key for other flows (`flow.raised` is the
  generic one). Fire-and-forget, after commit.
- **Hook**: a slot's attachments run *inside* the current flow (next section).

A trigger is raised **after the raising unit commits**, never inside its transaction
(`WorkflowStimulusQueue` → `WorkflowAfterCommit`): the flows it starts run in child shell
scopes of their own, one per stimulus, under the object's lock when the stimulus is
correlated to an object. Nothing is raised for a unit that failed. Every stimulus carries a
`StimulusId`, the idempotency key a consumer that must act once per event keys on.

### Platform activities

The activities the platform modules contribute, each an engine activity in its own module
with `Input<T>` properties, a `Failure` output and Done/Failed ports, registered through the
registry so the palette groups them by object. Those with an effect outside the database
carry `IUnitBoundary` and `RunAsynchronously`: the unit commits first and they run after it
in a unit of their own ([Units of work](#units-of-work-how-a-run-commits)).

| Module | Activities | After commit |
| --- | --- | --- |
| Email | `Send email` (to, cc, bcc, from, sender, reply-to, subject, text and HTML bodies, body format) | yes |
| Sms | `Send SMS` | yes |
| Notifications | `Notify users` (comma-separated user names), `Notify content owner` (item id, default the payload's ContentItemId) | yes |
| Twitter | `Update X status` | yes |
| Facebook (Pixel) | `Send Meta conversion event` | yes |
| Users | `Create user` (e-mail, user name, moderation, optional confirmation e-mail with the link as the ConfirmationUrl output), `Validate user` (Anonymous / Authenticated / InRole on the burst's caller), `Assign user role`, `Unassign user role` (refreshes the security stamp), `Get users by role` (user id → name) | no |
| Tenants | `Create tenant`, `Setup tenant`, `Enable tenant`, `Disable tenant` (default tenant only; unit boundaries) | no |
| Crest.Workflows | `Log` (level, message) | no |

The content, user, tenant and party activities ask the decision against the item, role or user
(`IAccessDecision.RequireAsync`/`AllowsAsync`); a read the caller may not make is "not found".
HTTP requests are the engine's own activities (`Send HTTP request`, `Download HTTP file`),
which use the connectors' guarded handler (`ConnectorNetworkPolicy.CreateHandler`), so every
connection, redirects included, reaches only public addresses unless the shell's
`AllowPrivateNetworks` opens it. Form posting is the engine's HTTP workflows. Publishing a
`WorkflowDefinition` item never fans out to content triggers.

`Crest.Workflows.Contents` (feature) adds the engine-native content triggers (created,
published, updated, deleted, versioned, draft saved, unpublished) with a content-type filter
and the acting user, and content tasks (create/get/update/publish/unpublish/delete).

## Units of work: how a run commits

The posting activities and anything composed of several steps must be ACID: an invoice posts
or does not, and what the tenant attached to its creation (copying the customer's route and
terms) is in the same unit; the invoice with those fields exists, or nothing does.

- **A unit is a burst.** A run is a sequence of bursts: the engine executes synchronously until
  a wait (a bookmark), persists, and resumes later. A burst is one shell scope, one YesSql
  session, committed as one transaction when the scope ends. The engine's stores flush
  (`FlushAsync`) but never commit mid-run. A burst is bounded by compute time: nothing inside a
  unit waits or calls out. Every entry point (scheduled tasks, the background channels, the
  bookmark queue, background jobs) runs in a shell scope of its own, so every entry point is a
  unit.
- **A failed unit is cancelled.** A burst that a required hook or atomic step fails is
  cancelled before the host would commit it: nothing the burst wrote stands, the faulted
  instance is recorded in a fresh scope so the journal shows what happened, and an API request
  that ran the hook answers **409** with the slot, the attachment and the reason.
- **External calls are boundaries, like waits.** `Call connector`, `Poll connector` and the
  external platform activities (`Send email`, `Send SMS`, `Notify users`, ...: `IUnitBoundary`)
  are the engine's own **background activities** (`RunAsynchronously`): the engine's middleware
  bookmarks the node and hands it to the scheduler; `DurableBackgroundActivityScheduler` writes
  the job (`WorkflowBackgroundJob`, indexed, idempotency key instance + node + bookmark) in the
  unit, so it commits with the bookmark or not at all, and runs it after commit in a child shell
  scope of its own with the connection's auth, resilience strategy and rate limit;
  `ShellScopedBackgroundActivityInvoker` resumes the flow by bookmark in another (the next
  unit), and the engine captures outputs, outcomes and journal. Connectors send the job's key as
  `Idempotency-Key`. The author writes one linear flow and one plain `ExecuteAsync`; a job whose
  unit fails is never run; jobs a dead process left are run again when the tenant activates
  (`RecoverAsync`). `Raise trigger` is *not* a boundary: the queue sends after commit and the
  flow needs no answer, so a hook attachment may raise one, the honest "call after commit"
  shape.
- **Atomicity is derived, never asserted.** Every activity, composites included, is *atomic*
  (no boundary inside, nested definitions walked) or *long-running*, computed at publish by
  `WorkflowAtomicityAnalyzer`; a published composable flow re-validates what uses it.
  Boundaries: the engine's bookmark-creating activities (events, timers, delays, HTTP
  endpoints), connector activities, `Request approval`, the external platform activities, the
  tenant activities, `Download HTTP file`.
- **Two kinds of extension point.** **Hooks** run the attached flows *inline, inside the unit*,
  as child instances in the same scope and session. A slot (a module's own slot, such as a
  document's created or posting slot, or the generic `flow.hook`) is run either by a flow's
  `Hook` activity or by a registry service from inside its own request through
  `IWorkflowHookRunner`, so the attachment runs whichever path created the object. A
  **required** attachment that faults or suspends fails the unit; a **best-effort** one's
  failure is journaled. Hooks accept only atomic attachments (refused at attach and at publish,
  with the boundary named and the event alternative suggested). An activity's Failed port inside
  an attachment is the activity's answer, not the unit's: the attachment says "the host must not
  go through" with `Fail unit`. **Triggers** (`Raise trigger`, the registry triggers) fire after
  commit, and their consumers run as their own units: for notifications, integrations, anything
  where a delay does not matter.
- **Hook slots and attachments are locked like flows.** A slot is declared by the owning module
  (`WorkflowHookSlotDescriptor`: key, object, whether tenants may attach, whether tenant
  attachments must be required, where they run). An attachment (slot, flow, position,
  required|best-effort) carries an ownership tier: system attachments ship with the module and
  are upgraded by code; tenant attachments are the tenant's within the slot's policy, behind the
  flow permissions and the attached flow's access lists.
- **Per-object serialization.** `IWorkflowObjectLock` (Abstractions; over the engine's
  `IDistributedLockProvider`, keyed by the object's id, re-entrant within a unit) is taken by the
  owning registry service around every write to an object and by the after-commit send around
  each correlated run: no two units touching one object interleave. Version checks reduce to
  "read under the lock". What is still pending for an object (flows about it that have not
  finished and the jobs they wait on) is `GET api/crest/workflows/pending?correlationId=`.
- **What stays impossible, on purpose:** a hook that must call out *and* be in the transaction
  (a distributed transaction with someone else's API). The validator says so and names the two
  honest shapes: call after commit with compensation (`Void` answers `Post`), or call before
  the unit as a precondition.

**As built.** `WorkflowUnitOfWork` (scoped; nested frames for hook attachments; a failed unit
cancels its own session through a before-dispose callback registered ahead of the host's
commit, so a service's request discards the same way a burst does) and
`UnitOfWorkCommitStateHandler` (wraps the engine's commit handler: a failed unit cancels the
document store and records the faulted state through a child scope). `WorkflowAfterCommit`
(Server/Units) is the one after-commit hook the stimulus queue and the background jobs share;
anything that must be after-commit uses it, never `WorkflowStateCommitted` (sent **before** the
YesSql commit). `Hook` and `Fail unit` activities, `WorkflowHookService` (system attachments
from `IWorkflowHookAttachmentProvider`, tenant ones in a document), `WorkflowHookRunner`,
`WorkflowHookFailedException` → 409 through `WorkflowHookFailedExceptionMapper` (the API
registry's error contract) for any module's API, `api/crest/workflows/hooks`, `WorkflowAtomicityAnalyzer` (used at attach and,
through the engine's validating notification, at publish). The trigger store treats a
re-indexed trigger as an update (delete-then-insert of one document in a session is refused by
YesSql). `BackgroundJobContext` (the running job, for the idempotency key). The two action
pipelines are named in `Server/Units/ActionPipelines.cs`. Checks `workflows-units`,
`workflows-fields`, `workflows-connectors`.

Known limits: the approval decision resumes its flow inline in the deciding request and an
incoming webhook runs its flows in the request (both are their own boundaries, so no unit is
split, but neither is after-commit). Child scopes never activate the shell
(`activateShell: false`): during setup that path would wait on the activation lock the
setting-up scope holds.

## Field dependencies

**Contracts are per-activity field dependencies, not a record-level contract.** An activity
declares each content field it reads or writes as a dependency on a field path (`Part.Field`)
with a **required marker per field** and a read/write marker (`Server/Fields`, contracts in
Abstractions): system activities declare theirs in code with
`[FieldDependency(path, Required, Writes, ContentType)]`; configurable activities derive theirs
from their bindings through `IFieldDependencySource` (`Copy fields`' mapping rows: each source
with its own marker, each target as a write). The engine's input model is untouched: the
dependency is a declaration beside the inputs, read from literal bindings at publish.

**Publish warns, runtime fails, and only for required fields.**
- At publish, `WorkflowFieldDependencyAnalyzer` (nested published definitions walked)
  resolves each dependency against the tenant's content definitions as they stand and
  `FieldDependencyPublishHandler` stamps the **warnings** on the version
  (`Crest.FieldWarnings` custom property; absent when clean), never a refusal: the part
  or field does not exist, or the activity requires a field the tenant's definition leaves
  optional or shows only under a Crest visibility condition. Written fields only have to
  exist. `GET api/crest/workflows/definitions/{id}/field-dependencies` resolves the latest
  version on demand (the "what this flow needs" panel). A tenant who wants a run to stop on
  a condition adds the condition node; the warning is the reminder.
- At runtime, a missing value for a **required** dependency completes the activity on
  `Failed` with the field named (`IWorkflowFieldDependencyChecker`, which a module's
  activities call before asking the service; `Copy fields` does the same per row); an
  **optional** dependency reads null and the activity accounts for it ("use it if you have
  it"; tax is the standing example, it may not be enabled).
- Declared dependencies ride the engine's activity descriptors
  (`crest:fieldDependencies`, via an `IActivityDescriptorModifier`) and the registry's
  activity entries (`FieldDependencies`), so the palette and the panel can show them.

**System activities name their fields, and those fields are locked.** Every system activity
is specific about the fields it depends on (id, created date, the transaction part's number,
status, party id, subtotal, total, ...); those are marked required and **locked in the
shipped content type definition** so tenants cannot make them optional or hidden. The lock
mechanism lives in Crest: the field/part-definition counterpart of Crest's option-list locks
(None|Tenant|Module, module locks unliftable in-tenant), tracked in
[content-items.md](content-items.md).

**Activities compose.** Sub-actions such as "write a journal entry" are activities of their
own, chained and nested like functions: `UsableAsActivity` makes a published definition a
palette entry (`WorkflowDefinitionActivity`, a `Composite`), so a posting flow is built from
smaller system flows; the C# `Composite` base stays for pieces that belong in code. The
ownership tiers apply to the pieces. Posting is workflows.

## Activities Crest adds

| Activity | What it does |
| --- | --- |
| `Registry trigger`, `Raise trigger` | start from / raise a registered trigger |
| `Hook`, `Fail unit` | run a slot's attachments inside the unit; fail the unit |
| `Require permission` | gate on a permission of the burst's caller |
| `Request approval` | park until a role member or permission holder decides |
| `Call connector`, `Poll connector` | call a tenant connection after commit; poll for changes |
| `Run query` | run a registered query through the query pipeline ([Tasks](#3-the-registry)) |
| Content operations | create, update, delete, publish, unpublish per type ([Tasks](#3-the-registry)) |
| `Log` | write to the tenant's log |
| `Send email`, `Send SMS`, `Notify users`, `Notify content owner`, `Update X status`, `Send Meta conversion event`, the user and tenant activities | the platform modules' activities (§ Platform activities) |
| `Copy fields`, `Move fields` | copy field values between content items by typed mapping rows |
| `Create party role`, `Resolve party` | Parties |

Triggers, activities and shipped flows by Crest module:

| Module | Triggers raised | Activities | Shipped flows |
| --- | --- | --- | --- |
| Users | `user.created`, `.updated`, `.deleted`, `.enabled`, `.disabled`, `.confirmed`, `.logged-in`, `.logged-out` (event handlers and both login paths; correlation = user id) | `CreateUser`, `ValidateUser`, `AssignUserRole`, `UnassignUserRole`, `GetUsersByRole` | — |
| Email, Sms, Notifications, Twitter, Facebook, Tenants | — | `SendEmail`; `SendSms`; `NotifyUsers`, `NotifyContentOwner`; `UpdateTwitterStatus`; `SendMetaConversionEvent`; `CreateTenant`, `SetupTenant`, `EnableTenant`, `DisableTenant` | — |
| Parties | `party.role-created`, `.role-removed` (a content handler on every registered party type except the two base types; correlation = role id) | `CreatePartyRole` (registry key → role type, party from the payload, through `IPartyRoleService`, idempotent); `ResolveParty` (a role or base party id → RoleId, RoleContentType, RoleTypeKey, BasePartyId, BasePartyContentType: which item holds a field, nothing walks the graph implicitly) | — |
| Crest.Workflows | `flow.raised`; hook slot `flow.hook` | `Registry trigger`, `Raise trigger`, `Hook`, `Fail unit`, `Require permission`, `Request approval`, connector activities, `Log`, `Copy fields` / `Move fields` (Server/Contents: source and target item ids, the target defaulting to the payload's ContentItemId or TransactionId, and a JSON mapping of `Part.Field → Part.Field` rows each with a `required` marker; `ContentFieldValueCopier` resolves both sides against the tenant's current definitions, copies a same-type pair's JSON whole, converts text ↔ numeric, refuses other pairs; a required source that is empty or missing ends on Failed with the field named, an optional one is skipped and listed in `Skipped`; Move clears the source; the write goes through the content operations' update path) | — |

**Approvals** (`Server/Approvals`): `Request approval` records an `ApprovalTask` (YesSql,
indexed) for a role and/or permission and waits on a bookmark;
`api/crest/workflows/approvals` lists what the signed-in user may decide,
`{id}/decide` (approve|reject, comment) authorizes through role membership or the decision for
the permission (member ceilings apply), records the decision and resumes the flow on Approved
or Rejected with DecidedBy/Comment as outputs, as the run's own actor; 403 outside the role,
409 once decided. Crest page `/workflows/approvals`. The shared approvals system grows out of
this ([permissions.md](permissions.md) › Approvals).

## Connectors

The connection model, sealed secrets and tokens, OAuth flow, HTTP invoker, token cache, rate
limiting and retries belong to the connection system: the protocol modules (`Crest.Http`,
`Crest.Rest`, `Crest.Soap`, `Crest.GraphQL`, ☐ to be created). Workflows keeps the activities
and inbound webhook triggers as consumers of their connections; the one Connections screen and
the `ManageConnections` permission are the connection system's, not Workflows' or Queries'
([queries.md › Connections](queries.md#connections-the-protocol-modules-and-the-data-layer)).
What follows is what is built today in `Server/Connectors`, until it moves (☐).

A **connection** is a tenant's handle on an external service: base URL, auth kind (`none`,
`api-key`, `basic`, `bearer`, `oauth2-client-credentials`, `oauth2-authorization-code`,
`hmac` for inbound webhooks), a sealed secret that never leaves the server, retry count,
rate limit, timeout and roles (the webhook caller's, [Running identity](#running-identity)).
The registry ships the generic `http` and `webhook` connectors; a module ships a specific one
for a service it knows. Tenant connections live in one document (`WorkflowConnection`), secrets
sealed with the tenant's data protection (purpose `Crest.Workflows.Connections`) and never
returned (`WorkflowConnectorAuthKinds`, settings in `WorkflowConnectionSettingKeys`). API
`api/crest/workflows/connections` (Manage connections, antiforgery); Crest page
`/workflows/connections`.

Activities: `Call connector` (Done/Failed; outputs status, body, parsed JSON, failure),
`Poll connector` (Changed/Unchanged/Failed; a hash per definition + node in the tenant
database, driven by the engine's Timer or Cron), `Webhook received`
(`api/crest/workflows/webhooks/{connection}/{hook}`, HMAC-SHA256 of the body required, 401
otherwise, 404 for unknown/non-hmac connections; payload Body/Query/ContentType; runs as the
connection's application caller).

Policy (`ConnectorHttpClient`, `ConnectorInvoker`, `ConnectorRateLimiters`,
`ConnectorTokenCache`): `ConnectorInvoker` makes one attempt and marks transient failures
(network/timeout/408/429/5xx); retries are the engine's resilience feature with the
connection's `RetryCount` as the policy (`ConnectionResilienceStrategy`, category Connectors,
default on connector nodes, attempts journaled). Per-connection fixed-window rate limit without
queue, response and webhook size caps, no automatic redirects, paths cannot leave the base URL,
and every connection is refused on loopback/private/link-local/CGNAT addresses at connect time
(after DNS) unless the shell sets `CrestWorkflows:Connectors:AllowPrivateNetworks` (a host's dev
setup does, for the local test endpoint).

**OpenAPI import** (`POST connections/import-openapi`): the first server becomes the base
URL, the global security scheme the auth kind (http bearer/basic, apiKey header, oauth2
clientCredentials or authorizationCode with their URLs and scopes), every path operation an
entry stored on the connection (`settings.operations`) and shown by the registry as a
palette preset on `Call connector` (`connector.{connection}.{operationId}`: connection, method
and path preset; the flow binds the body). **OAuth2 authorization code**
(`oauth2-authorization-code`): `GET connections/{key}/oauth/authorize` redirects to the
provider with a sealed state (tenant, connection, user, issued-at), `oauth/callback`
exchanges the code and seals the token set on the connection; the invoker uses the access
token and refreshes it with the refresh token when expired; the API only says
`authorized` and when the token expires.

## Designer and admin pages

**Admin surface.** A "Workflows" menu section (`WorkflowsPages`, a page registry source) with
Definitions, Instances, Connections, Approvals, Hooks; each page declares its route permission
and menu place with `[PageEntry]` ([admin-menu.md](admin-menu.md) › The page registry), and the
Studio pages are marked browser-only (`ClientOnly`). Connections, Approvals and Hooks are Crest pages;
Definitions/Instances (and the editor and instance viewer, on Studio's own routes
`/workflows/definitions[/{id}/edit]`, `/workflows/instances[/{id}/view]`) are the forked
Studio.

**Hooks page** (`blazor-wasm/Pages/WorkflowHooks.razor` at `/workflows/hooks`, menu
Workflows › Hooks, View workflows): every slot with its module and tenant attachments, attach
(published flows the user may edit, from `GET api/crest/workflows/hooks/candidates`) and detach
for users with Edit workflows; the API refuses what is not atomic.

**Studio loads on demand, in its own container.** Nothing of Studio is in the admin client's
startup download or its service container. `Crest.Workflows.BlazorWasm` (always loaded,
thin) has the pages, `StudioHost` (loads the lazy assemblies with `LazyAssemblyLoader`,
builds Studio's container through `Crest.Workflows.Studio.Host.StudioServices.BuildAsync`
by reflection, attaches it as a late container and renders the view) and
`StudioActivityNode`, the canvas node's always-registered stand-in
(`[CrestJSComponent]`). `Crest.Workflows.Studio.Host` (lazy) holds the container setup
(Studio core, shell/MudBlazor, workflows module, the engine backend at
`{tenant}/crest-workflows/api` behind `CookieApiHandler` with the antiforgery token, a cookie
auth provider manager) and the views with MudBlazor's providers. The lazy set (26 assemblies:
Studio, MudBlazor + extensions, Monaco, Refit, Polly, FluentValidation, Humanizer, Radzen, the
engine API client) is listed in `crest-lazy-assemblies.txt`; the host app's WASM build turns
every module's `blazor-wasm` library into lazy pages (`tools/Crest.LazyModules`, imported by
the host's WASM entry project). Measured on a trimmed Release publish (brotli): startup ≈ 5.2
MB, on demand 2.6 MB. **Browser-only** because under InteractiveAuto a first visit runs in a
server circuit where Studio's HTTP clients would share scopes across users; the host prerenders
a placeholder and reloads into WASM if an in-app link reached it inside a circuit.

The move of the designer onto Crest components is under [Still to build › Designer](#designer-the-transition-onto-crest-components).

**Run history and insight go through the audit system** (`Server/Audit`): each burst of a run
with its journal and incidents, hook attachment runs and inbound webhook deliveries (rejected
posts included) are audit events (category `Workflows`); failures are written so a failed unit
keeps them ([audit.md › Implementation](audit.md#implementation)). The insight sections and
metrics are audit feeds.

## Testing

Playwright checks under `tests/playwright/checks`, registered in the host's aggregated
suite (the harness is Crest's); each cleans up what it creates and leaves shipped flows as
shipped. The checks Crest.Workflows carries:

| Check | Covers |
| --- | --- |
| `workflows-api` | engine API round-trip (save, publish, execute, journal), antiforgery 400, limited role 403, anonymous 401 |
| `workflows-units` | a failed required hook attachment faults the host and discards its writes; a healthy one commits with child instances; best-effort failure journaled; long-running flows refused at attach and at republish; a trigger raised in a failed unit never fires |
| `workflows-approvals` | request, queue, decide, 403/409; the pending API shows the parked flow for its object |
| `workflows-designer` | the real UI: open, add node, connect, save, publish, run, journal; lazy download assertion; Connections and Approvals pages |

A consuming host's own checks cover the registry, ownership, connectors, field activities
and tenant isolation end to end through its modules.

Unit tests (`tests/Crest.Workflows.Tests`): the API gate branch by branch (grant mapping,
per-definition Run, 403 on refusal, `ApiGateResponseTests` against the real authorization
middleware), ownership property round-trips and stamping, the access handler's
veto/admit/bypass rules, the system scope, the registry catalog and publisher, the user
snapshot, `WorkflowCallerLifetimeTests`, `ActorStampingDispatcherTests`, connector address
policy, path containment and signatures, the async-only disposable scan, the shipped-flow sync
decision.

**Known flake:** on a freshly provisioned tenant, a check running early in a filtered run
occasionally hits Playwright's "Execution context was destroyed" (the admin page navigated
once under an evaluate). Not reproduced idle or in the full suite; the check most exposed
settles and retries once. Suspect a circuit drop and reload in Crest.
Also: edits under the watch dev server during a test run rebuild shared WASM
assets and break the test server's integrity checks (every login stays disabled); stop the
watch server or do not edit during a run. A build that fails on
`ActivityWrapper.razor` with RZ1021/RZ9981 (unchanged file) is a stale Razor build server:
`dotnet build-server shutdown`, then build again.

## Where things are

- Module: `Crest.Workflows`: `Abstractions` (contracts), `Server` (the module: `Registry`,
  `Security`, `Units`, `Hooks`, `Fields`, `Contents`, `Connectors`, `Approvals`, `Audit`,
  `Queries`, `Stores`), `engine` (the vendored engine; its management API in
  `src/modules/Crest.Workflows.Api/Endpoints`), `designer` (the forked Studio), `tests`.
- The consuming module's part, e.g. an ERP module's `Server/Workflows` (provider,
  activities), `Server/Ledger`, `Server/Data/workflows/*.json` (shipped flows).
- Admin pages: Workflows › Definitions, Instances, Connections, Approvals, Hooks, under the
  Crest admin; the designer opens from a definition.

## Operations: one registry, one request path, four pipelines, one access machinery

The backbone of the application: how every read and write in Crest, from a UI button, a
picker, a content list, a Liquid slot, a workflow step, a REST, GraphQL or RPC call, a webhook
or an external connector, goes through one system. The designs it builds on are
[queries.md](queries.md) (the query pipeline, and the connection system it and the action
pipelines consume), the sections above
(the action pipelines: units of work, queues, hooks), [access.md](access.md) (the access
machinery), [parties.md › Members](parties.md#members-persons-who-act-for-an-organization) and
[shells-and-themes.md › The member portal](shells-and-themes.md#the-member-portal-login-sessions-sides)
(the caller's side and organization) and [blazor-display.md](blazor-display.md) (what binds to
operations). Status: steps 1, 2 and 4 are built in their first form, 3 in part, 5–8 are open
([Tasks](#tasks)).

Priorities, in order: speed, runtime adaptability, ease of maintenance.

### Principles

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
- **Modules declare, Crest.Server serves.** Every surface, declared or generated, is an entry
  in Crest.Server's API registry ([Workflows builds on top of the module APIs](#workflows-builds-on-top-of-the-module-apis)).
- **The caller is built per request on the server**, from the authenticated identity plus
  server-held state cached under the tenant's permission version; the client's session copy
  is for the UI only and never authority.
- **A read never writes; an action never serves a page; the workflow runtime never runs a
  synchronous read for a page.**
- **Workflows are the only code path for tenant logic**; there is no `Crest.Scripting`
  ([blazor-display.md](blazor-display.md) § 12).

**Why one system.** The platform has no data-access layer below the web layer
(`IContentManager`, `ISession` and `IQueryManager` do no authorization; each controller checks
for itself); each module defined and enforced its own permissions with no contract for "the
caller's scope over this table"; and scope was designed after most endpoints existed. The
result was nine distinct paths for reading or writing data, each with its own permission
handling ([The surfaces today](#the-surfaces-today-and-their-fate)). The fix is structural: one
place answers the question, and the surfaces are generated.

### The request path

```text
1. Tenant shell      the platform's tenant router: which tenant, which shell container,
                     the tenant prefix stripped into PathBase.
2. Theme shell       the shell selector (shells-and-themes.md › Shell dispatch): admin, site
                     or member bucket from the request, stamped; the shell base into
                     PathBase. A background entry point (timer, queue consumer, job) carries
                     its bucket and tenant explicitly and enters here too.
3. Authentication    the cookie or the Api scheme (bearer, machine-actors.md) → an identity,
                     or anonymous.
4. Caller context    the caller built for this request: side from the bucket (API calls
                     confirm it with X-Shell, the member side carries X-Org), organization,
                     roles, class ceiling, policy verdicts, from the server-side state cache.
                     Markers that disagree with the bucket: denied, never defaulted.
5. Decision          IAccessDecision for the operation (a page is an operation too: its page
                     registry entry's permission).
6. Scope             the ScopeSet for this caller, built once per request.
7. Dispatch          to one of the four pipelines (or, for a page, to the renderer, whose
                     slots and actions re-enter at step 5 as operations: decision and scope
                     run for each; the caller is built once per request).
8. Audit             the auditor records the decision and the execution.
```

Steps 2–6 run once each, in `Crest.Access`'s gate, called from the shell selector's startup
filter before routing ([access.md › The system](access.md#the-system)). The order, host then
tenant: `ModularTenantContainerMiddleware` (matches the tenant by host and prefix, opens the
shell scope the whole request runs in, commits or cancels it at the end) →
`ModularTenantRouterMiddleware` (tenant prefix into `PathBase`) → the tenant pipeline: the
`IStartupFilter`s (`ShellSelectionMiddleware` classifies admin, login and member paths and
calls `IAccessGate`, then runs the route permission check, stamps the bucket and shifts
`PathBase`) → `UseRouting()` → the module startups by `ConfigureOrder` (`AccessGateMiddleware`
at −150; the workflows engine API branch at −151, which maps the caller onto engine grants and
authenticates nothing; request localization at −100; endpoints, CORS and `UseAuthorization` in
the default group) → `UseEndpoints`. Background work enters through a synthetic `HttpContext`
flagged `IsBackground` and runs as the system caller its entry point sets.

#### Caller lifetime

The server-side state the caller is built from is cached per (user, tenant, side,
organization) with the tenant's **permission version**, bumped by every rights write and
compared on every use, so a decision is always against current rights.

| Lifetime | Caller built | Decision and scope |
| --- | --- | --- |
| HTTP request | once, at step 4 | per operation |
| Interactive circuit or websocket (Blazor Server, SignalR) | per operation (`ConnectionCallerHubFilter`, `ConnectionCallerCircuitHandler` in `Crest.Access/Services/ConnectionCallers.cs`), from the side and organization the connection was admitted on; the security stamp is re-validated once the stamp interval has passed; a changed stamp, a gone or disabled user or a refused caller aborts the hub connection or tears the circuit down. The shell selector admits `{shellBase}/_blazor` on the shell's side | per operation |
| Workflow burst | the sealed actor bound to the instance says **who**; the caller is built again at the start of every burst, so a right lost between bursts fails the next one; only the first burst may inherit a caller from its scope, and never the system caller unless the definition is published as system | per data activity |
| Background job | enters the path at step 1 as the actor it was written with; built then, never from the job payload | per operation |

Nothing serialized into a workflow instance, a job or a client carries rights; only identity.

#### The member caller

Two kinds of signed-in people share one tenant and one user store: **staff** (tenant users)
and **members** (organization-bound users, [parties.md › Members](parties.md#members-persons-who-act-for-an-organization)).
Same user management, same roles and permissions, same hierarchy; what differs is the
side they act on and the organization they act for, and that is the caller's business:

| | Staff side | Member side |
| --- | --- | --- |
| Managed by | tenant admins | tenant admins and the organization's member admin, subtree-scoped |
| Roles | the user's tenant groups and roles | the organization's groups at that organization's reach ([permissions.md](permissions.md)) |
| Data scope | tenant-wide per role and hierarchy | one organization, fail-closed, hierarchy within it |
| Signs in at | the admin login | the member portal only ([shells-and-themes.md](shells-and-themes.md#the-member-portal-login-sessions-sides)) |
| Tenant SSO | eligible | excluded |

**The class is a property on the user record, never a role**: a set derived from the user's
bindings (staff is a binding to the tenant organization, member a binding to any other;
`CrestMemberInfo` aspect, indexed by `UserConnectionIndex`, stamped in
`IUserEventHandler.CreatingAsync` so it persists in the same write; [parties.md](parties.md)
› Members). Roles are what admins grant and revoke daily; the class decides which login
surface accepts the account at all, so it must not be grantable: a mis-granted member role
would lock staff out, a removed one would let a member into the staff login. **Conversion**
is its own permission-gated action, never the role editor, bumping the security stamp so
sessions re-establish on the right surface; a member hired onto staff keeps their account,
credentials and external logins, and the event is audited.

**The class permission ceiling.** Some permissions are never valid on the member side,
tenant settings being the canonical example, even when a super user assigns a role that
carries them. The ceiling (`MemberClassCeiling`, a final denial in the one decision,
configured through `MemberPermissionCeilingOptions`) fails a ceilinged permission for a
member-side caller and beats every other success, closed over the dynamic expansions
(`Publish_{Type}` and the per-role user-management variants), with the admin role resolved
through `ISystemRoleProvider`, never hard-coded. **The ceiling keys on the side, never on the
user**: a dual user keeps every staff permission on the admin side and is ceilinged on the
member side. The role editor's effective-permissions preview goes through the same decision,
so ceilinged permissions grey out there; UI warnings are politeness, the ceiling is enforced at
decision time.

**Permissions are tenant-governed.** Every module contributes its permissions through
`IPermissionProvider`, member-portal abilities included, so they appear in the tenant's
ordinary role editor; organization scope and hierarchy expansion **narrow** what a granted
permission reaches (the scope providers), they never grant. Permission says "may manage
members"; scope says "of this organization's subtree". Organizations choose people, tenants
choose powers.

**What the contributor adds** (`MemberCallerContributor`): the organization (from `X-Org`,
else the session's last choice) validated against the bindings, the organization's groups and
roles only on the member side, the class, the impersonator, and the organization system
actor's member-admin roles. The staff roles and permissions a dual user holds never reach the
member side. Nothing is enriched into the cookie.

**Access policies**: a downstream module may affect a member's access without Members knowing
why. The caller builder consults registered access policies at portal sign-in, organization
switch and refresh, and a policy may refuse portal access for that organization or narrow the
member's permissions there; a subscription module uses it so an unpaid membership loses
access. Policy verdicts are part of the cached caller state and bump the permission version
when they change. Today a policy is a caller contributor (the conformance suite's
subscription fixture); a dedicated registered seam is still to build.

### The four pipelines

| | Synchronous (answers in the request) | Asynchronous (answers later) |
| --- | --- | --- |
| **Read** | **Query pipeline.** Resolve → compile the statement with the ScopeSet → execute through the connection → page after scoping with a stable order → typed rows → cache key `(operation, version, parameters, scope signature, culture)`. Non-SQL sources apply the ScopeSet before paging. A page render, a picker and an API `GET` use it. | **Read job.** The same compiled, scoped query as a durable background job: exports, large result sets, streaming, slow external sources. The caller gets a job id; the result arrives by bookmark, notification or download. Never used by a page render. |
| **Action** | **Unit of work.** One burst: one shell scope, one session, one transaction; inline hooks as child instances; a failed required attachment fails the unit and the request answers 409; per-object serialization by the object lock; atomicity derived, never asserted. A content operation, a short workflow and an API `POST` run here. | **Durable background.** The burst bookmarks and commits; the job is written in that unit, so it exists only if the unit committed; it runs afterwards in its own child scope with the connection's auth, resilience, rate limit and an idempotency key, and hands back by bookmark. The event side: stimuli after commit, partitioned by correlation id, at-least-once with idempotency keys. Connectors, approvals, timers, webhooks and long-running flows run here. |

Rules across the four: the gate runs once per request before dispatch, and a workflow's data
activities re-enter decision and scope with the run's actor; the actor rule (a run acts as the
caller who started it unless the definition is published as system, an explicit, permissioned
property; an HTTP-endpoint workflow acts as the request's caller); asynchronous entry points
enter at step 1 with tenant and bucket explicit, as the system actor; connectors move into the
connection system so an external read is a read job and an external write a durable
background action.

### Design decisions

- **Organization scope on data.** `CrestOrganizationPart` with an index (`OrganizationId`),
  carried by every scoped type and filled by the owning module, is the row's owner (in
  Crest.Parties, [access.md](access.md) › Parties). Per-item grants are Sharing's
  ([sharing.md](sharing.md)). The hierarchy's subtree scoping ([parties.md](parties.md) ›
  Hierarchies) joins on the owner column.
- **What is audited: actions always; reads selectively.** Every action (unit of work, durable
  background) and every denial is recorded. Reads are recorded when impersonation is active,
  when they go through an external connection, when they are read jobs (exports, bulk reads),
  and for types or fields a tenant flags as sensitive; ordinary scoped reads are not. This
  matches standard systems (change logs always; read-access logging opt-in per sensitive
  object, as SAP's read access logging, Dynamics' optional read audit and CloudTrail's opt-in
  data events). **Read logging can be switched fully on** as a tenant setting
  (`AccessAuditSettings.LogReads`, read by the auditor per event), and then logs are per user
  request, one event per operation, no sampling. Volume is handled by **retention settings**,
  not by recording less: short by default, extended per tenant where a legal regime requires
  it. The auditor writes decisions in a child scope that commits on its own; read events go to
  their own collection through a batched writer, trimmed per collection on its own schedule.
- **Who authenticates: everything, in this order.** Tenant first (which pages even exist is a
  tenant question), then the theme shell from the URL, then the user (anonymous is
  authenticated as the Anonymous caller, since it is the first check on use and on the share
  settings of content, pages and media), then permissions. The gate owns authentication before
  routing.
- **One registry.** The engine's registry (`IActivityRegistry`) is the registry. Crest's
  `WorkflowRegistryCatalog` and its providers (`IWorkflowTriggerProvider`,
  `IWorkflowActivityProvider`, `IWorkflowFlowProvider`, `IWorkflowConnectorProvider`, hook
  slots) migrate onto it: each catalog kind becomes engine descriptors with grouping and
  position in `CustomProperties`, and every consumer of the catalog (the registry page,
  `api/crest/workflows/registry`, the trigger publisher, the stock-activity and
  connector-operation providers) reads the engine registry instead. The catalog is deleted when
  its last consumer moves. (How each catalog kind maps is open, [Open questions](#open-questions).)
- **HTTP-endpoint workflows** are served under `/workflows/`, consolidated so a caller knows
  it is calling an action; later `/workflows/{group}/{name}` once workflows have groups and an
  organization system. Their routes are defined entries in Crest.Server's API registry
  ([above](#workflows-builds-on-top-of-the-module-apis)); the trigger's `Authorize`/`Policy`
  inputs give way to the operation's decision, and the run is the request's unit of work.
- **Structured queries compiled through YesSql, not SQL text.** A tenant-authored query is a
  step list (source index or content type, filters, joins, projections, sort, page) compiled
  through YesSql's `SqlBuilder` and dialects into parameterised SQL with the tenant prefix; the
  ScopeSet's predicates are added as filter steps for every table the plan touches, so
  injection is impossible by construction. SQL text survives for the SQL source (scope-rewritten,
  [queries.md](queries.md) › Scope enforcement) and for external SQL connections, where the
  external system applies its own permissions and the author is an administrator. Reads and
  writes share one stack: writes through the content manager onto the YesSql session, reads
  through the builder onto the same store. A Liquid template names query operations only, so it
  is read-only by kind. The external SQL connector keeps SQL text; parameters-only there comes
  later.
- **Where `Crest.Access` sits.** The chain is core → access contracts → data → connections →
  access implementation and query → surfaces, with workflows on query: `Crest.Access.Abstractions`
  (`ICallerContext`, `IAccessDecision`, `IScopeProvider`, `ScopeSet`, `ScopedExecution`) below
  `Crest.Data`, because the data layer hands out `ScopedExecution` instead of raw sessions;
  `Crest.Access` (caller builder, decision, cache, gate) beside `Crest.Queries`, because
  building a caller reads roles, bindings and policies from the store.
- **System actors.** The **tenant system actor acts as a tenant admin**, always; an
  **organization system actor acts as that organization's admin**, always (background work
  started on behalf of an organization, such as its scheduled flows, runs with the organization
  admin's scope and ceiling). Publishing a workflow as system requires
  `ManageShippedWorkflows`. **Only the system can use the system credentials**: a system caller
  is never produced from a request, a token, a cookie or a job payload; it exists only where the
  path's step 1 admits a background entry point or a definition published as system, built in
  process by `Crest.Access` with no credential a user could present. There is no system user
  account, no system role a user can be given, and no API scheme that yields the system caller.
  Impersonating the system is impossible by construction, and the audit records the definition
  or job that ran as system, never a user. The system actors act as the system accounts
  ([sharing.md](sharing.md) § 1).
- **Per-part permissions** are scope providers on content parts ([permissions.md](permissions.md)).

### Tasks

Each step compiles and runs on its own. Pre-release rules apply: no compatibility code, dev
tenants reset ([architecture.md](architecture.md)). The order is the dependency order.

#### 1. `Crest.Access`: the access machinery

Built: the caller per request, the permission version, the server-side state cache, the one
decision behind `IAuthorizationService`, scope providers and the `ScopeSet` (a target with no
provider is refused), organization scope on data, the auditor, the gate owning authentication,
background entry points with an explicit system caller, the `Api` scheme for machines (a
principal with no user record is an application caller), caller lifetime for circuits,
websockets and bursts, the two projects. Details: [access.md](access.md). The clients send
`X-Shell`/`X-Org` (`CrestShellContext`), the loopback client forwards them.

- [ ] **The analyzer** in `Crest.Build` that refuses `ISession`, content-manager writes,
  `IDbConnectionAccessor` and `IQueryManager` outside `Crest.Access` and the registered
  sources.
- [ ] **The conformance suite**: fixtures (callers, organizations, roles, policies, rows) and
  expected verdicts, run against every pipeline and every generated surface; a differing
  answer fails the build. Written, not yet run: `test/Crest.Tests/Access/Conformance/`
  (`AccessWorld`, the fixture tenant and verdict table; `AccessConformanceTests`;
  `CallerContextFactoryTests`, with revocation between two requests and a subscription policy
  as a fixture; `AccessGateTests`; `HandDownTests`; `DataAccessBoundaryTests`, the analyzer's
  rules over the source, failing on the known bypasses by design),
  `Crest.Members.Tests/OrganizationConformanceTests`, `ConnectionCallerTests`, and on the
  workflow side `AccessGateTests`, `WorkflowCallerLifetimeTests`,
  `ActorStampingDispatcherTests`. Still to add: the workflow activity gate and the query
  pipeline running the table.
- [ ] The user picker's scope.
- [ ] OpenId validation enabled by the host recipe ([machine-actors.md](machine-actors.md)).

#### 2. The query pipeline and the connection system ([queries.md](queries.md))

Built: the structured query model (a JSON step list: from, join, filter, project, sort, page;
declared parameters; output columns derived from the index types) compiled through YesSql's
dialect builder into parameterised SQL with the caller's `ScopeSet` conjoined for every table
(the `Structured` source); the SQL source parses templates as written (no Liquid rendering,
`{{`/`{%` rejected at validation) and the AST rewriter (`PlatformSqlRewriter.VisitSelect`)
conjoins the scope for every table, subquery, CTE body and set-operation branch, refusing a
table with no rule; the source contract carries cancellation, page tokens, totals and typed
columns through every caller; the `System` source serves `item`, `user` and `site`, in-process
and memoised per request; `QueryDescriptor` and `IQueryCatalog` describe every saved and
built-in query. Every run pages.

- [ ] **Statement compilation cached** per query version and scope signature; no per-request
  rewrite.
- [ ] **`ReturnContentItems` loads through the gate** (the content provider's predicate, not a
  per-item check after the fact).
- [ ] **The SQL connection over the host database** as the dogfooded connector, replacing
  the stock SQL source; dialect paging through YesSql's `ISqlDialect`.
- [ ] **The read job**: the asynchronous read pipeline on the durable background machinery;
  streaming through `DbDataReader`.
- [ ] **The connection system**: the protocol modules `Crest.Http`, `Crest.Rest`, `Crest.Soap`,
  `Crest.GraphQL` (with `Crest.Apis.GraphQL.Client` becoming or folding into the GraphQL one),
  owning the model, sealed secrets, OAuth, retries with `Retry-After`, rate limits and the SSRF
  guard, moved down from Workflows; SQL connections (host and external) in the data layer;
  Queries' sources over both; search engines as connections (Lucene and Elasticsearch sources
  become connections, their APIs retired).
- [ ] The rest of queries.md's list: the builder, column types by aspect, export, the
  plugin registry.

#### 3. The registry

The engine's `ActivityDescriptor` comes from `IActivityProvider`s (`TypedActivityProvider` for
CLR activities, `WorkflowDefinitionActivityProvider` for each published definition, which
turns a definition's `Inputs`/`Outputs` into synthetic `InputDescriptor`/`OutputDescriptor`s so
a flow is itself a typed activity); `IActivityDescriptorModifier` post-processes every
descriptor (Crest's `FieldDependencyDescriptorModifier` adds Crest data to
`CustomProperties`); `IActivityRegistryPopulator` runs the providers from Crest's
`PopulateRegistriesTask`.

- [x] **Query descriptors are engine `ActivityDescriptor`s** emitted by a Crest
  `IActivityProvider` (`QueryActivityProvider` over `IQueryCatalog`,
  `Server/Queries/RunQuery.cs`): one per saved query and one per code query, with the query's
  parameters as `InputDescriptor`s, its columns as typed `OutputDescriptor`s, and the output
  schema (pageable, total, column types) in `CustomProperties` under a Crest key. This gives the
  palette, `/descriptors/activities`, the studio's hint handlers and the options endpoint for
  free. The descriptor's `ClrType` is the one run-query activity.
- [x] **The run-query activity** (`RunQuery`): executes its descriptor's query through the
  query pipeline with the run's actor; outputs the typed rows and a page token.
- [x] **Content operations** (create, update, delete, publish, unpublish per type) as typed
  activities wrapping the content manager, so validation, handlers and the audit content
  handler keep firing; the only writes outside workflows. `Server/Contents/ContentOperations.cs`:
  `ContentOperationActivityProvider` emits one descriptor per content type and operation
  (`Crest.Content.{Operation}{Type}`, CLR type `ContentOperation`), create and update with the
  type's fields as typed synthetic inputs (text, HTML, Markdown, numeric, boolean, date,
  date-time, content, user and option pickers; other kinds stay with Copy fields); the gate asks
  the owner variation of the operation's content permission against the type (the
  `crest:permissionResource` descriptor property), and `ContentOperationService` asks the
  decision again with the item, validates before it writes, and calls the content manager. Copy
  fields and Move fields write through its update path. Update writes the latest version in
  place (no draft is forced).
- [ ] **One registry** ([Design decisions](#design-decisions)): Crest's catalog kinds
  (triggers, flows, connectors, hook slots) become engine descriptors; the catalog's consumers
  move to the engine registry; the catalog goes. Not started; the mapping is open.
- [ ] **Operation names per tenant**, mapped from the module's declared name, so a tenant can
  replace a shipped read with a saved query of the same name (the override model of
  blazor-display.md § 5a, applied to operations). Not started; the model is open. Today a saved
  query whose name equals a built-in one already hides it in the catalog's list.

#### 4. The action pipelines

The engine runs its default pipelines (`EngineFeature.cs`; workflow: heartbeat, engine
exception handling, persistent variables, exception handling, scheduler; activity: exception
handling, execution logging, notifications, log-persistence evaluation,
`BackgroundActivityInvokerMiddleware` as the terminal). Crest's extensions are two inserted
middleware and service replacements in `Server/Features/CoreStartup.cs`
(`ICommitStateHandler` → `UnitOfWorkCommitStateHandler`, `IBackgroundActivityScheduler` →
`DurableBackgroundActivityScheduler`, `IBackgroundActivityInvoker` →
`ShellScopedBackgroundActivityInvoker`, the scheduled-task handler, the bookmark queue worker,
the background consumers).

- [x] **The access gate as engine middleware** (`Server/Security/AccessGate.cs`):
  `WorkflowAccessGateMiddleware` at index 0 (the run's actor and decision, once per burst) and
  `ActivityAccessGateMiddleware` before the terminal invoker (decision and scope for each data
  activity), both inserted with `pipeline.Insert` in `CoreStartup.AddCrestWorkflows` rather
  than a rebuilt list, so engine updates do not silently drop them.
- [x] **The actor rule** ([Running identity](#running-identity)), including actors where the
  engine had none: the scheduled-task handler, the bookmark worker and the background-job runner
  run as the resolved caller through `IAccessRunner`; a resume takes the actor from the
  instance; a timer- or cron-started flow runs as the system actor, declared on the definition;
  a flow with neither is refused by the gate.
- [x] **HTTP-endpoint workflows through the request path**: the gate has run, the caller is
  the request's, `HttpWorkflowEndpointAuthorizationHandler` asks the decision.
- [x] **Data activities through the gate**: `[RequiresPermission]` on the field and connector
  activities (`UseConnections`), the run-query activity and the content operations, which are
  the only data activities.
- [x] **The two action pipelines named and hosted** (`Server/Units/ActionPipelines.cs`), with
  the queue's partition and idempotency keys ([Queues](#queues)).
- [x] **The engine API gate is a consumer of `IAccessDecision`** (§ Ownership and permissions).
- [ ] **Connectors into the connection system** ([queries.md › Connections](queries.md#connections-the-protocol-modules-and-the-data-layer)):
  the model, sealed secrets, OAuth, `ConnectorHttp` (SSRF guard, redirects off, size
  limits), rate limiters, token cache and `ConnectionResilienceStrategy` move down into the
  protocol modules; the activities stay as consumers; the webhook controller verifies through a narrow verifier.
- [ ] **Expression limits**: Jint and Fluid limits configured in one place
  ([blazor-display.md](blazor-display.md) § 12, with the parked sandbox).
- [ ] **HTTP endpoints and the engine API in the Server's API registry**
  ([Workflows builds on top of the module APIs](#workflows-builds-on-top-of-the-module-apis)).

#### 5. Move the reads

- [ ] `api/crest` lists (content items, content types, groups, navigation, menus, media,
  users, roles, tenants, features) re-expressed as query operations, module by module.
- [ ] **The pickers**: the option-sources scope logic becomes scope providers; the
  controller becomes the generated surface of the queries each picker binds; the user
  provider gets scope.
- [ ] Members and Parties reads as query operations.
- [ ] This is the window where two paths exist; one module per change keeps it short.

#### 6. Move the writes

- [ ] Content create, update, delete, publish as content operations.
- [ ] Members and Parties writes as actions.
- [ ] Machine clients: the `Api` scheme and bearer tokens on the generated routes
  ([machine-actors.md](machine-actors.md)); the gate sees a machine caller.

#### 7. Generate the surfaces

Every generated route is an entry in Crest.Server's API endpoint registry, beside the APIs
modules declare ([architecture.md › Modules declare, Crest.Server registers and serves](architecture.md#modules-declare-crestserver-registers-and-serves)).
A generated route implements a module's declared API, or is a defined entry (an operation
endpoint); a module's domain API is never moved into a workflow endpoint.

- [ ] **REST + OpenAPI**: one route per operation (`GET` for queries, `POST` for actions),
  parameters and schema from the descriptor.
- [ ] **GraphQL** regenerated from the registry: a field per query, a mutation per action.
- [ ] **RPC** (JSON-RPC; gRPC later).
- [ ] **The Liquid slot binding** `{ query, parameters, path }` → a query operation.
- [ ] **The picker backend** and **the workflow palette** from the registry.
- [ ] A surface adds protocol concerns only (negotiation, batching, rate limits).

#### 8. Retire

- [ ] The platform's `api/content`, the stock GraphQL content schema and per-query types,
  `api/queries`, the Lucene and Elasticsearch APIs (search is a connection).
- [ ] The Liquid `query` filter and the Razor query helpers.
- [ ] `Crest.Scripting` and every global method provider.
- [ ] The hand-written `api/crest/*` controllers as each generated route lands. The API stays
  declared by its module ([MVC/api.md](MVC/api.md) § Placement); only the implementation
  becomes a generated route over that module's operations.

### The surfaces today, and their fate

| Path | Today | Fate |
| --- | --- | --- |
| `api/crest/*` (about 25 controllers) | one permission per controller, no scope on lists | declared by their module and registered in Crest.Server's API registry; implementations generated over operations (5–7) |
| `api/crest/option-sources` | one gate; content scoped by scope providers; user provider unscoped | generated (7) |
| Platform `api/content` | `AccessContentApi` + resource checks | retired (8) |
| GraphQL | execute permission, scoped content filters, hand-written query types | regenerated (7) |
| `api/queries/{name}` | per-query permission; sources scoped; 404 on deny | retired; queries are operations (3, 8) |
| Lucene, Elasticsearch APIs | one permission; scoped hits, raw documents only for an All rule | retired; search is a connection (8) |
| Liquid `query` filter, scripting | per-query permission | slot binding through the gate (7); scripting removed (8) |
| Workflow connectors, content activities | gated per activity, actor rule | connectors into the connection system (4) |
| Workflow HTTP endpoints, engine API | defined entries (`WorkflowHttpEndpointSource`) and a declared contributor (`WorkflowsManagementApi`) in the Server's API registry, behind the gate | ☑ |
| Members, Parties | the caller carries side and organization; decisions per organization resource | routes generated (5–6) |

## Still to build

The workflow service, its registry, connectors, approvals, ownership, permissions, units
of work, hooks, field dependencies and the platform activities are built (sections above).
This section holds what is designed but not built.

### Queues

Built: see [Units of work](#units-of-work-how-a-run-commits).

**Two queues, two guarantees.** The *write side*: no two units touching one object
interleave, through the engine's `IDistributedLockProvider` keyed by correlation id around a
unit (`IWorkflowObjectLock`, built), with version checks on what the unit read and a re-run on
conflict, and a serial writer applying working sets only if a measured conflict rate asks for
them. The *event side*: the engine's stimulus dispatch and bookmark queue, hardened: emitted
after commit, partitioned by correlation id (one consumer per object at a time, in order),
payloads carrying the object version the unit committed, at-least-once delivery with
idempotency keys for exactly-once effect. Costs accepted: read-your-writes latency with a
pending state in the UI, optimistic conflicts re-run.

The event side as built (`Units/WorkflowStimulusQueue.cs`): every stimulus is written as a
`WorkflowStimulus` document in the raising unit (keyed by the unit's id and the stimulus's slot
in it, `WorkflowUnitOfWork.Id`/`NextSlot`; the key is the flows' `StimulusId` input), so it
exists only if the unit commits. After the commit its partition (the correlation id; an
uncorrelated stimulus is its own) is drained: each delivery is one child scope that takes the
object's lock, takes the partition's oldest pending stimulus, sends it and marks it delivered;
the mark commits with the flows the send ran and the lock is held until that commit, so one
object's events are consumed one at a time, in order, and once. A send that throws is retried
(three attempts, then set aside as failed); one whose flows failed their unit counts as
delivered (the failure is on the instances); stimuli a dead process left pending are delivered
again at activation. Values keep their types (a content item in the input stays one).

- [ ] The object version in the payload (the raisers name no version yet).
- [ ] A periodic redelivery sweep between activations.
- [ ] Version checks and a serial writer beyond the object lock, only if a measured conflict
  rate asks for them.

### Designer: the transition onto Crest components

Built today: see [Designer and admin pages](#designer-and-admin-pages).

The designer moves onto Crest components last; Studio on MudBlazor in the Crest admin is
acceptable until the backend is complete. Crest's UI layer is its own fork over Radzen
(`Crest*` components, theme tokens, localization), and the Studio fork must end up on it so the
designer is themed, localized and tested like every other Crest page, without losing what the
Elsa designer offers. What stays foreign: the canvas (X6, DOM interop) and Monaco. Studio is on
Radzen 11 already; what remains is MudBlazor (about 2.6 MB, loaded only when the designer
opens). Inventory: 98 Razor files, ~800 usages, every one with a Crest counterpart: MudStack 70
→ CrestStack; MudText 53 → CrestText; MudTable family ~120 → CrestDataGrid/CrestTable;
MudMenu family 62 → CrestMenu/CrestContextMenu/CrestSplitButton; MudTabs 44 → CrestTabs;
MudButton family ~73 → CrestButton/CrestToggleButton; MudSelect family ~80 → CrestDropDown;
MudTextField family ~51 → CrestTextBox/CrestTextArea/CrestFormField; MudTooltip 30;
MudAlert 19; MudDivider/Spacer/Paper/Container ~46 → markup + CrestCard; MudDialog 12 →
CrestDialog; MudCheckBox/Switch/Radio ~16; MudForm 7 → CrestTemplateForm; MudChip ~6;
MudBadge 4; MudFileUpload 3 → CrestUpload; date/time pickers 4; MudTreeView 2 → CrestTree;
MudTimeline 2; MudExpansionPanels 4 → CrestAccordion (the palette); MudProgressLinear;
ISnackbar → Crest notifications. Proposed order (W6): the shared UIHints (20 files) and Shared
components (17) first (the property panel every view uses), then the editor, then list/instance
viewers; MudThemeProvider last (until then the Mud theme reads Crest tokens).
`workflows-designer` stays green throughout. New pages (hook attachment, hook and webhook
insight, per-process metrics) are built on Crest components now, independent of the move.

- [ ] The transition itself (W6).
- [ ] The security token consolidation (`ICrestAntiforgery`), with the transition.
- [ ] Edit without Publish: Studio ties editing to the `publish` link.
- [ ] **Hook and webhook insight**: clear, labelled sections for **webhooks** (pub/sub
  insight, process instances and history) and for **internal hooks**, with the same insight
  data, as audit feeds ([audit.md](audit.md) › Activity feeds).
- [ ] **Per-process metrics**: run counts, timings and failures per flow, on the workflow
  pages, as audit feeds (source open, below).
- [ ] **The designer UI that comes with it**:
  - [ ] The tier badge.
  - [ ] Outdated + Reset.
  - [ ] The access-list editor (Edit, Run, View; today the API sets them).
  - [ ] The field-dependency panel.
  - [ ] The pending state.
  - [ ] An OAuth "Authorize" button on a connection.
  - [ ] A dedicated mapping editor for `Copy fields` (until then the rows are a JSON input).

### Elsewhere

- [ ] **Machine actors** ([machine-actors.md](machine-actors.md)).
- [ ] **A registered access-policy seam** ([The member caller](#the-member-caller)).

## Open questions

- **One registry: how each catalog kind maps.** Only triggers and activity presets are
  operations; a shipped flow is a definition JSON to import, a connector is an auth kind and
  base URL (and moves with the connection system), a hook attachment is a pairing. What does a
  flow, a connector, a hook slot and a system attachment become in the engine registry (a
  descriptor each, with the JSON and settings in `CustomProperties`, or providers that stay as
  import sources), and may the registry API's shape change for the pages that read it?
- **Operation names per tenant.** Is it the existing shadowing made consistent (find and
  execute too), or a stored per-tenant map from the declared name to the tenant's operation
  (the three-ids-and-a-map model), and where is that map edited?
- **Workflows as permissioned, shareable assets.** Treat workflow definitions the way the media
  system treats files: owned, permissioned, shared and approved through the same mechanisms
  ([media.md](media.md), [sharing.md](sharing.md)), so people build and deploy workflows
  together, and a person can keep workflows only they use (a personal drive of workflows)
  sandboxed from the tenant's. Questions: what a definition's share levels map to (View, Run,
  Edit, Publish), whether a personal workflow may touch tenant data or only the owner's, how
  approval (publish) fits the approvals system ([permissions.md](permissions.md) › Approvals),
  and what "sandboxed" bounds (the actor it runs as, connectors it may use). The View list is
  the first step.
- **A flow's output contract**: what a composable flow promises its caller.
- **Per-process metrics source.** Built from the engine's indexes (instances, execution logs,
  activity records) now, or after run history reaches the audit feeds?
- **Background throughput (W5).** Connector background jobs run on **one worker per tenant**,
  so one slow external call blocks that tenant's other background work; the object lock is
  **file-based**, so it works on one server only; poll state is **one document per tenant**
  updated without a lock, so two polls at once can lose an update. Options: (1) fix the
  poll-state race now (a record per connection, or a lock) and decide the worker count and a
  database-backed distributed lock when there is a second server or real traffic; (2) decide
  everything now; (3) leave all of it until measured. Recommendation: 1; the race is a bug, the
  rest is capacity.
- **Moving the designer onto Crest components (W6).** Options: (1) staged, as above; (2) keep
  MudBlazor permanently, themed from Crest's tokens, and build only new designer pages on Crest
  components; (3) rewrite from scratch reusing only the canvas. Recommendation: 1, still last;
  the edit-without-publish fix comes in the first stage; if the move stalls, it stops at option
  2.
- **The activity gate fails open** for an activity that declares no permission; who may run
  the messaging activities ([access.md](access.md) › Open questions).
- **Does the member ceiling also ceiling staff-only permissions in reverse?** Probably
  unnecessary: staff are trusted with member-portal surfaces through impersonation.
- **What the platform offers natively for connections**, to research before the connection
  model is designed ([queries.md](queries.md)).
- **The access machinery on separate hardware (later).** About half of all queries are
  permission work: building each request's caller for its side and organization, the policy
  verdicts, the hierarchy's "everyone under me", the file access index. The direction:
  `Crest.Access` gets an API that can be deployed apart from the web servers, in-process for a
  small host and a separate service for a large one, without callers changing. Bears on
  [parties.md](parties.md) (the hierarchy) and the caller state cache, [media.md](media.md)
  (the access index), [queries.md](queries.md) (the scope inside queries) and
  [shells-and-themes.md](shells-and-themes.md) (shells on their own servers).
