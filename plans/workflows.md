# Workflows — the fifth registry, the engine under it, and the designer on top

## Status

**Current as of 2026-10-02.** The engine, the registry, the stock-Orchard bridge, connectors
(OpenAPI import, OAuth2 authorization code), approvals, shipped flows, ownership tiers, the
permission set, per-flow access, units of work with hooks and background external calls,
per-object serialization, field dependencies (declared, warned at publish, checked at
runtime), the posting activities (`Create transaction`, `Copy fields` / `Move fields`,
`Resolve party`), and the posting engine itself (the Journal Entry kind, the ledger, the
system flows that post invoices and customer payments and reverse on void) are built and verified (`bash dev/dev.sh test`; the Testing section lists the checks). Open
work, by domain, is listed at the end of each section below and collected under "Open
work". The one large item left is the **designer's move onto Crest components** (deferred to last
by ruling; the forked Studio runs in the Crest admin on MudBlazor, loaded on demand, until
then), with the admin UI that waits for it (tier badge, Outdated + Reset, access lists,
field-dependency panel, pending state).

This document describes the system as it is. History (the stock-module analysis, the port's
state when found, the phase-by-phase build) lives in git; rulings that still shape the
design are kept under "Rulings".

**Where it lives.** Workflows is part of the application layer
([application-layer.md](application-layer.md)): `Crest.Workflows` in the Crest
repository, engine included, with this plan and [docs/workflows.md](../docs/workflows.md)
beside it. The engine API prefix is `crest-workflows/api` and activity type names are
`Crest.Workflows.*`. Accounting's activities, hook attachments and posting flows are
Accounting's and are the worked example of a downstream module. The code still sits
under `Crest.Workflows` with the old names; the move is on that
plan's checklist, and names below are the current ones until it lands.

## What the system does

1. **Connects modules to each other without references.** A CRM stage change
   creates an Accounting role; a posted invoice starts a tenant's own flow; a Hire will
   produce a Pay Run. Modules register activities, triggers and flows; the definition holds
   the connection.
2. **Connects to Crest and Orchard modules.** Content events, users and roles, e-mail,
   notifications, forms: everything Orchard already does is a palette item, not a
   re-implementation.
3. **Connects to external services.** Outgoing HTTP calls with stored credentials, incoming
   signed webhooks, polling on a schedule, retries and rate limits, without code.
4. **Is authored by tenants in the Crest admin**, with drafts, publishing and versions.
5. **Is multi-tenant and permissioned like everything else:** Orchard permissions and roles,
   per-shell data and execution, shipped definitions migrated by code.

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
depends on it (Accounting, Parties, CRM, Assets declare the feature); there is no no-op
mode. Crest is the UI and API layer and never references it; the generic pieces the module
needed from Crest (late service containers, lazy module pages, JS components, antiforgery
access) are Crest features any module can use.

**Where code lives**

| Piece | Where |
| --- | --- |
| Contracts modules bind to: `WorkflowTriggerDescriptor`, `WorkflowFlowDescriptor` (+ `WorkflowOwnership`), `WorkflowActivityDescriptor`, `WorkflowConnectorDescriptor`, the provider interfaces, `IWorkflowTriggerPublisher`, `WorkflowsConstants` (routes, permissions, roles, property keys) | `Domain/` |
| Engine wiring, stores, publisher, API gate, permissions, ownership guard, access handler and API, registry catalog/importer/controller, triggers, stock adapters, connectors, approvals, navigation | `Server/` |
| Content triggers and tasks | `Contents/` |
| Admin pages, `StudioHost`, the canvas node stand-in, `crest-lazy-assemblies.txt` | `blazor-wasm/` (always loaded, thin) |
| Studio's own container (`StudioServices.BuildAsync`), cookie API handler, views | `studio-host/` (lazy) |
| Activities on core objects | the module that owns the object (Accounting: Post/Void/Convert/ResolveAccount; Parties: CreatePartyRole) |
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
mid-run: they flush, and the burst's shell scope commits ("Posting on workflows").

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

