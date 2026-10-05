# Workflows

How Crest's workflow service works: what a workflow is here, who may do what to one, how
a flow gets started, how a run commits (or does not), and how a downstream module builds on
top of it. Design not yet built, and the rulings, are in [workflows.md](workflows.md);
the flowchart of a run's life is [workflows.mmd](workflows.mmd).

## What it is

`Crest.Workflows` is the tenant's workflow service. It is a vendored fork of Elsa 3 (the
engine: activities, flowcharts, bookmarks, bursts, the designer) installed as the Orchard
workflow service by override: the stock `OrchardCore.Workflows` feature stays enabled so
every Orchard module's events and tasks keep registering, and Crest replaces the services
behind it. The engine's own HTTP API lives at `crest-workflows/api` behind the tenant
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
contracts in `Crest.Workflows.Domain`, and never reference the engine module. The
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
 Crest.Workflows  (the fifth core registry; depends on feature "OrchardCore.Workflows", overrides its services)
 ├─ engine/   vendored elsa-core 3.6.0 (22 projects, renamed Crest.Workflows.*)
 ├─ Server/   the Orchard integration: stores on YesSql per shell, definitions as content items,
 │            the API gate, the registry, ownership, access, connectors, approvals, stock adapters
 ├─ Contents/ content triggers and tasks (feature Crest.Workflows.Contents)
 ├─ Domain/   the contracts modules bind to (descriptors, IWorkflowTriggerPublisher, constants)
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
| Contracts modules bind to: `WorkflowTriggerDescriptor`, `WorkflowFlowDescriptor` (+ `WorkflowOwnership`), `WorkflowActivityDescriptor`, `WorkflowConnectorDescriptor`, the provider interfaces, `IWorkflowTriggerPublisher`, `WorkflowsConstants` (routes, permissions, roles, property keys) | `Domain/` |
| Engine wiring, stores, publisher, API gate, permissions, ownership guard, access handler and API, registry catalog/importer/controller, triggers, stock adapters, connectors, approvals, navigation | `Server/` |
| Content triggers and tasks | `Contents/` |
| Admin pages, `StudioHost`, the canvas node stand-in, `crest-lazy-assemblies.txt` | `blazor-wasm/` (always loaded, thin) |
| Studio's own container (`StudioServices.BuildAsync`), cookie API handler, views | `studio-host/` (lazy) |
| Activities on core objects | the module that owns the object (Parties: CreatePartyRole, ResolveParty) |
| Triggers a registry raises | the owning module's service or content handler |
| Shipped flows | the owning module's `Data/workflows/*.json`, embedded |
| Unbuilt upstream pieces (Timers/Quartz, Queries, Data, UI, the MVC designer host) | `reference/`, kept for diffing only |

## Engine

**The override.** `Crest.Workflows` depends on the stock `OrchardCore.Workflows` feature
so the upstream modules' workflow startups (gated on that feature id) keep registering their
activities and event handlers, and `CoreStartup` (`Order = int.MaxValue`, last by feature
dependency and then by order) re-registers `IWorkflowManager` as `OrchardWorkflowManager`
(stimuli into the engine) and removes the stock admin menu. Nothing executes on the stock
engine; its evaluators stay registered because stock activities resolve them. Upstream calls
exactly one thing, `IWorkflowManager.TriggerEventAsync`, and binds to
`OrchardCore.Workflows.Abstractions`, not to the stock module - which is why the override
needs no upstream change. Fallback if a stock piece ever fights it: claim the feature id and
exclude the stock assembly.

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

**Definitions store and publisher.** `CrestWorkflowsWorkflowDefinitionStore` maps
`WorkflowDefinitionPart` ↔ `WorkflowDefinition`; `ContentItemWorkflowDefinitionPublisher`
publishes through the content manager (a save with `publish: true` on a new definition
stores the draft first). The engine's definition notifications are raised once, by
`WorkflowDefinitionContentHandler` (Retracted with the definition as unpublished, so the
indexer drops its triggers). Both call the ownership guard (below). No store commits
mid-run: they flush, and the burst's shell scope commits ("Units of work" below).

