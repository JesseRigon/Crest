# Workflows

How Crest's workflow service works: what a workflow is here, who may do what to one, how
a flow gets started, how a run commits (or does not), and how a downstream module builds on
top of it. Workflows are the **action** kind of operation: every read and write in Crest goes
through one registry, one request path, four pipelines and one access machinery, and that
plan, with its task list, is the [Operations](#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)
section (merged from the former operations.md on 2026-10-10). Design not yet built, and the
rulings, are under [Still to build](#still-to-build); the flowchart of a run's life is
[workflows.mmd](workflows.mmd).

## What it is

`Crest.Workflows` is the tenant's workflow service. It is a vendored fork of Elsa 3 (the
engine: activities, flowcharts, bookmarks, bursts, the designer) and the tenant's only
workflow engine: the stock OrchardCore workflow module is gone (2026-10-10); platform
modules register engine activities and raise registry triggers like any Crest module. The
engine's own HTTP API lives at `crest-workflows/api` behind the tenant
cookie, Crest's antiforgery header and its permissions; the designer (the forked
Studio, Blazor WebAssembly) is loaded on demand into the Crest admin.

What the system does:

1. **Connects modules to each other without references.** A stage change in one module
   creates a party role; a posted document starts a tenant's own flow. Modules register
   activities, triggers and flows; the definition holds the connection.
2. **Connects to Crest and Orchard modules.** Content events, users and roles, e-mail,
   notifications, forms: everything Orchard already does is a palette item, not a
   re-implementation.
3. **Connects to external services.** Outgoing HTTP calls with stored credentials, incoming
   signed webhooks, polling on a schedule, retries and rate limits, without code.
4. **Is authored by tenants in the Crest admin**, with drafts, publishing and versions.
5. **Is multi-tenant and permissioned like everything else:** Orchard permissions and roles,
   per-shell data and execution, shipped definitions migrated by code.

**Where it lives.** Workflows is part of the application layer
([application-layer.md](architecture.md)): `Crest.Workflows` in the Crest
repository, engine included. The engine API prefix is `crest-workflows/api` and activity
type names are `Crest.Workflows.*`. A downstream module's activities, hook attachments and
shipped flows are that module's own.

Workflows is a **registry** like Parties and the other core registries: modules register
what they contribute - triggers, activities, hook slots, shipped flows, connectors - through
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

## Architecture

```
 Crest admin (Blazor WASM, Radzen)
 ├─ Workflows pages: Definitions, Instances (Studio, lazy), Connections, Approvals (Crest)
 └─ Designer: the forked Studio (MudBlazor), loaded on demand in its own container
        │  engine API (FastEndpoints) at ~/crest-workflows/api, tenant cookie + antiforgery,
        │  Orchard permissions mapped onto the engine's per-endpoint grants
        ▼
 Crest.Workflows  (the fifth core registry; depends on feature "Crest.Workflows", overrides its services)
 ├─ engine/   vendored elsa-core 3.6.0 (22 projects, renamed Crest.Workflows.*)
 ├─ Server/   the Orchard integration: stores on YesSql per shell, definitions as content items,
 │            the API gate, the registry, ownership, access, connectors, approvals
 ├─ Contents/ content triggers and tasks (feature Crest.Workflows.Contents)
 ├─ Abstractions/ the contracts modules bind to (descriptors, IWorkflowTriggerPublisher, constants)
 ├─ blazor-wasm/ + studio-host/   the admin pages and the lazily loaded Studio container
 └─ designer/ vendored elsa-studio 3.6.0 (15 projects)
```

**The engine is Elsa 3.6, vendored and renamed** (`engine/`, `designer/`; `UPSTREAM.md` and
the `UPSTREAM-COMMIT` files record the upstream tag and commits). Upstream is a diff source,
not a dependency: no `Elsa*` package exists in the graph; namespaces, assembly ids, activity
type ids (`Crest.Workflows.WriteLine`), the API prefix (`crest-workflows/api`) and the
YesSql collections (`CrestWorkflows_*`) are renamed. Licences: elsa-core and elsa-studio
MIT, the original Orchard integration BSD-3-Clause, transitive FastEndpoints (MIT), Jint
(BSD-2), Fluid (MIT), Polly (BSD-3), MudBlazor (MIT). At every upstream diff, refuse anything
not MIT / Apache-2.0 / BSD.

**Crest.Workflows is core.** Every module that registers or raises anything
depends on it (Parties and every downstream module that registers declare the feature);
there is no no-op mode. Crest's UI and API core never references it; the generic pieces
the module needed from Crest (late service containers, lazy module pages, JS components,
antiforgery access) are Crest features any module can use.

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
| Unbuilt upstream pieces (Timers/Quartz, Queries, Data, UI, the MVC designer host) | `reference/`, kept for diffing only |

## Engine

**One engine.** `CoreStartup` (`Order = int.MaxValue`, last by feature dependency and then
by order) wires the engine into the tenant container. A platform module that contributes
activities or triggers references `Crest.Workflows.Abstractions` (the contracts) and the
engine's `Crest.Workflows.Core` and `.Engine` (the activity base classes and the module
builder), and from a startup gated on the `Crest.Workflows` feature registers an
`IWorkflowActivityProvider` / `IWorkflowTriggerProvider` for the registry and calls
`services.ConfigureCrestWorkflows(w => w.AddActivitiesFrom<Startup>())`. Triggers are
raised through `IWorkflowTriggerPublisher` from the module's own handlers. The stock
OrchardCore workflow module, its bridge (`PlatformEvent`, `PlatformTask`,
`StockActivityRunner`, the `IWorkflowManager` override, the stock Liquid and JavaScript
evaluators) and its activity contracts (`Crest.Workflows.Platform.*`) were deleted on
2026-10-10 once the stock activities were ported (§ Platform activities).

**Tenant citizenship.** One engine per shell: every engine service, store and hosted service
lives in the tenant container. Definitions are `WorkflowDefinition` content items
(`WorkflowDefinitionPart`; drafts and versions are Orchard's); instances, stored triggers,
bookmarks, execution logs and activity execution records are YesSql collections with
per-shell migrations. The file lock directory is `<data root>/Sites/<tenant>/locks`;
`MediatorOptions.JobWorkerCount = 1` per shell; `JobRunnerHostedService` and
`ActivateTenants` are started from a deferred task on activation and stopped on
termination; `LocalScheduler` (timers, cron) is enabled per shell and disposable so a
released shell's timers stop. Orchard disposes containers synchronously, so the engine's
async-only tenant service is replaced (`SyncDisposableTenantService`) and a scan test pins
the async-only surface of the engine assemblies.

**Definitions store and publisher.** `YesSqlWorkflowDefinitionStore` maps
`WorkflowDefinitionPart` ↔ `WorkflowDefinition`; `ContentItemWorkflowDefinitionPublisher`
publishes through the content manager (a save with `publish: true` on a new definition
stores the draft first). The engine's definition notifications are raised once, by
`WorkflowDefinitionContentHandler` (Retracted with the definition as unpublished, so the
indexer drops its triggers). Both call the ownership guard (below). No store commits
mid-run: they flush, and the burst's shell scope commits ("Units of work" below).

**Fork changes worth knowing** (engine): `FindWorkflowInput<T>` (default when a key is
absent - `GetWorkflowInput` throws, which faulted flows started from the API, a timer or a
webhook); `LocalScheduler : IDisposable`; `BookmarkQueueWorker.Stop()` tolerates a worker
that never started (a tenant deactivated after a failed setup). Designer: Radzen 11 and ASP.NET 10.0.9 (Crest's
versions), the ILocalizer clash aliased, `data-testid` on Save/Publish/Run, the ClientLib
webpack bundles built by `designer/Directory.Build.targets` when missing (gitignored).

**Speed** (checked in the engine source): the definition JSON is parsed once and cached
(`IWorkflowDefinitionCacheManager`, enabled) into a graph of compiled C# activities; only
the walk between nodes is interpreted. The costs are JavaScript/Liquid expressions (Jint,
per run) and persistence (instance state, execution records, journal). Dials that exist:
commit strategies (`IWorkflowCommitStrategy`, `IActivityCommitStrategy`),
`LogPersistenceMode` per activity, literal/typed inputs instead of script.

**After-commit dispatch.** Stimuli are never sent through the engine's `IStimulusDispatcher`
(its background worker is a hosted service of the tenant container, which Orchard never
starts, and its scopes carry no `ShellScope`); `WorkflowStimulusQueue` sends them as Orchard
deferred tasks, one child scope per stimulus, after the raising scope's session commits.

**Performance dials.** The engine's default workflow commit strategy
is none: a burst persists once, at its end, through the commit handler - exactly the unit
model, so no strategy is configured; an activity-level strategy a designer picks is a
flush within the unit, never a commit. Log persistence stays at the engine default (inputs
and outputs journaled): the posting flows' journal is the audit trail. Compile-on-publish is
**not** done: the graph materializer's cache (`UseCache`) already keeps parsed graphs, and
nothing measured points at graph interpretation.

## Ownership and permissions

Every definition has an **ownership tier**, kept in its custom properties:

- **system** - code's. Shipped by a module, read-only for every user including the
  administrator, upgraded in place when the module bumps its version. The posting flows.
- **shipped** - a template a module ships that the tenant may edit. Editing marks it
  *forked*; a forked copy is not upgraded until it is **reset** to the shipped version
  (`POST api/crest/workflows/registry/flows/{key}/reset`), which keeps the tenant's edits in
  the version history. A flow shipped as a draft is turned on by publishing it.
- **tenant** - the tenant's own.

Permissions are Orchard permissions, implied by the `ManageWorkflows` umbrella:
`ViewCrestWorkflows`, `EditCrestWorkflows`, `PublishCrestWorkflows`,
`RunCrestWorkflows`, `ManageShippedCrestWorkflows`, `ManageCrestWorkflowConnections`.
The engine API maps each endpoint to one of them and answers 403, never a login redirect.
Two shipped roles: `WorkflowEditor`, `WorkflowViewer`. A definition can additionally name
who may **edit** and who may **run** it (role or user names; `GET/PUT
api/crest/workflows/definitions/{id}/access`); the super user and Administrator are never
narrowed. `WorkflowOwnershipGuard` enforces the tier on every change path, including the
engine's own endpoints.

**The API gate** (`Security/ApiSecurityMiddleware`, on the API path
branch, slotted before the Users module's authentication/authorization pair and
authenticating the branch itself): anonymous → 401; without *View workflows* → 403; a
non-GET without Orchard's antiforgery token → 400 (the engine's endpoints validate none);
then the Orchard permissions the user holds are mapped onto the engine's per-endpoint
permission names as `permissions` claims for the request (`EnginePermissions`: View → every
`read:*` name; Edit → `write:`/`delete:workflow-definitions`, refresh/reload; Publish →
`publish:`/`retract:workflow-definitions`; Run → `exec:`, `trigger:event`, `tasks:complete`,
`write:`/`cancel:`/`delete:workflow-instances`). The gate evaluates the matched endpoint's
own policy itself so a missing permission is a 403 with a reason (left to the authorization
middleware, the cookie scheme redirected the fetch to the access-denied page). Starting a
definition by hand (`execute`, `dispatch`, `bulk-dispatch`) is authorized against that
definition's Run list. A `WorkflowChangeDeniedException` thrown deeper down becomes a 403
with its message. Nothing is cached in the cookie; a role change applies on the next request.
the member permission ceiling applies (it runs inside `IAuthorizationService`).