**Performance dials (decided 2026-10-02).** The engine's default workflow commit strategy
is none: a burst persists once, at its end, through the commit handler - exactly the unit
model, so no strategy is configured; an activity-level strategy a designer picks is a
flush within the unit, never a commit. Log persistence stays at the engine default (inputs
and outputs journaled): the posting flows' journal is the audit trail. Measured: an invoice
posting through the hook, with its journal entry (two content items, two bookmarks' worth
of engine state, the index rows), averages about 55 ms end to end through the API in the
dev instance (`workflows-ledger` budgets 4 s per posting and reports the average). Compile-on-publish is
**not** done: the graph materializer's cache (`UseCache`) already keeps parsed graphs, and
nothing measured points at graph interpretation.

Open work: none.

## Security

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

Open work:
- UI for the tier badge, Outdated + Reset, and the access-list editor (with the designer
  transition).
- Machine actors: see [machine-actors.md](machine-actors.md) (notes only).

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
  inside the unit (see "Posting on workflows"); `WorkflowHookSlotDescriptor` and
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

Open work: none.

## Stock Orchard activities

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

Open work: none.

## Activities and flows by module

| Module | Triggers raised | Activities | Shipped flows |
| --- | --- | --- | --- |
| Accounting (Transactions) | `transaction.created` (after the creating unit commits; payload TransactionId, Kind, ContentType, PartyId), `transaction.posted`, `.voided`, `.converted` (TransactionService; payload TransactionId, Kind, Number, ContentType); `transaction.account-posted` once per account kind the document's kind `PostsTo` (AccountKind, AccountId, AccountCode, Total, Currency). **Hook slot** `transaction.created`: run by the service inside the unit that creates a draft - API, `CreateTransaction`, `ConvertTransaction` - once the document is whole; tenants may attach | `CreateTransaction` (kind, party, dates, notes through the service; the palette shows it once per registered kind as *Create invoice*, *Create quote*, ... via registry presets; moves the flow's correlation to the new document), `PostTransaction`, `VoidTransaction`, `ConvertTransaction` (with "post the result"), `ResolveAccount` (account for a kind); the document id defaults to the payload's TransactionId; failures end on Failed with the reason as an output | `accounting.transaction-posted` (**system**, the sample: a posted document writes a line); `accounting.quote-to-invoice` (shipped, draft: posted quote → posted sales order → draft invoice, two `FlowDecision`s on Kind) |
| Accounting (Ledger) | hook slots `transaction.posting`, `transaction.voiding` | `PostJournalEntry` (*Journal entry*), `ReverseJournalEntries` | `accounting.post-invoice`, `accounting.post-customer-payment`, `accounting.reverse-posting` (**system**, attached to the slots) |
| Accounting (Accounts) | `account.created` (AccountService) | — | — |
| Parties | `party.role-created`, `.role-removed` (a content handler on every registered party type except the two base types; correlation = role id) | `CreatePartyRole` (registry key → role type, party from the payload, idempotent); `ResolveParty` (a role or base party id → RoleId, RoleContentType, RoleTypeKey, BasePartyId, BasePartyContentType: which item holds a field, nothing walks the graph implicitly) | — |
| CRM | `crm.stage-changed` (payload RoleId, ContentType, TypeKey, PartyId, Stage, PreviousStage; the last raised stage is kept so a save without a change raises nothing) | — | `crm.lead-to-customer` (shipped, draft: Qualified Lead → Prospect, Won Prospect → Customer through `CreatePartyRole`) |
| Assets | `asset.created`, `.changed` (a content handler over every registered asset kind) | — (Assets has no operations yet) | — |
| Crest.Workflows | `flow.raised`; hook slot `flow.hook` | `Crest trigger`, `Raise trigger`, `Hook`, `Fail unit`, `Require permission`, `Request approval`, connector activities, `Orchard task` (inside the unit) and `Orchard external task` (e-mail, SMS, notifications, HTTP: a background activity after commit; the registry maps each stock task to the right one), `Copy fields` / `Move fields` (Server/Contents: source and target item ids - the target defaults to the payload's ContentItemId or TransactionId - and a JSON mapping of `Part.Field → Part.Field` rows each with a `required` marker; `ContentFieldValueCopier` resolves both sides against the tenant's current definitions, copies a same-type pair's JSON whole, converts text ↔ numeric, refuses other pairs; a required source that is empty or missing ends on Failed with the field named, an optional one is skipped and listed in `Skipped`; Move clears the source; the written field is re-applied as a typed element so the item's own readers see it in the same unit) | — |