**Fork changes worth knowing** (engine): `FindWorkflowInput<T>` (default when a key is
absent - `GetWorkflowInput` throws, which faulted flows started from the API, a timer or a
webhook); `LocalScheduler : IDisposable`. Designer: Radzen 11 and ASP.NET 10.0.9 (Crest's
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

Permissions are Orchard permissions, implied by the stock `ManageWorkflows`:
`ViewCrestWorkflows`, `EditCrestWorkflows`, `PublishCrestWorkflows`,
`RunCrestWorkflows`, `ManageShippedCrestWorkflows`, `ManageCrestWorkflowConnections`.
The engine API maps each endpoint to one of them and answers 403, never a login redirect.
Two shipped roles: `WorkflowEditor`, `WorkflowViewer`. A definition can additionally name
who may **edit** and who may **run** it (role or user names; `GET/PUT
api/crest/workflows/definitions/{id}/access`); the super user and Administrator are never
narrowed. `WorkflowOwnershipGuard` enforces the tier on every change path, including the
engine's own endpoints.

**The API gate** (`Security/CrestWorkflowsApiSecurityMiddleware`, on the API path
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
implied by the stock `ManageWorkflows`, which stays the umbrella so existing roles keep
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
override). `WorkflowDefinitionAccessHandler` is a raw `IAuthorizationHandler` (the member
ceiling's shape) that fails a `PermissionRequirement` for Edit/Manage shipped/Publish when
the Edit list is non-empty and names neither the user nor one of their roles, and Run
against the Run list; it acts only when the resource is a `WorkflowDefinitionAccessResource`,
so `AuthorizeAsync(user, permission, resource)` is the one call everywhere (guard, gate,
linker, access API) and role grants, the super user, the member ceiling and every other
handler decide together. The super user and Administrators are never narrowed by a list.
`GET/PUT api/crest/workflows/definitions/{id}/access` reads (View) and writes (the right to
edit that definition: Manage shipped for a shipped flow, Edit for a tenant one; system flows
refuse); writes update the latest and published versions in place, no new draft.

**Studio follows the same decisions.** `CrestWorkflowDefinitionLinker` replaces the
engine's static linker: a definition's write links (`publish`, `retract`, `delete`, `import`,
`update-references`) are present only for what the acting user may do to that definition,
and Studio's read-only decision is "no `publish` link". Nuance until the UI transition:
Studio ties editing to the `publish` link, so Edit without Publish reads as read-only.

**Running identity.** Activities run as trusted system code; authoring (Edit + Publish) is
the security boundary. The acting user at trigger time is snapshotted as claims
(`WorkflowUserContext`, workflow input `Actor` - not `User`, which stock Users events use)
and rebuilt on demand: `IWorkflowAuthorizer` asks Orchard's pipeline exactly as a request
would. `RequiredPermission` on the Crest and content triggers ends a run on `Denied`;
the `Require permission` activity gates any point of a flow. Rulings kept: the security
token (`ICrestAntiforgery`, the API behind the Orchard cookie) stays until the UI merge onto
Crest components; machine identities (client credentials, API keys) are a separate plan,
[machine-actors.md](machine-actors.md).

## Registry

The fifth registry, shaped like Parties: providers contribute descriptors, the catalog
merges them by key (first registration wins, ordered by position then key), the API and
pages read the catalog. The catalog merges lazily: stock activity constructors reach content
handlers that raise triggers through the publisher, which takes the catalog.

- **Triggers** - `WorkflowTriggerDescriptor(Key, DisplayName, Object, Description, Position)`
  from `IWorkflowTriggerProvider`; raised through `IWorkflowTriggerPublisher.PublishAsync`,
  which queues the stimulus to fire after the current unit commits (refuses unregistered
  keys so a typo fails loudly).
  Input to the flow: `TriggerKey`, `Payload` (ids and scalars only; activities re-read
  objects through their registries' services), `Actor`; correlation id = the object's id.
  The `Crest trigger` activity (`CrestTrigger`, ports Done/Denied/Skipped) subscribes
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

- **Crest trigger** (`CrestTrigger`): a registered trigger key, an optional payload
  filter (`Kind = invoice`, one `Key = value` per line, all must match → otherwise the
  `Skipped` port), an optional required permission for the acting user (otherwise `Denied`).
  The payload and the acting user arrive as workflow input (`Payload`, `Actor`,
  `TriggerKey`, `StimulusId`).
- **Stock Orchard events** (`OrchardEvent`): every event an Orchard module registers
  (content published, user logged in, ...) as a trigger with its stock filter; **stock
  tasks** run through `OrchardTask` (inside the unit) or `OrchardExternalTask` (e-mail, SMS,
  notifications, HTTP - after the unit commits, see below).
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

### Stock Orchard activities

Every activity the enabled Orchard modules register through `WorkflowOptions` (Email, Users,
Roles, Notifications, Forms, Contents, ...) is in the palette unchanged:
`StockActivityProvider` lists each as `orchard.<Name>` on the `OrchardEvent` (events),
`OrchardTask` (tasks inside the unit) or `OrchardExternalTask` (tasks with an effect outside
the database - `StockActivityRunner.External`: e-mail, SMS, notifications, HTTP - run as a
background activity after the unit commits) adapter with the name preset, the stock category,
`IsTrigger` for events; each adapter refuses the other's names. `OrchardWorkflowManager` turns `TriggerEventAsync(name, input, correlationId)` into
a stimulus keyed by event name; `OrchardEvent` instantiates the stock event with the node's
properties and evaluates its own `CanExecute` (a non-matching content type ends on Skipped,
the stock contract); `StockActivityRunner` runs a stock task with the two stock contexts
built from the engine's state, mapping outcomes to ports. `StockActivityRunner.EngineNative`
leaves out, and refuses to run, the stock control-flow/state/HTTP-pipeline/forms activities
the engine does natively. Option providers feed the property panel: permissions, registered
triggers, stock events and tasks, tenant connections, roles, content types. Stock Liquid in
stock activities works (the stock evaluators stay registered). Publishing a
`WorkflowDefinition` item never fans out to content triggers. Crest's JSON login raises
`UserLoggedInEvent` like the stock controller does.

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
approvals, connectors, external stock tasks. `Raise trigger` is not one.

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
  `Orchard external task` (the stock tasks with effects outside the database,
  `StockActivityRunner.External`: e-mail, SMS, notifications, HTTP) are the engine's own
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
  endpoints), connector activities, `Request approval`, stock tasks with external effects
  (e-mail, notifications, HTTP request) - all of them waits now that calls are two-phase.
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

The queue design that goes further (partitioned event delivery, a serial writer) is in
[workflows.md](workflows.md).

**Units and hooks, as built:** the engine stores flush instead of committing
(`FlushAsync`; the shell scope commits the burst), the trigger store treats a re-indexed
trigger as an update (delete-then-insert of one document in a session is refused by YesSql),
and the definition notifications (Publishing/Published, Retracting/Retracted) are raised
once, by the content handler, with Retracted carrying the definition as unpublished.
`WorkflowStimulusQueue` (Server/Units) collects every stimulus - registry triggers, stock
events, content events - and sends each in its own child scope after the scope's session
commits; nothing is sent when the unit failed or the commit threw. `WorkflowUnitOfWork`
(scoped; nested frames for hook attachments) and `UnitOfWorkCommitStateHandler` (wraps the
engine's commit handler: a failed unit cancels the document store and records the faulted
state through a child scope). `Hook` and `Fail unit` activities, `WorkflowHookService`
(system attachments from `IWorkflowHookAttachmentProvider`, tenant ones in a document),
`api/crest/workflows/hooks`, `WorkflowAtomicityAnalyzer` (used at attach and, through the
engine's validating notification, at publish), `IUnitBoundary` on the connector, approval
and external stock task activities; the generic `flow.hook` slot. Check `workflows-units`.
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
factored out), recovery at tenant activation. `ConnectorActivityBase` and
`OrchardExternalTask` are `RunAsynchronously`; `OrchardTask` refuses external task names;
`RaiseTrigger` is not an `IUnitBoundary`. `WorkflowHookRunner` (`IWorkflowHookRunner` in Domain) runs a slot from a
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
are new; the after-commit queue and `CrestTrigger` are thin glue over `IStimulusSender`;
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
- `IWorkflowObjectLock` (Domain) over the engine's `IDistributedLockProvider`, keyed by
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
and a read/write marker. `Server/Fields`, contracts in Domain: system activities
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
| `Crest trigger`, `Raise trigger` | start from / raise a registered trigger |
| `Hook`, `Fail unit` | run a slot's attachments inside the unit; fail the unit |
| `Require permission` | gate on an Orchard permission of the acting user |
| `Request approval` | park until a role member or permission holder decides |
| `Call connector`, `Poll connector` | call a tenant connection after commit; poll for changes |
| `Orchard task`, `Orchard external task` | a stock Orchard task inside the unit / after commit |
| `Copy fields`, `Move fields` | copy field values between content items by typed mapping rows |
| `Create party role`, `Resolve party` | Parties |

Triggers, activities and shipped flows by Crest module:

| Module | Triggers raised | Activities | Shipped flows |
| --- | --- | --- | --- |
| Parties | `party.role-created`, `.role-removed` (a content handler on every registered party type except the two base types; correlation = role id) | `CreatePartyRole` (registry key → role type, party from the payload, idempotent); `ResolveParty` (a role or base party id → RoleId, RoleContentType, RoleTypeKey, BasePartyId, BasePartyContentType: which item holds a field, nothing walks the graph implicitly) | — |
| Crest.Workflows | `flow.raised`; hook slot `flow.hook` | `Crest trigger`, `Raise trigger`, `Hook`, `Fail unit`, `Require permission`, `Request approval`, connector activities, `Orchard task` (inside the unit) and `Orchard external task` (e-mail, SMS, notifications, HTTP: a background activity after commit; the registry maps each stock task to the right one), `Copy fields` / `Move fields` (Server/Contents: source and target item ids - the target defaults to the payload's ContentItemId or TransactionId - and a JSON mapping of `Part.Field → Part.Field` rows each with a `required` marker; `ContentFieldValueCopier` resolves both sides against the tenant's current definitions, copies a same-type pair's JSON whole, converts text ↔ numeric, refuses other pairs; a required source that is empty or missing ends on Failed with the field named, an optional one is skipped and listed in `Skipped`; Move clears the source; the written field is re-applied as a typed element so the item's own readers see it in the same unit) | — |

**Approvals** (`Server/Approvals`): `Request approval` records an `ApprovalTask` (YesSql,
indexed) for an Orchard role and/or permission and waits on a bookmark;
`api/crest/workflows/approvals` lists what the signed-in user may decide,
`{id}/decide` (approve|reject, comment) authorizes through role membership or Orchard
authorization for the permission (member ceilings apply), records the decision and resumes
the flow on Approved or Rejected with DecidedBy/Comment as outputs; 403 outside the role,
409 once decided. Crest page `/workflows/approvals`.

## Connectors

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
Crest build turns every module's `blazor-wasm` project into lazy pages
(`CrestLazyModules.targets`, `tools/Crest.LazyModules`). Measured on a trimmed Release
publish (brotli): startup ≈ 5.2 MB, on demand 2.6 MB. **Browser-only** because under
InteractiveAuto a first visit runs in a server circuit where Studio's HTTP clients would
share scopes across users; the host prerenders a placeholder and reloads into WASM if an
in-app link reached it inside a circuit.

The move of the designer onto Crest components is in [workflows.md](workflows.md).

## Testing

Playwright checks under `tests/playwright/checks`, registered in the host's aggregated
suite (the harness is Crest's); each cleans up what it creates and leaves shipped flows as
shipped. The checks Crest.Workflows carries:

| Check | Covers |
| --- | --- |
| `workflows-api` | engine API round-trip (save, publish, execute, journal), antiforgery 400, limited role 403, anonymous 401 |
| `workflows-orchard-activities` | stock `ContentPublishedEvent` (filtered) starts a flow; stock `CreateContentTask` with stock Liquid |
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

- Module: `Crest.Workflows` - `Domain` (contracts), `Server` (the
  module: `Registry`, `Security`, `Units`, `Hooks`, `Fields`, `Contents`, `Connectors`,
  `Approvals`, `Orchard`, `Stores`), `engine` (the vendored engine), `designer` (the forked
  Studio), `tests`.
- The consuming module's part, e.g. an ERP module's `Server/Workflows` (provider,
  activities), `Server/Ledger`, `Server/Data/workflows/*.json` (shipped flows).
- Admin pages: Workflows › Definitions, Instances, Connections, Approvals, under the Crest
  admin; the designer opens from a definition.

## Still to build

The workflow service, its registry, connectors, approvals, ownership, permissions, units
of work, hooks, field dependencies and the stock-Orchard bridge are built: see
[docs/workflows.md](workflows.md). This plan holds what is designed but not built,
the rulings that shape the design, and the open work.

### Rulings

- **Workflows is the fifth core registry, owned by Crest.**
- **Elsa is the Orchard workflow service, by override** (2026-09-27): depend on the stock
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

### Crest

- [ ] **Boot the Blazor admin shell under a url-prefixed tenant.** The Blazor admin shell does not boot under a url-prefixed tenant
  (`/wfiso/_framework/dotnet.js` served with an empty MIME type); found by the isolation
  check, which signs in through `api/crest/auth/login` instead. Belongs to Crest's tenant
  plan. Also the navigated-under-evaluate flake (docs › Testing).

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