**Permission set** (`Server/Permissions.cs`; names in `WorkflowsConstants.Permissions`):
*View*, *Edit*, *Publish*, *Run*, *Manage shipped workflows*, *Manage connections*, each
implied by the `ManageWorkflows` umbrella, which stays the umbrella so existing roles keep
working. Approvals are decided per task by role or permission (`all=true` listing needs
`ManageWorkflows`). Pages and controllers take the specific permission: Definitions and
Instances pages, the registry and the triggers list → View; Connections page and API →
Manage connections; triggers reindex → Publish. The stock stereotypes give Administrator
**and Editor** `ManageWorkflows`, so the finer set ships as two recipe roles (also the
provider's stereotypes): `WorkflowEditor` (View, Edit, Publish, Run, admin panel) and
`WorkflowViewer` (View, admin panel).

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
and the store's Delete. GetDraft is guarded because Orchard's `DraftRequired` read creates
and saves a new draft version; a refusal after it would leave a stray draft as the latest
version.

**Per-flow access, in the same pipeline.** Custom properties `Crest.Access.Edit` and
`Crest.Access.Run` list role names and user names (a user name is the per-user
override). `WorkflowDefinitionAccessCeiling` (2026-10-10; it was a raw
authorization handler) is a ceiling inside the one decision: it vetoes Edit, Manage shipped
and Publish when the Edit list is non-empty and names neither the caller nor one of their
roles, and Run against the Run list; it acts only when the resource is a
`WorkflowDefinitionAccessResource`, so `AuthorizeAsync(user, permission, resource)` and a
direct `DecideAsync` give the same answer everywhere (guard, gate, linker, access API), and
the super user and the system are never narrowed.

**Studio follows the same decisions.** `WorkflowDefinitionLinker` replaces the
engine's static linker: a definition's write links (`publish`, `retract`, `delete`, `import`,
`update-references`) are present only for what the acting user may do to that definition,
and Studio's read-only decision is "no `publish` link". Nuance until the UI transition:
Studio ties editing to the `publish` link, so Edit without Publish reads as read-only.

**Running identity** (the actor rule, landed 2026-10-10). A run acts as the caller who
started it. The acting user at trigger time is snapshotted as **identity only**
(`WorkflowUserContext`: user id, user name, tenant, authentication type; workflow input
`Actor` - not `User`, which stock Users events use); `WorkflowCallerResolver` builds the
caller from it again at the start of every burst through `Crest.Access`, so a right lost
between bursts fails the next one, and `IWorkflowAuthorizer`, `RequiredPermission` on the
Crest and content triggers and the `Require permission` activity ask the one
`IAccessDecision`. A definition published with `Crest.RunsAsSystem` (a property that takes
*Manage shipped workflows* to set, enforced by `WorkflowOwnershipGuard`) runs as the tenant
system actor instead; timers, cron runs, the bookmark queue and background jobs resolve the
same way, and a flow with neither an actor nor the system property is refused by the gate.
An HTTP-endpoint workflow runs as the request's caller (`IHttpWorkflowInputContributor`).
Rulings kept: the security token (`ICrestAntiforgery`, the API behind the Orchard cookie)
stays until the UI merge onto Crest components; machine identities (client credentials, API
keys) are a separate plan, [machine-actors.md](machine-actors.md).

## Registry

The fifth registry, shaped like Parties: providers contribute descriptors, the catalog
merges them by key (first registration wins, ordered by position then key), the API and
pages read the catalog. The catalog merges lazily: content handlers raise triggers through
the publisher, which takes the catalog.

- **Triggers** - `WorkflowTriggerDescriptor(Key, DisplayName, Object, Description, Position)`
  from `IWorkflowTriggerProvider`; raised through `IWorkflowTriggerPublisher.PublishAsync`,
  which queues the stimulus to fire after the current unit commits (refuses unregistered
  keys so a typo fails loudly).
  Input to the flow: `TriggerKey`, `Payload` (ids and scalars only; activities re-read
  objects through their registries' services), `Actor`; correlation id = the object's id.
  The `Registry trigger` activity (`RegistryTrigger`, ports Done/Denied/Skipped) subscribes
  by key, with an optional `RequiredPermission` and an optional **payload filter** (one
  `Key = value` per line, case-insensitive equality; a miss ends on Skipped).
- **Activities** - `WorkflowActivityDescriptor(Key, DisplayName, Object, ActivityType,
  Description, Position, Category, IsTrigger, Inputs)` from `IWorkflowActivityProvider`;
  the activity class lives in the owning module and is registered with the engine from that
  module's startup (`AddActivitiesFrom`); `Inputs` presets a generic activity for a purpose.
- **Flows** - `WorkflowFlowDescriptor(Key, DisplayName, Object, DefinitionJson, Position,
  Publish, Ownership, Version)`: the engine's export JSON, embedded under the module's
  `Data/workflows`, imported per tenant by `WorkflowFlowImporter.SyncAsync` right after the
  activity registry is populated (the JSON names activity types). `Publish: false` ships a
  flow as a draft the tenant opts into (anything that changes business data on its own).
  `WorkflowFlowLookup` finds a tenant's copy by `Crest.FlowKey`.
- **Connectors** - `WorkflowConnectorDescriptor(Key, DisplayName, AuthKind, BaseUrl,
  Description, Position)` from `IWorkflowConnectorProvider`; see "Connectors".
- **Extension points.** `Raise trigger` (`RaiseTrigger`, Done/Failed) publishes a registered
  key after this flow's unit commits, with its correlation id and payload by default;
  Crest.Workflows registers the generic `flow.raised`. `Hook` runs a slot's attachments
  inside the unit (see "Units of work"); `WorkflowHookSlotDescriptor` and
  `WorkflowHookAttachmentDescriptor` are the registry's slot and system-attachment
  contracts, listed by the registry API and `api/crest/workflows/hooks`.
- **API:** `GET api/crest/workflows/registry` (triggers, activities, flows with definition
  id, published, ownership, shipped/installed version, forked, outdated; connectors);
  `POST .../registry/flows/{key}/reset`; `GET api/crest/workflows/triggers[?definitionId=]`
  and `POST .../triggers/{definitionId}/reindex` (diagnostics over the stored triggers).

Gotchas: a trigger node indexes only with `customProperties.canStartWorkflow = true`
(shipped JSON and API clients must set it); the engine's save endpoint needs
`variables/inputs/outputs/outcomes` as empty collections; body-less requests must not carry
`Content-Type: application/json`; a flowchart connection from a trigger is port `Done` →
`In`; list inputs accept a plain JSON array.

`POST api/crest/workflows/registry/sync` (ManageShipped) re-runs the activation sync and
answers what it did per flow (Install, Republish, Upgrade, Keep); the decision is
`WorkflowFlowImporter.Decide`, unit-tested branch by branch, and the ownership check calls
sync and expects Keep for every flow.

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
in a unit of their own (§ Units of work).

| Module | Activities | After commit |
| --- | --- | --- |
| Email | `Send email` (to, cc, bcc, from, sender, reply-to, subject, text and HTML bodies, body format) | yes |
| Sms | `Send SMS` | yes |
| Notifications | `Notify users` (comma-separated user names), `Notify content owner` (item id, default the payload's ContentItemId) | yes |
| Twitter | `Update X status` | yes |
| Facebook (Pixel) | `Send Meta conversion event` | yes |
| Users | `Create user` (e-mail, user name, moderation, optional confirmation e-mail with the link as the ConfirmationUrl output), `Validate user` (Anonymous / Authenticated / InRole on the request's user), `Assign user role`, `Unassign user role` (refreshes the security stamp), `Get users by role` (user id → name) | no |
| Tenants | `Create tenant`, `Setup tenant`, `Enable tenant`, `Disable tenant` (default tenant only) | no |
| Crest.Workflows | `Log` (level, message) | no |

Deleted without a port, because the engine already has the behaviour: the stock content
events and tasks (the Contents feature below), the Forms and ReCaptcha form-posting
activities (HTTP workflows are the engine's), `HttpRequestTask` (`Send HTTP request`),
`NotifyTask` (a session notifier with no Blazor equivalent) and `MissingActivity`.
Publishing a `WorkflowDefinition` item never fans out to content triggers.

`Crest.Workflows.Contents` (feature) adds the engine-native content triggers (created,
published, updated, deleted, versioned, draft saved, unpublished) with a content-type filter
and the acting user, and content tasks (create/get/update/publish/unpublish/delete).

## Units of work: how a run commits

A run is a sequence of **bursts**: the engine executes synchronously until a wait (a
bookmark), persists, and resumes later. Here **a burst is a unit of work**: one shell
scope, one YesSql session, committed as one transaction when the scope ends. The engine's
stores flush but never commit mid-run. A burst is bounded by compute time - nothing inside
a unit waits or calls out.

**Hooks run inside the unit.** A hook slot (a module's own slot, such as a document's
created or posting slot, or the generic `flow.hook`) is run either by a flow's `Hook`
activity or by a registry service from inside its own request (`IWorkflowHookRunner`) - so
a tenant's attachment to a created slot runs whether the object came from the API or from a
flow. Each attached flow runs as a child instance in the same scope and session. A
**required** attachment that faults or suspends fails the unit; a **best-effort** one's
failure is journaled. An attachment says "the host must not go through" with `Fail unit`
(an activity's own `Failed` port is its answer, not the unit's). When the unit fails, the
session is cancelled before Orchard would commit it: nothing the burst wrote stands, the
faulted instance is recorded in a fresh scope so the journal shows what happened, and an
API request that ran the hook answers **409** with the slot, the attachment and the reason.

**Atomicity is derived, never asserted.** `WorkflowAtomicityAnalyzer` walks a definition
(nested composable flows included) and classifies it atomic or long-running; a hook accepts
only atomic attachments (refused at attach and at publish, naming the node and suggesting
the event side). Boundaries are everything that waits: triggers, delays, HTTP endpoints,
approvals, connectors, external platform activities. `Raise trigger` is not one.

**External calls are background activities.** `Call connector`, `Poll connector` and
`Orchard external task` are engine background activities: the burst bookmarks the node and
commits; `DurableBackgroundActivityScheduler` writes the job in that same unit (so it exists
only if the unit committed), runs it afterwards in a child scope of its own with the
connection's auth, resilience strategy and rate limit and an `Idempotency-Key` header, and
hands the result back by bookmark - the next burst. A job whose unit failed is never run;
jobs a dead process left are run again when the tenant activates. The author writes one
linear flow.

**Per-object serialization.** `IWorkflowObjectLock` (the engine's distributed lock, keyed by
the object's id, re-entrant within a unit) is taken by the owning registry service around
every write to an object and by the after-commit send around each correlated run: no two
units touching one object interleave. What is still pending for an object - flows about it that
have not finished and the jobs they wait on - is `GET api/crest/workflows/pending?correlationId=`.

Shell scopes everywhere: scheduled tasks (timers, cron, delays), the engine's background
command and notification channels and the bookmark-queue worker all run in shell scopes
of their own (`ShellScopedRunScheduledTaskHandler`, `ShellScopedBackgroundConsumers`,
`ShellScopedBookmarkQueueWorker`), so every entry point is a unit.

### Why, and the rules in detail

**Units of work, hooks and queues.** The posting activities and
anything composed of several steps must be ACID: an invoice posts or does not, and what the
tenant attached to its creation (copying the customer's route and terms) is in the same
unit - the invoice with those fields exists, or nothing does. Found on the way and fixed
first: the engine's YesSql stores committed the whole session after every state write, so
a two-step flow left step one committed when step two failed; and triggers were raised
inside the raiser's transaction, so hooked flows ran entangled with the posting and saw
uncommitted state.

- **A unit is a burst.** The engine already runs a flow as bursts: synchronously until a
  bookmark, persist, resume later. A unit of work is one burst and commits as one YesSql
  transaction when its shell scope ends; no store commits mid-run. A burst that a required
  hook or atomic step fails is **cancelled** (every write discarded) and the failure is
  recorded for the journal in a fresh scope. Bursts are bounded by compute time: nothing
  inside a unit waits or calls out.
- **External calls are boundaries, like waits.** `Call connector`, `Poll connector` and
  the external platform activities (`Send email`, `Send SMS`, `Notify users`, ...:
  `IUnitBoundary`) are the engine's own
  **background activities** (`RunAsynchronously`): the engine's middleware bookmarks the node
  and hands it to the scheduler; Crest's `DurableBackgroundActivityScheduler` writes the
  job (`WorkflowBackgroundJob`, indexed, idempotency key instance + node + bookmark) in the
  unit, so it commits with the bookmark or not at all, and runs it after commit in a child
  shell scope of its own; `ShellScopedBackgroundActivityInvoker` resumes the flow by bookmark
  in another - the next unit - and the engine captures outputs, outcomes and journal. Connectors
  send the job's key as `Idempotency-Key`. The author writes one linear flow and one plain
  `ExecuteAsync`; a job whose unit fails is never run; jobs a dead process left are run again
  when the tenant activates (`RecoverAsync`). `Raise trigger` is *not* a boundary: the queue
  sends after commit and the flow needs no answer, so a hook attachment may raise one - the
  honest "call after commit" shape.
- **Atomicity is derived, never asserted.** Every activity, composites included, is
  *atomic* (no boundary inside, nested definitions walked) or *long-running*. Computed at
  publish by `WorkflowAtomicityAnalyzer`; a published composable flow re-validates what uses
  it. Boundaries: the engine's bookmark-creating activities (events, timers, delays, HTTP
  endpoints), connector activities, `Request approval`, the external platform activities
  (e-mail, SMS, notifications, social posts) - all of them waits now that calls are two-phase.
- **Two kinds of extension point.** **Hooks** run the attached flows *inline, inside the
  unit*, as child instances in the same scope and session; a required attachment's failure
  fails the unit. A slot is run either by a flow (`Hook` activity) or by a registry service
  from inside its own request through `IWorkflowHookRunner` (a module's created slot, run
  from its service), so the attachment runs whichever path created the object. Hooks
  accept only atomic attachments (refused at attach and at publish, with the boundary named
  and the event alternative suggested). An activity's Failed port inside an attachment is the
  activity's answer, not the unit's: the attachment says what it means with `Fail unit`.
  **Triggers** (`Raise trigger`, the registry triggers) fire after commit, as deferred shell
  tasks today and from the writer later, and their consumers run as their own units: for
  notifications, integrations, anything where a delay does not matter.
- **Hook slots and attachments are locked like flows.** A slot is declared by the owning
  module (`WorkflowHookSlotDescriptor`: key, object, whether tenants may attach, whether
  tenant attachments must be required, where they run). An attachment (slot, flow,
  position, required|best-effort) carries an ownership tier: system attachments ship with
  the module and are upgraded by code; tenant attachments are the tenant's within the slot's
  policy, behind the flow permissions and the attached flow's access lists.
- **What stays impossible, on purpose:** a hook that must call out *and* be in the
  transaction (a distributed transaction with someone else's API). The validator says so and
  names the two honest shapes: call after commit with compensation (`Void` answers `Post`),
  or call before the unit as a precondition.

The queue design that goes further (partitioned event delivery, a serial writer) is under
Still to build › Queues.

**Units and hooks, as built:** the engine stores flush instead of committing
(`FlushAsync`; the shell scope commits the burst), the trigger store treats a re-indexed
trigger as an update (delete-then-insert of one document in a session is refused by YesSql),
and the definition notifications (Publishing/Published, Retracting/Retracted) are raised
once, by the content handler, with Retracted carrying the definition as unpublished.
`WorkflowStimulusQueue` (Server/Units) collects every stimulus - registry triggers,
content events - and sends each in its own child scope after the scope's session
commits; nothing is sent when the unit failed or the commit threw. `WorkflowUnitOfWork`
(scoped; nested frames for hook attachments) and `UnitOfWorkCommitStateHandler` (wraps the
engine's commit handler: a failed unit cancels the document store and records the faulted
state through a child scope). `Hook` and `Fail unit` activities, `WorkflowHookService`
(system attachments from `IWorkflowHookAttachmentProvider`, tenant ones in a document),
`api/crest/workflows/hooks`, `WorkflowAtomicityAnalyzer` (used at attach and, through the
engine's validating notification, at publish), `IUnitBoundary` on the connector, approval
and external platform activities; the generic `flow.hook` slot. Check `workflows-units`.
Known limits, for the queue stage: the flows one stimulus starts share a unit (the serial
writer gives per-unit isolation); the approval decision resumes its flow inline in the
deciding request and an incoming webhook runs its flows in the request (both are their own
boundaries, so no unit is split, but neither is after-commit). Child scopes never activate
the shell (`activateShell: false`): during setup that path waits on the activation lock the
setting-up scope holds.

**Background calls, field activities, the created slot, as built:**
`WorkflowAfterCommit` (Server/Units) is the one after-commit hook the stimulus queue and the
background jobs share. `DurableBackgroundActivityScheduler` (replaces the engine's in-memory
`LocalBackgroundActivityScheduler` through `WorkflowRuntimeFeature.BackgroundActivityScheduler`),
`WorkflowBackgroundJob` + store + `BackgroundJobContext` (the running job, for the
idempotency key), `ShellScopedBackgroundActivityInvoker` (the engine's invoker with the
hand-back resumed directly; engine edit: `ResumeWorkflowAsync` virtual, `BuildResumeOptionsAsync`
factored out), recovery at tenant activation. `ConnectorActivityBase` and the
external platform activities (`Send email`, `Send SMS`, the notify activities, the social
posts) are `RunAsynchronously`;
`RaiseTrigger` is not an `IUnitBoundary`. `WorkflowHookRunner` (`IWorkflowHookRunner` in Abstractions) runs a slot from a
flow or a service; `WorkflowHookFailedException` → 409 through `WorkflowHookFailedExceptionFilter`
for any module's controller; a failed `WorkflowUnitOfWork` cancels its own session through a
before-dispose callback registered ahead of Orchard's commit, so a service's request discards
the same way a burst does. `ContentFieldValueCopier`, `CopyFields`, `MoveFields`;
Parties' `ResolveParty`. Found and fixed on the way: scheduled tasks
(timers, cron, delays) ran in a bare service scope where nothing after-commit could run -
`ShellScopedRunScheduledTaskHandler` replaces the engine's handler and gives each a shell
scope; and the engine persisted only *declared* workflow input across bursts, so a flow
resumed after a wait read a null payload - the vendored `WorkflowStateExtractor` now keeps
undeclared input too. Checks `workflows-fields`, `workflows-connectors`.

**What the engine already had, and what Crest adds.** Ruling: do not
duplicate engine infrastructure; extend it. The vendored engine has an in-process mediator
(`INotificationSender`/`ICommandSender`, `CommandStrategy.Background` onto an in-memory
channel), dispatchers over it (`IEventPublisher`, `IStimulusDispatcher`, `IWorkflowDispatcher`),
a durable bookmark queue (stimuli that arrive before their bookmark), background activities
(bookmark → out-of-band run → captured outputs → resume), commit strategies, a Polly-based
resilience feature, scheduling, distributed locks. None of it knows the host's transaction:
the engine saves its own state immediately and its background consumers run in bare service
scopes. Verdict: ownership, registry, hooks, units, field dependencies, approvals, connections
are new; the after-commit queue and `RegistryTrigger` are thin glue over `IStimulusSender`;
two things had been reinvented and were folded back - two-phase calls onto background
activities (above) and connector retries onto the resilience feature
(`ConnectionResilienceStrategy`, category Connectors, the connection's `RetryCount` as the
policy, default on connector nodes, attempts journaled by the engine; `ConnectorInvoker` makes
one attempt and marks transient failures). Engine gaps fixed on the way: scheduled tasks and
the background command/notification channels now run in shell scopes
(`ShellScopedRunScheduledTaskHandler`, `ShellScopedBackgroundConsumers` started at activation
- Orchard never starts a tenant container's hosted services, so the engine's background
channels had no consumer at all), the bookmark queue worker processes in a shell scope
(`ShellScopedBookmarkQueueWorker`), and `StimulusSenderOptions.QueueUnmatchedBroadcastStimuli`
(engine edit, default upstream behaviour) is off: a broadcast nobody listens to is no longer
written to the bookmark queue table on every content save. Also found: the engine's describer
never carried `[Activity(RunAsynchronously = true)]` onto the descriptor, so a declared
background task ran inline unless a node toggled it (engine edit in `ActivityDescriber`).

**The write side, as built:**
- `IWorkflowObjectLock` (Abstractions) over the engine's `IDistributedLockProvider`, keyed by
  object id, re-entrant within a unit (scoped): the owning registry service takes it around
  every write to an object and the after-commit send
  takes it around each correlated run, so no two units touching one object interleave.
  Version checks reduce to "read under the lock" for now; no serial writer was needed.
- Every stimulus carries a `StimulusId` input (the event side's idempotency key); the
  trigger journals it. `GET api/crest/workflows/pending?correlationId=` answers the pending
  state an object's page shows: running or waiting flows about it and their background jobs.

## Field dependencies

Contracts are **per-activity field dependencies**, not a record-level contract. An activity
declares each content field it reads or writes as `Part.Field` with a **required** marker
and a read/write marker - in code (`[FieldDependency]`, which is what the system activities
do) or from its bindings (`Copy fields`' mapping rows). At publish, `WorkflowFieldDependencyAnalyzer` resolves them against the tenant's
definitions and stamps **warnings** on the version (`Crest.FieldWarnings`; the part or
field is missing, or a required read is optional or conditionally visible in the tenant's
definition) - never a refusal; `GET api/crest/workflows/definitions/{id}/field-dependencies`
resolves on demand. At runtime a missing value for a **required** dependency ends the
activity on `Failed` naming the field; an optional one reads null. Declared dependencies
ride the engine's activity descriptors (`crest:fieldDependencies`) and the registry.

**Contracts are per-activity field dependencies, not a record-level contract.** An
activity declares each content field it reads or writes as a dependency on a field path
(`Part.Field`) with a **required marker per field**
and a read/write marker. `Server/Fields`, contracts in Abstractions: system activities
declare theirs in code with `[FieldDependency(path, Required, Writes, ContentType)]`;
configurable activities derive theirs from their bindings through `IFieldDependencySource`
(`Copy fields`' mapping rows: each source with its own marker, each target as a write). The
engine's input model is untouched - the dependency is a declaration beside the inputs, read
from literal bindings at publish. The mapping rows are a JSON input.

**Publish warns, runtime fails, and only for required fields.**
- At publish, `WorkflowFieldDependencyAnalyzer` (nested published definitions walked)
  resolves each dependency against the tenant's content definitions as they stand and
  `FieldDependencyPublishHandler` stamps the **warnings** on the version
  (`Crest.FieldWarnings` custom property; absent when clean), never a refusal: the part
  or field does not exist, or the activity requires a field the tenant's definition leaves
  optional or shows only under a Crest visibility condition. Written fields only have to
  exist. `GET api/crest/workflows/definitions/{id}/field-dependencies` resolves the latest
  version on demand - the "what this flow needs" panel. A tenant who wants a run to stop on
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
shipped content type definition** so tenants cannot make them optional or hidden. **The lock
mechanism lives in Crest**: the field/part-definition counterpart of
Crest's option-list locks (None|Tenant|Module, module locks unliftable in-tenant), tracked
in [content-items.md](content-items.md).

**Activities compose.** Sub-actions such as "write a journal entry" are activities of their
own, chained and nested like functions: `UsableAsActivity` makes a published definition a
palette entry (`WorkflowDefinitionActivity`, a `Composite`), so a posting flow is built from
smaller system flows; the C# `Composite` base stays for pieces that belong in code. The
ownership tiers apply to the pieces.

## Activities Crest adds

| Activity | What it does |
| --- | --- |
| `Registry trigger`, `Raise trigger` | start from / raise a registered trigger |
| `Hook`, `Fail unit` | run a slot's attachments inside the unit; fail the unit |
| `Require permission` | gate on an Orchard permission of the acting user |
| `Request approval` | park until a role member or permission holder decides |
| `Call connector`, `Poll connector` | call a tenant connection after commit; poll for changes |
| `Log` | write to the tenant's log |
| `Send email`, `Send SMS`, `Notify users`, `Notify content owner`, `Update X status`, `Send Meta conversion event`, the user and tenant activities | the platform modules' activities (§ Platform activities) |
| `Copy fields`, `Move fields` | copy field values between content items by typed mapping rows |
| `Create party role`, `Resolve party` | Parties |

Triggers, activities and shipped flows by Crest module:

| Module | Triggers raised | Activities | Shipped flows |
| --- | --- | --- | --- |
| Users | `user.created`, `.updated`, `.deleted`, `.enabled`, `.disabled`, `.confirmed`, `.logged-in`, `.logged-out` (event handlers and both login paths; correlation = user id) | `CreateUser`, `ValidateUser`, `AssignUserRole`, `UnassignUserRole`, `GetUsersByRole` | — |
| Email, Sms, Notifications, Twitter, Facebook, Tenants | — | `SendEmail`; `SendSms`; `NotifyUsers`, `NotifyContentOwner`; `UpdateTwitterStatus`; `SendMetaConversionEvent`; `CreateTenant`, `SetupTenant`, `EnableTenant`, `DisableTenant` | — |
| Parties | `party.role-created`, `.role-removed` (a content handler on every registered party type except the two base types; correlation = role id) | `CreatePartyRole` (registry key → role type, party from the payload, idempotent); `ResolveParty` (a role or base party id → RoleId, RoleContentType, RoleTypeKey, BasePartyId, BasePartyContentType: which item holds a field, nothing walks the graph implicitly) | — |
| Crest.Workflows | `flow.raised`; hook slot `flow.hook` | `Registry trigger`, `Raise trigger`, `Hook`, `Fail unit`, `Require permission`, `Request approval`, connector activities, `Log`, `Copy fields` / `Move fields` (Server/Contents: source and target item ids - the target defaults to the payload's ContentItemId or TransactionId - and a JSON mapping of `Part.Field → Part.Field` rows each with a `required` marker; `ContentFieldValueCopier` resolves both sides against the tenant's current definitions, copies a same-type pair's JSON whole, converts text ↔ numeric, refuses other pairs; a required source that is empty or missing ends on Failed with the field named, an optional one is skipped and listed in `Skipped`; Move clears the source; the written field is re-applied as a typed element so the item's own readers see it in the same unit) | — |

**Approvals** (`Server/Approvals`): `Request approval` records an `ApprovalTask` (YesSql,
indexed) for an Orchard role and/or permission and waits on a bookmark;
`api/crest/workflows/approvals` lists what the signed-in user may decide,
`{id}/decide` (approve|reject, comment) authorizes through role membership or Orchard
authorization for the permission (member ceilings apply), records the decision and resumes
the flow on Approved or Rejected with DecidedBy/Comment as outputs; 403 outside the role,
409 once decided. Crest page `/workflows/approvals`.

## Connectors

**Moving to Queries (ruling 2026-10-06).** The connection model, sealed secrets and tokens,
OAuth flow, HTTP invoker, token cache and rate limiting described here move into
`Crest.Queries`' connection system, and Workflows depends on Queries; the activities and
inbound webhook triggers stay here as consumers ([queries.md](queries.md) › Workflows on
Queries). Retries move with them: the connection system retries at the transport level and
connector activities default to no engine retry ([queries.md](queries.md) › Retries belong to
the connection system). The section below describes what is built today.

A **connection** is a tenant's handle on an external service: base URL, auth kind (`none`,
`api-key`, `basic`, `bearer`, `oauth2-client-credentials`, `oauth2-authorization-code`,
`hmac` for inbound webhooks), a sealed secret that never leaves the server, retry count,
rate limit and timeout (`api/crest/workflows/connections`). Calls resolve a relative path
under the base URL and refuse one that leaves it; private networks are refused unless the
shell configuration allows them. Retries are the engine's resilience feature with the
connection's policy as the strategy (`ConnectionResilienceStrategy`), attempts journaled.
An OpenAPI 3 document imports as a connection with one palette entry per operation
(`POST connections/import-openapi`); an authorization-code connection is authorized once
by a user at `GET connections/{key}/oauth/authorize` and refreshes its token itself.

`Server/Connectors`. The registry ships the generic `http` and `webhook` connectors; a module
ships a specific one for a service it knows. Tenant **connections** live in one Orchard
document (`WorkflowConnection`), secrets sealed with the tenant's data protection (purpose
`Crest.Workflows.Connections`) and never returned; auth kinds none / api-key / basic /
bearer / OAuth2 client credentials (token cached per connection version) / hmac
(`WorkflowConnectorAuthKinds`, settings in `WorkflowConnectionSettingKeys`). API
`api/crest/workflows/connections` (Manage connections, antiforgery); Crest page
`/workflows/connections`.

Activities: `Call connector` (Done/Failed; outputs status, body, parsed JSON, failure),
`Poll connector` (Changed/Unchanged/Failed; a hash per definition + node in the tenant
database, driven by the engine's Timer or Cron), `Webhook received` (anonymous
`api/crest/workflows/webhooks/{connection}/{hook}`, HMAC-SHA256 of the body required, 401
otherwise, 404 for unknown/non-hmac connections; payload Body/Query/ContentType; no acting
user).

Policy (`ConnectorHttpClient`, `ConnectorInvoker`, `ConnectorRateLimiters`,
`ConnectorTokenCache`): retries on network/timeout/408/429/5xx with backoff, per-connection
fixed-window rate limit without queue, response and webhook size caps, no automatic
redirects, paths cannot leave the base URL, and every connection is refused on
loopback/private/link-local/CGNAT addresses at connect time (after DNS) unless the shell
sets `CrestWorkflows:Connectors:AllowPrivateNetworks` (a host's dev setup does, for the
local test endpoint).

**OpenAPI import** (`POST connections/import-openapi`): the first server becomes the base
URL, the global security scheme the auth kind (http bearer/basic, apiKey header, oauth2
clientCredentials or authorizationCode with their URLs and scopes), every path operation an
entry stored on the connection (`settings.operations`) and shown by the registry as a
palette preset on `Call connector` (`connector.{connection}.{operationId}`:
connection, method and path preset; the flow binds the body). **OAuth2 authorization code**
(`oauth2-authorization-code`): `GET connections/{key}/oauth/authorize` redirects to the
provider with a sealed state (tenant, connection, user, issued-at), `oauth/callback`
exchanges the code and seals the token set on the connection; the invoker uses the access
token and refreshes it with the refresh token when expired; the API only says
`authorized` and when the token expires. Retries are the engine's resilience feature
(`ConnectionResilienceStrategy`, the connection's `RetryCount`).

## Designer and admin pages

**Admin surface.** A "Workflows" menu root (`WorkflowsAdminMenu`, replacing the stock entry)
with Definitions, Instances, Connections, Approvals; `WorkflowsRoutePermissionProvider`
gates the routes and marks the Studio routes browser-only (`ICrestWebAssemblyRouteProvider`).
Connections and Approvals are Crest pages; Definitions/Instances (and the editor and
instance viewer, on Studio's own routes `/workflows/definitions[/{id}/edit]`,
`/workflows/instances[/{id}/view]`) are the forked Studio.

**Studio loads on demand, in its own container.** Nothing of Studio is in the admin client's
startup download or its service container. `Crest.Workflows.BlazorWasm` (always loaded,
thin) has the pages, `StudioHost` (loads the lazy assemblies with `LazyAssemblyLoader`,
builds Studio's container through `Crest.Workflows.Studio.Host.StudioServices.BuildAsync`
by reflection, attaches it as a late container and renders the view) and
`StudioActivityNode`, the canvas node's always-registered stand-in
(`[CrestJSComponent]`). `Crest.Workflows.Studio.Host` (lazy) holds the container setup
(Studio core, shell/MudBlazor, workflows module, the engine backend at
`{tenant}/crest-workflows/api` behind `CrestCookieApiHandler` with Orchard's antiforgery
token, a cookie auth provider manager) and the views with MudBlazor's providers. The lazy
set (26 assemblies: Studio, MudBlazor + extensions, Monaco, Refit, Polly, FluentValidation,
Humanizer, Radzen, the engine API client) is listed in `crest-lazy-assemblies.txt`; the
host app's WASM build turns every module's `blazor-wasm` library into lazy pages
(`tools/Crest.LazyModules`, imported by the host's WASM entry project). Measured on a trimmed Release
publish (brotli): startup ≈ 5.2 MB, on demand 2.6 MB. **Browser-only** because under
InteractiveAuto a first visit runs in a server circuit where Studio's HTTP clients would
share scopes across users; the host prerenders a placeholder and reloads into WASM if an
in-app link reached it inside a circuit.

The move of the designer onto Crest components is under Still to build › Designer: the
transition onto Crest components.

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

Unit tests (`tests/Crest.Workflows.Tests`): the API gate branch by branch (grant
mapping, per-definition Run, 403 on refusal), ownership property round-trips and stamping,
the access handler's veto/admit/bypass rules, the system scope, the registry catalog and
publisher, the user snapshot, connector address policy, path containment and signatures,
the async-only disposable scan, the shipped-flow sync decision.

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

- Module: `Crest.Workflows` - `Abstractions` (contracts), `Server` (the
  module: `Registry`, `Security`, `Units`, `Hooks`, `Fields`, `Contents`, `Connectors`,
  `Approvals`, `Orchard`, `Stores`), `engine` (the vendored engine), `designer` (the forked
  Studio), `tests`.
- The consuming module's part, e.g. an ERP module's `Server/Workflows` (provider,
  activities), `Server/Ledger`, `Server/Data/workflows/*.json` (shipped flows).
- Admin pages: Workflows › Definitions, Instances, Connections, Approvals, under the Crest
  admin; the designer opens from a definition.

## Operations: one registry, one request path, four pipelines, one access machinery

**Status (2026-10-10): steps 1, 2 and 4 landed in their first form; 3 is half built; 5–8 are open.**
Merged from the former operations.md. The backbone of the application: how
every read and write in Crest, from a UI button, a picker, a content list, a Liquid slot, a
workflow step, a REST, GraphQL or RPC call, a webhook or an external connector, goes through
one system. This section is the task list; the designs it builds on are
[queries.md](queries.md) (the query pipeline and the connection system), the sections above
(the action pipelines: units of work, queues, hooks),
[parties.md › Members](parties.md#members-persons-who-act-for-an-organization) and
[shells-and-themes.md › The member portal](shells-and-themes.md#the-member-portal-login-sessions-sides) (the caller's side and organization) and
[blazor-display.md](blazor-display.md) (what binds to operations).

Priorities, in order: speed, runtime adaptability, ease of maintenance.

### Rulings (2026-10-10)

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

### Why there are nine surfaces today

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
`IStartupFilter`s (this is where `BlazorAdminThemeMiddleware`, the shell selector, runs:
it classifies admin, login and member paths and calls `IAccessGate`, which authenticates
once, stamps the side and builds the caller, then the selector runs the route permission
check, stamps the bucket and shifts `PathBase`) → `UseRouting()` → the module startups by
`ConfigureOrder` (`AccessGateMiddleware` at −150: the request-handler schemes, then the gate
for whatever the selector left; the workflows engine API branch at −151, which maps the
caller onto engine grants and authenticates nothing; request localization at −100;
Crest.Server's endpoints, CORS and `UseAuthorization` in the default group) →
`UseEndpoints`. As of 2026-10-10 steps 1 to 4 run once each (the platform's
`UseAuthentication` is gone); background work enters through a synthetic `HttpContext`
flagged `IsBackground` and runs as the system caller its entry point sets.

#### Caller lifetime

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

#### The member caller

Two kinds of signed-in people share one tenant and one user store: **staff** (tenant users)
and **members** (organization-bound users, [parties.md › Members](parties.md#members-persons-who-act-for-an-organization)).
Same user management, same roles and permissions, same hierarchy; what differs is the
side they act on and the organization they act for, and that is the caller's business:

| | Staff side | Member side |
| --- | --- | --- |
| Managed by | tenant admins | tenant admins and the organization's member admin, subtree-scoped |
| Roles | the user's tenant roles | the active binding's role templates only |
| Data scope | tenant-wide per role and hierarchy | one organization, fail-closed, hierarchy within it |
| Signs in at | the admin login | the member portal only ([shells-and-themes.md](shells-and-themes.md#the-member-portal-login-sessions-sides)) |
| Tenant SSO | eligible | excluded by ruling |

**The class is a property on the user record, never a role** (`CrestUserClass` aspect,
indexed by `UserClassIndex`, stamped in `IUserEventHandler.CreatingAsync` so it persists in
the same write). Roles are what admins grant and revoke daily; the class decides which login
surface accepts the account at all, so it must not be grantable: a mis-granted member role
would lock staff out, a removed one would let a member into the staff login. **Conversion**
is its own permission-gated action, never the role editor, bumping the security stamp so
sessions re-establish on the right surface; a member hired onto staff keeps their account,
credentials and external logins, and the event is audited.

**The class permission ceiling.** Some permissions are never valid on the member side,
tenant settings being the canonical example, even when a super user assigns a role that
carries them. Modules declare their staff-only permissions beside their normal
`IPermissionProvider`; the ceiling (`MemberClassCeiling`, a final denial in the one
decision, Decisions › step 1) fails a ceilinged permission for a member-side caller and
beats every other success, closed over the dynamic expansions (`Publish_{Type}` and the
per-role user-management variants), with the admin role resolved through
`ISystemRoleProvider`, never hard-coded. The role editor's effective-permissions preview
goes through the same decision, so ceilinged permissions grey out there for free; UI
warnings are politeness, the ceiling is enforced at decision time.

**Permissions are tenant-governed.** Every module contributes its permissions through
`IPermissionProvider`, member-portal abilities included, so they appear in the tenant's
ordinary role editor; organization scope and hierarchy expansion **narrow** what a granted
permission reaches (the scope providers), they never grant. Permission says "may manage
members"; scope says "of this organization's subtree". Organizations choose people, tenants
choose powers.

**What the contributor adds** (`MemberCallerContributor`, landed 2026-10-10): the
organization (from `X-Org`, else the session's last choice) validated against the
bindings, the binding's roles only on the member side, the class, the impersonator, and
the organization system actor's member-admin roles. The staff role and permission claims a
dual user carries never reach the member side. Superseded and removed with it: the
per-request cookie enrichment (`MemberCookieEventsConfiguration`), the claims-based
`MemberPermissionCeilingHandler`, and the sign-in-time snapshot of roles into the cookie.

**Access policies** (ruling 2026-10-05): a downstream module may affect a member's access
without Members knowing why. The caller builder consults registered access policies at
portal sign-in, organization switch and refresh, and a policy may refuse portal access for
that organization or narrow the member's permissions there; a subscription module uses it
so an unpaid membership loses access. Policy verdicts are part of the cached caller state
and bump the permission version when they change. Still to build as a registered seam.

### The four pipelines

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

### Tasks

Each step compiles and runs on its own. Pre-release rules apply: no compatibility code, dev
tenants reset ([architecture.md](architecture.md)). The order is the dependency order.

#### 1. `Crest.Access`: the access machinery

**Landed 2026-10-10** (`src/Crest.Access.Abstractions`, `src/Crest.Access`): the caller built
per request by `CallerContextFactory` from the user's roles and the roles' permission claims
with the Anonymous and Authenticated roles, the super user, and the contributors
(`MemberCallerContributor`: class, organization from `X-Org` or the session, binding roles
only on the member side, impersonator, the organization system actor's member-admin roles);
`PermissionVersionDocument` bumped by role and user events; `CallerStateCache` keyed under
it; `AccessDecisionService` (ceilings as final denials, super user, `IResourcePermissionMapper`
with `ContentResourcePermissionMapper` for items and types, implied chains) behind
`AccessAuthorizationHandler`, which replaced `PermissionHandler`, `RolesPermissionsHandler`,
`SuperUserHandler`, `ContentTypeAuthorizationHandler` and `MemberPermissionCeilingHandler`;
`ScopeSetProvider` over `ContentItemScopeProvider` (Contents), `AssignmentScopeProvider` and
`OrganizationScopeProvider` (Server, with `CrestOrganizationPart` and its index), compiled to
YesSql predicates by `Crest.Data.Scoping.ScopeExpressions` and applied by the admin content
lists, GraphQL, the content-items API and the pickers (the picker scope resolver and
`OptionSourceScope` are gone); `AccessAuditor` on the audit trail's `Access` category;
`AccessRunner` and the system caller for scheduled tasks; the gate in the shell middleware
(every request authenticates once, cookie or `Api` scheme, `X-Shell` required on
cookie-authenticated API calls, Site stamped as a side); the clients send `X-Shell`/`X-Org`
(`CrestShellContext`), the loopback client forwards them. Not yet: the analyzer, the
conformance suite, the circuit-lifetime rules, the user picker's scope, read logging as a tenant setting
(`AccessAuditOptions.LogReads` is an option for now).

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

- [x] **`ICallerContext`** built per request at step 4 from the authenticated identity and
  the server-side state, not from baked cookie claims: tenant, side (from the bucket; API
  calls confirm with `X-Shell`, member side carries `X-Org`, disagreement denied), organization
  (from `MemberOrgBindingIndex`), roles and their permissions (from `RoleStore`'s document),
  class ceiling, policy verdicts, culture. System and anonymous callers. Replaces the members
  cookie-enrichment plan.
- [x] **The permission version**: a per-tenant counter in a document, bumped by
  `IRoleUpdatedEventHandler`, binding writes and policy writes; carried by every cached
  state entry and compared on use; the SignalR nudge becomes its client notification. The
  cookie keeps identity only; the security stamp stays for sign-out and credential change.
- [x] **The server-side state cache** per (user, tenant, side, organization) under that
  version; the client's session copy ([shells-and-themes.md](shells-and-themes.md#the-member-portal-login-sessions-sides)) is fed from it and is never authority.
- [x] **`IAccessDecision`** as the one implementation the platform's `IAuthorizationService`
  delegates to: the handlers above collapse into it (baked-claim lookup → caller roles;
  super user; Anonymous and Authenticated roles; the per-type dynamic permissions; the member
  ceiling; the access policies), answering allow, deny or deny-as-not-found. Existing
  `[Authorize]` and `AuthorizeAsync` callers keep working through it during the migration.
- [x] **`IScopeProvider`** per table or content type, registered by its owner, and
  **`ScopeSet`** compiled per scope signature and provider versions, as SQL fragments and as
  in-memory filters. First providers, lifted from the four places above: content items by
  type (view-any / view-own with owner), assignments (`CrestAssignmentIndex`, narrowing
  only, "no requirements" ≠ "nothing matched"), organizations and bindings, media access.
  `OptionSourceScopeResolver`, `DefaultContentsAdminListFilterProvider` and the GraphQL
  filters are deleted once the providers exist.
- [x] **Organization scope on data** (decision needed, below): the part or index that says
  which organization a content item belongs to, so a provider can scope by it.
- [x] **A target with no scope provider is refused**, never served unfiltered.
- [x] **`IAccessAuditor`**: every decision and execution, reads included. `AuditTrailManager`
  writes through the request's scoped session, so a denial on an exception path would roll
  back with it, and each event costs a document row, an index row and a site-settings read.
  The auditor therefore writes decisions in a child scope that commits on its own, and read
  executions through a batched writer into their own collection (decision needed on volume).
  Categories register the existing way (`IConfigureOptions<AuditTrailOptions>`).
- [x] **The `AccessGate` middleware chain** as the tenant startup filter that
  `BlazorAdminThemeMiddleware` is today: it becomes the shell selector (all three buckets
  stamped, Site included) **and owns authentication** for every request (cookie or `Api`
  scheme per request kind, once), then builds the caller; `UseAuthentication` at −150 and
  the engine API's own authentication branch go. The four pipelines and the renderer take a
  `ScopedExecution`, never a raw session or content manager.
- [x] **Background entry points enter the path**: a `RunAs(caller)` helper replaces the empty
  principal in `ModularBackgroundService`'s synthetic context and in the workflows'
  shell-scoped consumers, task handler and bookmark worker; the system caller is explicit
  there, and it is the **only** way a system caller comes to exist: built in process, never
  from a credential (Decisions › System actors). The platform's `IsBackground` shortcut around the tenant pipeline is reviewed so the
  gate still runs.
- [x] **The `Api` scheme for machines** (gate side landed 2026-10-10): the gate
  authenticates `Api` for every request with an `Authorization` header; the forwarder takes
  additional credential schemes (the remote deployment key is the first); a principal with
  no user record is an `application` caller. Still open: OpenId validation enabled by the
  host recipe (machine-actors.md).
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
- [x] **Two projects**: `Crest.Access.Abstractions` (contracts and `ScopeSet`) below
  `Crest.Data`, `Crest.Access` (caller builder, decision, cache, gate) beside `Crest.Queries`.
  Confirmed 2026-10-10 as a module that stays; the audit, defects, the one-calculation
  hand-down rule and the task list are [access.md](access.md).

#### 2. The query pipeline and the connection system ([queries.md](queries.md))

**Landed 2026-10-10** (`src/Crest.Queries*`): the structured query model (a JSON step list:
from, join, filter, project, sort, page; declared parameters; output columns derived from the
index types) compiled through YesSql's dialect builder into parameterised SQL with the
caller's `ScopeSet` conjoined for every table (the `Structured` source); the SQL source
parses templates as written (Liquid rendering gone, `{{`/`{%` rejected at validation) and the
AST rewriter conjoins the scope for every table, subquery, CTE body and set-operation branch,
refusing a table with no rule; the source contract carries cancellation, page tokens, totals
and typed columns through every caller; the `System` source serves `item`, `user` and `site`;
`QueryDescriptor` and `IQueryCatalog` describe every saved and built-in query, and the
workflow engine registers them as `RunQuery` activities. Every run pages (default 100, cap
500). Not yet: statement compilation caching per query version and scope signature,
`ReturnContentItems` through the gate, the read job, the connection system (connectors still
in Workflows), the builder.

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

- [x] **Parameters only, never templating.** SQL templates stop being Liquid-rendered;
  parameters bind as parameters. A Liquid-rendered statement is an injection surface and
  defeats the scope rewrite.
- [x] **The source contract**: typed columns (from the reader's schema and the builder's type
  cache), page tokens, totals where available, cancellation, the ScopeSet applied before
  paging, a stable order added when none.
- [x] **Scope injection in `PlatformSqlRewriter.VisitSelect`**: for every scoped table or
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
- [x] **The system sources**: item by route parameter, current principal, site settings;
  in-process, memoised per request (what blazor-display.md's slots need first).
- [ ] **The read job**: the asynchronous read pipeline on the durable background machinery;
  streaming through `DbDataReader`.
- [ ] **The connection system**: the model, sealed secrets, OAuth, retries with
  `Retry-After`, rate limits, the SSRF guard, moved down from Workflows; protocol features
  (`Sql`, `Rest`, `Soap`, `Rpc`); search engines as connections (Lucene and Elasticsearch
  sources become connections, their APIs retired).
- [ ] The rest of queries.md's list: the builder, column types by aspect, export, the
  plugin registry.

#### 3. The registry

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

- [x] **Query descriptors are engine `ActivityDescriptor`s** emitted by a Crest
  `IActivityProvider` (landed 2026-10-10: `QueryActivityProvider` over `IQueryCatalog`,
  `Server/Queries/RunQuery.cs`): one per saved query (from content) and one per code query (from
  attributes), with the query's parameters as `InputDescriptor`s, its columns as typed
  `OutputDescriptor`s, and the output schema (pageable, total, column types) in
  `CustomProperties` under a Crest key, the way field dependencies are carried. This gives
  the palette, `/descriptors/activities`, the studio's hint handlers and the options endpoint
  for free. The descriptor's `ClrType` is the one run-query activity.
- [x] **The run-query activity** (`RunQuery`, landed 2026-10-10): executes its descriptor's query through the query pipeline
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

#### 4. The action pipelines ([workflows.md](workflows.md))

What exists (survey 2026-10-10, `src/Crest.Workflows`): the engine runs the default
pipelines (`EngineFeature.cs:38`; workflow: heartbeat, engine exception handling,
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

- [x] **The `AccessGate` as engine middleware.** Landed 2026-10-10: `Server/Security/AccessGate.cs`
  (`WorkflowAccessGateMiddleware` at index 0, `ActivityAccessGateMiddleware` before the
  terminal invoker, both inserted in `CoreStartup.AddCrestWorkflows`). Register through
  `AddCrestWorkflows(...)` (`CoreStartup.cs:73`) with `WithWorkflowExecutionPipeline` /
  `WithActivityExecutionPipeline`: a workflow-level gate inserted after `Reset` (the run's
  actor and decision, once per burst) and an activity-level gate inserted before the terminal
  invoker (decision and scope for each data activity). Keep the engine's default middleware
  list otherwise; `pipeline.Insert` rather than a rebuilt list, so engine updates do not
  silently drop it.
- [x] **The actor rule.** Landed 2026-10-10: identity-only `WorkflowUserContext`, callers rebuilt
  per burst by `WorkflowCallerResolver`; `Crest.RunsAsSystem` takes ManageShippedWorkflows at
  publish (`WorkflowOwnershipGuard`). `WorkflowUserContext` (`Server/Contexts`) today captures **every
  claim** of the principal into the instance input (`InputKeys.Actor`) and rebuilds a
  principal with `ToPrincipal()` for `WorkflowAuthorizer`. Change it to identity and shell
  only (user id, user name, tenant, authentication type); the gate builds the caller from
  that identity at every burst through `Crest.Access`, and `WorkflowAuthorizer` and
  `RequirePermission` ask `IAccessDecision` instead of a rebuilt principal. The five capture
  sites (`PlatformWorkflowManager`, `WorkflowHookRunner`, `WorkflowTriggerPublisher`,
  `ContentEventHandler`, the webhook controller) keep capturing, with the reduced shape.
  Published-as-system: an explicit definition property, permissioned
  (`ManageShippedWorkflows`), that makes the gate build the system caller instead.
- [x] **Actors where none exists today.** Landed 2026-10-10: the scheduled-task handler, the
  bookmark worker and the background-job runner run as the resolved caller through
  `IAccessRunner`; the consumers leave it to the gate per burst. Background scopes have no `HttpContext`, so
  `IWorkflowUserContextAccessor.Capture()` there yields Anonymous: timer, cron and delay
  runs, the bookmark queue and the background consumers. Rule: a resume takes the actor from
  the instance input; a timer- or cron-started flow runs as the system actor, declared on
  the definition; a flow with neither is refused by the gate.
- [x] **HTTP-endpoint workflows through the request path.** Landed 2026-10-10:
  `IHttpWorkflowInputContributor` carries the request's caller as the actor;
  `HttpWorkflowEndpointAuthorizationHandler` asks the decision for Run plus the Run list. `HttpWorkflowsMiddleware`
  (`engine/.../Crest.Workflows.Http`, mounted by `Server/Features/HttpStartup.cs` under the
  `CrestWorkflows:Http` base path, default `/workflows`) runs the flow inline in the request
  with its own `IHttpEndpointAuthorizationHandler` and captures **no** actor. It becomes a
  dispatch target of step 7 of the path: the gate has run, the caller is the request's, the
  trigger's `Authorize`/`Policy` inputs are replaced by the operation's decision, and the run
  is the request's unit of work. The base path becomes a generated-surface route prefix.
- [x] **Data activities through the gate.** Landed 2026-10-10: `[RequiresPermission]` on the
  field and connector activities (`UseConnections`), the run-query activity and its
  provider (`Server/Queries`); the content operations and the rewrite of CopyFields/MoveFields
  onto them are still open. The run-query activity and the content
  operations (step 3) are the only data activities; `Contents/FieldActivities.cs`
  (`CopyFields`, `MoveFields`, which call `IContentManager` directly) are rewritten on content
  operations; `CallConnector`/`PollConnector` go through the connection system.
- [x] **Name and host the two action pipelines as such**: named 2026-10-10 in
  `Server/Units/ActionPipelines.cs`; the queue hardening (partition, idempotency keys) is
  still open under workflows.md › Queues. The unit of work and durable
  background become the two named hosts in `CoreStartup`, with the queue hardening from
  workflows.md › Queues (partition by correlation id, idempotency keys; after-commit emission
  is already `WorkflowAfterCommit`). Anything that must be after-commit uses it, never
  `WorkflowStateCommitted`.
- [x] **The engine API gate becomes a consumer of `IAccessDecision`.** Landed 2026-10-10:
  the caller comes from the request path's gate (none → 403), every check is the decision,
  the Run list is consulted beside it; the branch's own authentication went with the one
  gate (2026-10-10, access.md § 3).
  `ApiSecurityMiddleware` (`Server/Security`) maps Crest permissions to engine
  claims and enforces the run gate per definition; it keeps the mapping but asks the one
  decision, and sits after the request path's steps 1–6 rather than carrying its own
  authentication branch.
- [ ] **Connectors into the connection system** (queries.md › Workflows on Queries): the
  model, sealed secrets, OAuth, `ConnectorHttp` (SSRF guard, redirects off, size limits),
  rate limiters, token cache and `ConnectionResilienceStrategy` move down; the activities
  stay as consumers; the webhook controller verifies through a narrow verifier.
- [x] **The stock activities ported to `Input<T>`** and the stock module deleted. Landed
  2026-10-10: § Platform activities; the stock Liquid and JavaScript evaluators went with it.
- [ ] **Expressions**: the engine's Liquid handler replaced by the platform's parser and
  context; one Fluid version (2.40 platform, 2.31 engine today); Jint and Fluid limits
  configured in one place (blazor-display.md § 12).

#### 5. Move the reads

- [ ] `api/crest` lists (content items, content types, groups, navigation, menus, media,
  users, roles, tenants, features) re-expressed as query operations, module by module.
- [ ] **The pickers**: the option-sources scope resolver's logic becomes scope providers; the
  controller becomes the generated surface of the queries each picker binds; the user
  provider gets scope.
- [ ] Members and Parties reads; the member binding routes check the caller's organization
  through scope, not a global permission.
- [ ] This is the window where two paths exist; one module per change keeps it short.

#### 6. Move the writes

- [ ] Content create, update, delete, publish as content operations.
- [ ] Members and Parties writes as actions.
- [ ] Machine clients: the `Api` scheme and bearer tokens on the generated routes
  ([machine-actors.md](machine-actors.md)); the gate sees a machine caller.

#### 7. Generate the surfaces

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
- [ ] The hand-written `api/crest/*` controllers as each generated route lands.

### The surfaces today, and their fate

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

### Decisions needed

- [x] **Organization scope on data.** Ruled (2026-10-10): **both.** An organization part
  with an index (`OrganizationId`) that every scoped type carries, filled by the owning
  module, is the row's owner; assignments (`CrestAssignmentPart` with an organization
  target) are grants. The hierarchy's subtree scoping ([parties.md](parties.md) › Relational hierarchy) joins on the owner column.
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
  abstractions split below. Confirmed the same day ([access.md](access.md)): the module
  stays, and the old pipeline's remains fold into it. The chain is core → data →
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
- [ ] **Does the ceiling also ceiling staff-only permissions in reverse?** Probably
  unnecessary: staff are trusted with member-portal surfaces through impersonation.
- [ ] **The ceiling keys on the side, never on the user** (the security-critical rule of
  the dual-account ruling): a dual user keeps every staff permission on the admin side and
  is ceilinged on the member side. Landed as `MemberClassCeiling` reading the caller's side;
  the conformance suite's first fixtures cover it.
- [ ] **Per-part permissions** ([permissions.md](permissions.md)) as scope providers on
  content fields: the seam in step 1, the first rule when a type needs it.
- [ ] **What OrchardCore offers natively for connections** (carried from queries.md), to be
  researched before step 2 designs the connection model.
- [ ] **The access machinery can run on separate hardware** (2026-10-06, later). About half
  of all queries are permission work: building each request's caller for its side and
  organization, the policy verdicts, the hierarchy's "everyone under me", the file access
  index. `Crest.Access` therefore gets an API that can be deployed apart from the web
  servers, the same service in-process for a small host and a separate service on its own
  hardware for a large one, without callers changing. Bears on [parties.md](parties.md)
  (the hierarchy) and the caller state cache, [media.md](media.md) (the access index),
  [queries.md](queries.md) (the scope inside queries) and
  [shells-and-themes.md](shells-and-themes.md) (shells on their own servers).

## Still to build

The workflow service, its registry, connectors, approvals, ownership, permissions, units
of work, hooks, field dependencies and the platform activities are built: see the
sections above. This section holds what is designed but not built, the rulings that shape
the design, and the open work.

### Rulings

- **Workflows is the fifth core registry, owned by Crest.**
- **One engine, no stock module** (2026-10-10): the stock OrchardCore workflow module's
  activities were ported to engine activities in their own modules, its events became
  registry triggers, and the module, its bridge and its contracts were deleted. Supersedes
  the override ruling below.
- **Elsa is the Orchard workflow service, by override** (2026-09-27, superseded 2026-10-10): depend on the stock
  feature, re-register its services last. Rejected: stock engine + Studio over an adapter
  (no versions, no typed model, a copy of the non-virtual manager); two live engines (two
  palettes, duplicated events, two instance stores).
- **Elsa is vendored source, not a package** (2026-09-28); upstream is a diff source.
- **The designer transitions to Crest components, last** (2026-09-28 / 2026-09-29); Studio
  on MudBlazor in the Crest admin is acceptable until the backend is complete. Studio loads
  on demand in its own container (2026-09-29, option 2).
- **The security token stays until the UI merge** (2026-09-30).
- **Three ownership tiers; permissions integrated with Orchard's grant system, not a
  separate ACL; extension points are triggers** (2026-09-30).
- **Posting is workflows; per-activity field dependencies with a required marker; publish
  warns, runtime fails for required fields; system fields locked, the lock in Crest;
  activities compose; measure before compiling** (2026-09-30).
- **Trigger payloads are ids and scalars only**; activities re-read through the registries.
- **Extend the engine, never duplicate it** (2026-10-01, audit): background activities, the
  resilience feature, the bookmark queue and the mediator are the infrastructure; Crest
  adds the transaction awareness and durability they lack.

### Queues

Built today: see [docs/workflows.md › Units of work](workflows.md#units-of-work-how-a-run-commits).

- [x] **Merge the stock workflows module into Crest.Workflows** (ruling 2026-10-09, done with
  the platform rename). The stock module is gone. Its contract, which platform modules
  implement, is the library `Crest.Workflows.Abstractions` (activity base classes,
  `AddActivity`, `IActivityLibrary`, the expression and script evaluator interfaces, and
  `ManageWorkflows`). What the engine runs it with lives in Crest.Workflows under
  `Server/Platform/Runtime` (`PlatformActivitiesStartup`): the activity library, the Liquid and
  JavaScript evaluators, the execution-context handlers, and three general activities (Notify,
  Log, HTTP request). The stock engine, stores, indexes, admin UI, designer, deployment,
  trimming, user tasks, timers, HTTP events and control-flow activities were deleted, because
  the engine has its own. Platform modules' workflow startups now require the `Crest.Workflows`
  feature. Still to do: the platform modules' activity display drivers and views (for the old
  designer) are unused.
- [ ] **Two queues, two guarantees (the full way; designed, built after the above, on engine
  parts - see the audit).** The *write side*: no two units touching one object interleave -
  first through the engine's `IDistributedLockProvider` keyed by correlation id around a
  unit, with version checks on what the unit read and a re-run on conflict; a serial writer
  applying working sets only if that is measured insufficient. The *event side*: the engine's
  stimulus dispatch and bookmark queue, hardened - emitted after commit (built), partitioned
  by correlation id (one consumer per object at a time, in order), payloads carrying the
  object version the unit committed, at-least-once delivery with idempotency keys (unit id +
  slot) for exactly-once effect. Costs accepted: read-your-writes latency with a pending state
  in the UI, optimistic conflicts re-run.

  The planned queues follow the
  same ruling: the event queue is the engine's stimulus dispatch + bookmark queue, hardened;
  per-object serialization is tried first with the engine's `IDistributedLockProvider` keyed by
  correlation id, a serial writer only if measurement says so.
  - [ ] **Units:** a serial writer and version checks beyond the object lock only if a measured
    conflict rate asks for them.

### Designer: the transition onto Crest components

Built today: see [docs/workflows.md › Designer and admin pages](workflows.md#designer-and-admin-pages).

- [ ] **The transition onto Crest components** (ruling 2026-09-28; deferred to last, ruling
  2026-09-29): Crest's UI layer is its own fork over Radzen (`Crest*` components, theme
  tokens, localization), and the Studio fork must end up on it so the designer is themed,
  localized and tested like every other Crest page. What stays foreign: the canvas (X6, DOM
  interop) and Monaco. Studio is on Radzen 11 already; what remains is MudBlazor. Inventory
  (2026-09-29): 98 Razor files, ~800 usages, every one with a Crest counterpart - MudStack 70
  → CrestStack; MudText 53 → CrestText; MudTable family ~120 → CrestDataGrid/CrestTable;
  MudMenu family 62 → CrestMenu/CrestContextMenu/CrestSplitButton; MudTabs 44 → CrestTabs;
  MudButton family ~73 → CrestButton/CrestToggleButton; MudSelect family ~80 → CrestDropDown;
  MudTextField family ~51 → CrestTextBox/CrestTextArea/CrestFormField; MudTooltip 30;
  MudAlert 19; MudDivider/Spacer/Paper/Container ~46 → markup + CrestCard; MudDialog 12 →
  CrestDialog; MudCheckBox/Switch/Radio ~16; MudForm 7 → CrestTemplateForm; MudChip ~6;
  MudBadge 4; MudFileUpload 3 → CrestUpload; date/time pickers 4; MudTreeView 2 → CrestTree;
  MudTimeline 2; MudExpansionPanels 4 → CrestAccordion (the palette); MudProgressLinear;
  ISnackbar → Crest notifications. Order: the shared UIHints (20 files) and Shared components
  (17) first - the property panel every view uses - then the editor, then list/instance
  viewers; MudThemeProvider last (until then the Mud theme reads Crest tokens).
  `workflows-designer` stays green throughout; the security token consolidation
  (`ICrestAntiforgery`) happens with this merge.
  - [ ] The security token consolidation (`ICrestAntiforgery`).
  The workflows pages are built on the Elsa designer UI; what it offers must not be lost in
  the move (ruling 2026-10-05).
- [ ] **Hook attachment and hook insight** (ruling 2026-10-05: planned, not built).
  - [ ] A hook-attachment page (the API exists: `HooksController`).
  - [ ] Clear, labelled sections for **webhooks** — pub/sub insight, process instances and
    history — and for **internal hooks**, with the same insight data.
- [ ] **Run history and insight through the audit system** (ruling 2026-10-06). The engine's
  own journal (execution logs, activity records), hook runs and inbound webhook deliveries
  — including rejected posts, which today are only logged — are recorded as audit events;
  the insight sections and metrics are audit feeds ([audit.md](audit.md)). Whether that
  needs a change to AuditTrail's save path is open there.
- [ ] **Per-process metrics** (ruling 2026-10-05): run counts, timings and failures per flow,
  on the workflow pages, over the already-indexed instances, execution logs and activity
  records.
- [ ] **The designer UI that comes with it.** With it: the tier badge, Outdated + Reset, the access-list editor, the field-dependency
  panel, the pending state, an OAuth "Authorize" button on a connection, and a dedicated
  mapping editor for `Copy fields` (until then the rows are a JSON input).
  - [ ] The tier badge.
  - [ ] Outdated + Reset.
  - [ ] The access-list editor.
  - [ ] The field-dependency panel.
  - [ ] The pending state.
  - [ ] An OAuth "Authorize" button on a connection.
  - [ ] A dedicated mapping editor for `Copy fields`.

### Elsewhere

- [ ] **Machine actors** ([machine-actors.md](machine-actors.md)).

### Decisions needed

- [ ] **Design a flow's output contract.** **Still to design:** a flow's *output* contract (what a composable flow promises its
  caller), and version checks beyond the object lock should a conflict rate ever ask for
  them.
- [ ] **Background throughput (W5).** Connector background jobs run on **one worker per
  tenant**, so one slow external call blocks that tenant's other background work; the lock
  that keeps two units of work off one object is **file-based**, so it works on one server
  only; poll state is **one document per tenant** updated without a lock, so two polls at
  once can lose an update. Options: (1) fix the poll-state race now (a record per
  connection, or a lock) and decide the worker count and a database-backed distributed lock
  when there is a second server or real traffic; (2) decide everything now; (3) leave all of
  it until measured. Recommendation: 1; the race is a bug, the rest is capacity, decided
  when measured.
- [ ] **Moving the designer onto Crest components (W6).** Ruled to happen last, keeping
  what the Elsa designer offers; the canvas and the code editor stay foreign. 104 of 119
  designer files use MudBlazor; Studio ties editing to the publish link (without it, edit
  is read-only); MudBlazor loads only when the designer opens (about 2.6 MB), so the cost
  of keeping it is two UI stacks and a designer not themed, localized or tested like the
  rest, not startup speed. Options: (1) staged, as planned above (shared pieces first, then
  the editor, then the list and instance viewers; MudBlazor's theme reads Crest's tokens
  until it is gone); (2) keep MudBlazor permanently, themed from Crest's tokens, and build
  only new designer pages on Crest components; (3) rewrite from scratch reusing only the
  canvas. Recommendation: 1, still last; the new pages (hook attachment, hook and webhook
  insight, per-process metrics) are built on Crest components now, independent of the
  move; the edit-without-publish fix comes in the first stage; if the move stalls, it stops
  at option 2.