**Approvals** (`Server/Approvals`): `Request approval` records an `ApprovalTask` (YesSql,
indexed) for an Orchard role and/or permission and waits on a bookmark;
`api/crest/workflows/approvals` lists what the signed-in user may decide,
`{id}/decide` (approve|reject, comment) authorizes through role membership or Orchard
authorization for the permission (member ceilings apply), records the decision and resumes
the flow on Approved or Rejected with DecidedBy/Comment as outputs; 403 outside the role,
409 once decided. Crest page `/workflows/approvals`.

Open work:
- Assets: movement, valuation and depreciation activities once Assets has those operations.
- CRM: stages stay free text (`CRMConstants.Stages`) until CRM pipelines exist.

## Connectors

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
sets `CrestWorkflows:Connectors:AllowPrivateNetworks` (dev.sh does, for the local test
endpoint).

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

Open work: none.

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

**The transition onto Crest components** (ruling 2026-09-28; deferred to last, ruling
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

Open work:
- The transition above (last, after the workflow backend is complete).
- Tier badge, Outdated + Reset, access-list editor, field-dependency display (with it).
- Crest: the Blazor admin shell does not boot under a url-prefixed tenant
  (`/wfiso/_framework/dotnet.js` served with an empty MIME type); found by the isolation
  check, which signs in through `api/crest/auth/login` instead. Belongs to Crest's tenant
  plan.

## Posting on workflows (rulings 2026-09-30 / 2026-10-01; built 2026-10-01 / 2026-10-02)

**The posting engine is workflows, not service code.** Accounting owns the standard
documents (quote, sales order, invoice, credit memo, ...) and registers them as kinds in the
Transactions registry (its own domain and feature inside the Accounting module).
Crest.Workflows is the engine, a separate module for dev and publishing. Accounting
registers into it the activities that post, the triggers that start them, and the posting
flows themselves, shipped as **`system` flows** that tenants hook through the extension
points and never edit. This replaces the host's transactions plan phase 3 as first
written.

**Contracts are per-activity field dependencies, not a record-level contract.** An
activity declares each content field it reads or writes as a dependency on a field path
(`TransactionPart.Total`, `Invoice.SomeTenantField`) with a **required marker per field**
and a read/write marker. Built (`Server/Fields`, contracts in Domain): system activities
declare theirs in code with `[FieldDependency(path, Required, Writes, ContentType)]`
(Accounting's transaction activities do; `TransactionFieldPaths` names the base fields);
configurable activities derive theirs from their bindings through `IFieldDependencySource`
(`Copy fields`' mapping rows: each source with its own marker, each target as a write). The
engine's input model is untouched - the dependency is a declaration beside the inputs, read
from literal bindings at publish. A dedicated mapping editor comes with the designer
transition; until then the rows are a JSON input.

**Publish warns, runtime fails, and only for required fields.** Built:
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
  `Failed` with the field named (`IWorkflowFieldDependencyChecker`, which Accounting's
  activities call before asking the service; `Copy fields` does the same per row); an
  **optional** dependency reads null and the activity accounts for it ("use it if you have
  it"; tax is the standing example, it may not be enabled).
- Declared dependencies ride the engine's activity descriptors
  (`crest:fieldDependencies`, via an `IActivityDescriptorModifier`) and the registry's
  activity entries (`FieldDependencies`), so the palette and the panel can show them.

**System activities name their fields, and those fields are locked.** Every system activity
is specific about the fields it depends on (id, created date, the transaction part's number,
status, party id, subtotal, total, ...); those are marked required and **locked in the
shipped content type definition** so tenants cannot make them optional or hidden
(Accounting ships `TransactionPart.Status` and `TransactionDate` required and locked; the
posting activities read them required, Number and the party optional). **The lock
mechanism lives in Crest** (ruling 2026-09-30): the field/part-definition counterpart of
Crest's option-list locks (None|Tenant|Module, module locks unliftable in-tenant), tracked
in [plans/content-items.md](content-items.md).

**Activities compose.** Sub-actions such as "write a journal entry" are activities of their
own, chained and nested like functions: `UsableAsActivity` makes a published definition a
palette entry (`WorkflowDefinitionActivity`, a `Composite`), so a posting flow is built from
smaller system flows; the C# `Composite` base stays for pieces that belong in code. The
ownership tiers apply to the pieces.

**Units of work, hooks and queues (rulings 2026-10-01).** The posting activities and
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
  from inside its own request through `IWorkflowHookRunner` (`transaction.created` in
  `TransactionService`), so the attachment runs whichever path created the object. Hooks
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
- **Two queues, two guarantees (the full way; designed, built after the above, on engine
  parts - see the audit).** The *write side*: no two units touching one object interleave -
  first through the engine's `IDistributedLockProvider` keyed by correlation id around a
  unit, with version checks on what the unit read and a re-run on conflict; a serial writer
  applying working sets only if that is measured insufficient. The *event side*: the engine's
  stimulus dispatch and bookmark queue, hardened - emitted after commit (built), partitioned
  by correlation id (one consumer per object at a time, in order), payloads carrying the
  object version the unit committed, at-least-once delivery with idempotency keys (unit id +
  slot) for exactly-once effect. Costs accepted: read-your-writes latency with a pending state
  in the UI, optimistic conflicts re-run.
- **What stays impossible, on purpose:** a hook that must call out *and* be in the
  transaction (a distributed transaction with someone else's API). The validator says so and
  names the two honest shapes: call after commit with compensation (`Void` answers `Post`),
  or call before the unit as a precondition.

**Still to design:** a flow's *output* contract (what a composable flow promises its
caller), and version checks beyond the object lock should a conflict rate ever ask for
them. The ledger record, `PostsTo` as lines in a system flow, and reversal on void are
built (below). `PostsTo` on a kind stays declarative - the registry's statement of which
accounts a kind touches, used for the `transaction.account-posted` trigger - while the
posting flow names the lines; the two agree by construction for the shipped kinds.

**Built 2026-10-01 (units and hooks):** the engine stores flush instead of committing
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
and Raise trigger activities; the generic `flow.hook` slot. Check `workflows-units` (12).
Known limits, for the queue stage: the flows one stimulus starts share a unit (the serial
writer gives per-unit isolation); the approval decision resumes its flow inline in the
deciding request and an incoming webhook runs its flows in the request (both are their own
boundaries, so no unit is split, but neither is after-commit). Child scopes never activate
the shell (`activateShell: false`): during setup that path waits on the activation lock the
setting-up scope holds.

**Built 2026-10-01 (background calls, field activities, the created slot):**
`WorkflowAfterCommit` (Server/Units) is the one after-commit hook the stimulus queue and the
background jobs share. `DurableBackgroundActivityScheduler` (replaces the engine's in-memory
`LocalBackgroundActivityScheduler` through `WorkflowRuntimeFeature.BackgroundActivityScheduler`),
`WorkflowBackgroundJob` + store + `BackgroundJobContext` (the running job, for the
idempotency key), `ShellScopedBackgroundActivityInvoker` (the engine's invoker with the
hand-back resumed directly; engine edit: `ResumeWorkflowAsync` virtual, `BuildResumeOptionsAsync`
factored out), recovery at tenant activation. `ConnectorActivityBase` and
`OrchardExternalTask` are `RunAsynchronously`; `OrchardTask` refuses external task names;
`RaiseTrigger` lost `IUnitBoundary`. An earlier two-phase implementation of the same
(`WorkflowOutboundCall`, Begin/Complete split in the activities) was replaced after the
audit below. `WorkflowHookRunner` (`IWorkflowHookRunner` in Domain) runs a slot from a
flow or a service; `WorkflowHookFailedException` → 409 through `WorkflowHookFailedExceptionFilter`
for any module's controller; a failed `WorkflowUnitOfWork` cancels its own session through a
before-dispose callback registered ahead of Orchard's commit, so a service's request discards
the same way a burst does. `ContentFieldValueCopier`, `CopyFields`, `MoveFields`;
Accounting's `CreateTransaction` with per-kind registry presets and `TransactionKindOptionsProvider`,
the `transaction.created` slot and trigger raised from `TransactionService.CreatedAsync`
(create and convert); Parties' `ResolveParty`. Found and fixed on the way: scheduled tasks
(timers, cron, delays) ran in a bare service scope where nothing after-commit could run -
`ShellScopedRunScheduledTaskHandler` replaces the engine's handler and gives each a shell
scope; and the engine persisted only *declared* workflow input across bursts, so a flow
resumed after a wait read a null payload - the vendored `WorkflowStateExtractor` now keeps
undeclared input too. Checks `workflows-fields` (9), `workflows-connectors` (+2).

**Audit 2026-10-01 - what the engine already had, and what we had rebuilt.** Ruling: do not
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
background task ran inline unless a node toggled it (engine edit in `ActivityDescriber`). The planned queues follow the
same ruling: the event queue is the engine's stimulus dispatch + bookmark queue, hardened;
per-object serialization is tried first with the engine's `IDistributedLockProvider` keyed by
correlation id, a serial writer only if measurement says so.

**Built 2026-10-02 (the write side, the ledger, the posting flows):**
- `IWorkflowObjectLock` (Domain) over the engine's `IDistributedLockProvider`, keyed by
  object id, re-entrant within a unit (scoped): the transaction service takes it around
  every write to a document (update, lines, post, void, convert) and the after-commit send
  takes it around each correlated run, so no two units touching one document interleave.
  Version checks reduce to "read under the lock" for now; no serial writer was needed.
- Every stimulus carries a `StimulusId` input (the event side's idempotency key); the
  trigger journals it. `GET api/crest/workflows/pending?correlationId=` answers the pending
  state an object's page shows: running or waiting flows about it and their background jobs.
- The **Journal Entry** kind (`journal-entry`, `JE-`): a transaction whose Lines bag holds
  `JournalLine` items (account id and code snapshot, debit or credit as price fields, memo),
  with a `JournalEntryPart` (memo, reverses / reversed-by links); posted lines are indexed
  (`LedgerLineIndex`) and balances are sums over that index, signed by the account's normal
  side. `ILedgerService` posts a balanced entry in one unit (lines resolved by registered
  account kind, id or code), reverses every entry about a document, reads entries and
  balances; `api/crest/accounting/ledger` (trial balance, account balance, entries by source
  and by id). The **Customer Payment** kind (`customer-payment`, `PMT-`, posts to undeposited
  receipts and receivable) with an `AppliesTo` field.
- Ledger activities: `Journal entry` (lines by account kind with amounts as numbers, price
  field paths or a difference of two - the paths are the node's declared field dependencies)
  and `Reverse journal entries`.
- Hook slots `transaction.posting` (run by the service once the document is numbered and
  published, inside the posting unit) and `transaction.voiding`, with the **system
  attachments** `accounting.post-invoice` (receivable / sales / tax payable),
  `accounting.post-customer-payment` (undeposited receipts / receivable) and
  `accounting.reverse-posting`: the document posts with its entry or not at all, voids with
  its reversals or stays posted. A tenant attachment that fails the posting unit answers
  409 and leaves the document a draft (`workflows-ledger`). A posted journal entry raises
  `transaction.posted` like any document; tenant flows on that trigger filter on `Kind`.

Open work: the UI for the pending state (with the designer transition). A serial writer and
version checks beyond the lock only if a measured conflict rate asks for them.

## Testing

Playwright checks under `tests/playwright/checks`, registered in `dev/run-admin-suite.js`
(the harness is Crest's); each cleans up what it creates and leaves shipped flows as
shipped.

| Check | Covers |
| --- | --- |
| `workflows-api` | engine API round-trip (save, publish, execute, journal), antiforgery 400, limited role 403, anonymous 401 |
| `workflows-orchard-activities` | stock `ContentPublishedEvent` (filtered) starts a flow; stock `CreateContentTask` with stock Liquid |
| `workflows-accounting` (Accounting) | registry entries, the system sample flow runs once per posted document, the convert chain stops itself |
| `workflows-ledger` (Accounting) | the posting and voiding slots carry the system flows; posting an invoice posts a balanced entry inside the unit (receivable debit = total, sales credit = subtotal); the trial balance balances and receivable carries the total; a failed posting attachment answers 409, the invoice stays Draft, no entry; voiding posts a reversal and receivable returns; a customer payment books undeposited receipts against receivable; entries by id; five postings within budget |
| `workflows-parties` (Parties) | role-created/removed |
| `workflows-connectors` | Call connector with bearer to a Playwright-hosted endpoint, retry (the resilience feature, the connection's policy), rate limit, OAuth2 token reuse, path escape, webhook signatures, poll changes, a Timer-driven poll; calls are background activities (the run suspends, the call carries one `Idempotency-Key`, the flow settles after commit) and a call scheduled in a failed unit is never made; OAuth2 authorization code end to end against the local provider; OpenAPI import with one palette entry per operation |
| `workflows-shipped-flows` | Quote → Invoice and Lead → Customer as drafts, published for the test then retracted (with the retract regression), account/asset triggers |
| `workflows-ownership` | tiers, system-flow refusals, fork/reset, `WorkflowViewer`/`WorkflowEditor`, access lists, payload filter, Raise trigger; registry sync is idempotent |
| `workflows-units` | a failed required hook attachment faults the host and discards its writes; a healthy one commits with child instances; best-effort failure journaled; long-running flows refused at attach and at republish; a trigger raised in a failed unit never fires |
| `workflows-fields` | the registry lists `transaction.created` and the per-kind Create activities; a tenant attachment (Resolve party → Copy fields → Fail unit on Failed) runs inside the API's unit so the created invoice already carries the copied field; a required row with no value answers 409 and creates nothing; best-effort lets it through; `Create transaction` from a flow runs the hook and correlates; Move fields clears the source; incompatible types end on Failed naming both; field dependencies on the registry and the engine descriptors; publish stamps missing/optional warnings without refusing; the field-dependencies API; a flow on system-required fields carries none |
| `workflows-approvals` | request, queue, decide, 403/409; the pending API shows the parked flow for its object |
| `workflows-designer` | the real UI: open, add node, connect, save, publish, run, journal; lazy download assertion; Connections and Approvals pages |
| `workflows-tenant-isolation` (last) | a second tenant (`wfiso`): 401 across, no shared definitions/triggers/instances, shipped flow per tenant, events do not cross, shell reload leaves the other engine running |

Unit tests (`tests/Crest.Workflows.Tests`, 62): the API gate branch by branch (grant
mapping, per-definition Run, 403 on refusal), ownership property round-trips and stamping,
the access handler's veto/admit/bypass rules, the system scope, the registry catalog and
publisher, the user snapshot, connector address policy, path containment and signatures,
the async-only disposable scan, the shipped-flow sync decision.

**Known flake:** on a freshly provisioned tenant, a check running early in a filtered run
occasionally hits Playwright's "Execution context was destroyed" (the admin page navigated
once under an evaluate). Not reproduced idle or in the full suite; `workflows-accounting`
settles and retries once. Suspect a circuit drop and reload in Crest.
Also: edits under the watch dev server during a `dev.sh test` run rebuild shared WASM
assets and break the test server's integrity checks (every login stays disabled); stop the
watch server or do not edit during a run. A build that fails on
`ActivityWrapper.razor` with RZ1021/RZ9981 (unchanged file) is a stale Razor build server:
`dotnet build-server shutdown`, then build again.

## Rulings

- **Workflows is the fifth core registry, owned by Crest**
  and API layer and never references it.
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

## Open work

Collected from the sections above.

- **Designer:** the Crest-components transition (last); with it the tier badge, Outdated +
  Reset, access-list editor, field-dependency panel, pending state, OAuth "Authorize" button
  on a connection; the security token consolidation.
- **Posting:** a serial writer and version checks beyond the object lock only if a measured
  conflict rate asks for them; the purchasing kinds and their posting flows belong to
  the host's transactions plan.
- **Modules:** Assets activities once Assets has operations; CRM pipelines to replace free-text stages.
- **Crest:** admin shell under url-prefixed tenants; the navigated-under-evaluate flake.
- **Elsewhere:** machine actors ([machine-actors.md](machine-actors.md)).
