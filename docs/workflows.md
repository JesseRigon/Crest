# Workflows

How Crest's workflow service works: what a workflow is here, who may do what to one, how
a flow gets started, how a run commits (or does not), and how the posting engine is built on
top of it. The plan with its rulings and history is [plans/workflows.md](../plans/workflows.md);
the flowchart of a run's life is [workflows.mmd](workflows.mmd). Testing is in
the host's testing documentation.

## What it is

`Crest.Workflows` is the tenant's workflow service. It is a vendored fork of Elsa 3 (the
engine: activities, flowcharts, bookmarks, bursts, the designer) installed as the Orchard
workflow service by override: the stock `OrchardCore.Workflows` feature stays enabled so
every Orchard module's events and tasks keep registering, and Crest replaces the services
behind it. The engine's own HTTP API lives at `crest-workflows/api` behind the tenant
cookie, Crest's antiforgery header and its permissions; the designer (the forked
Studio, Blazor WebAssembly) is loaded on demand into the Crest admin.

Workflows is a **registry** like Parties, Accounts, Assets and Transactions: modules register
what they contribute - triggers, activities, hook slots, shipped flows, connectors - through
contracts in `Crest.Workflows.Domain`, and never reference the engine module. The
registry is read at `GET api/crest/workflows/registry`.

| A module registers | Through | Example |
| --- | --- | --- |
| Triggers it raises | `IWorkflowTriggerProvider`, raised with `IWorkflowTriggerPublisher.PublishAsync(key, correlationId, payload)` | `transaction.posted` |
| Activities (palette entries) | `IWorkflowActivityProvider` + an engine `Activity` class | `Post transaction`, `Create invoice` |
| Hook slots and system attachments | `IWorkflowHookSlotProvider`, `IWorkflowHookAttachmentProvider`, run with `IWorkflowHookRunner.RunAsync(slot, correlationId, payload)` | `transaction.posting` ← `accounting.post-invoice` |
| Shipped flows | `IWorkflowFlowProvider` (definition JSON embedded in the module) | `accounting.quote-to-invoice` |
| Connectors | `IWorkflowConnectorProvider` | `http`, `webhook` |

Trigger payloads are **ids and scalars only**; an activity re-reads the object through its
own registry service.

## Ownership and permissions

Every definition has an **ownership tier**, kept in its custom properties:

- **system** - code's. Shipped by a module, read-only for every user including the
  administrator, upgraded in place when the module bumps its version. The posting flows.
- **shipped** - a template a module ships that the tenant may edit. Editing marks it
  *forked*; a forked copy is not upgraded until it is **reset** to the shipped version
  (`POST api/crest/workflows/registry/flows/{key}/reset`), which keeps the tenant's edits in
  the version history. `accounting.quote-to-invoice` ships as a draft: publish it to turn it on.
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

## Units of work: how a run commits

A run is a sequence of **bursts**: the engine executes synchronously until a wait (a
bookmark), persists, and resumes later. Here **a burst is a unit of work**: one shell
scope, one YesSql session, committed as one transaction when the scope ends. The engine's
stores flush but never commit mid-run. A burst is bounded by compute time - nothing inside
a unit waits or calls out.

**Hooks run inside the unit.** A hook slot (`transaction.created`, `transaction.posting`,
`transaction.voiding`, the generic `flow.hook`) is run either by a flow's `Hook` activity or
by a registry service from inside its own request (`IWorkflowHookRunner`) - so a tenant's
attachment to `transaction.created` runs whether the invoice came from the API or from a
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
the object's id, re-entrant within a unit) is taken by the transaction service around every
write to a document and by the after-commit send around each correlated run: no two units
touching one document interleave. What is still pending for an object - flows about it that
have not finished and the jobs they wait on - is `GET api/crest/workflows/pending?correlationId=`.

Shell scopes everywhere: scheduled tasks (timers, cron, delays), the engine's background
command and notification channels and the bookmark-queue worker all run in shell scopes
of their own (`ShellScopedRunScheduledTaskHandler`, `ShellScopedBackgroundConsumers`,
`ShellScopedBookmarkQueueWorker`), so every entry point is a unit.

## Field dependencies

Contracts are **per-activity field dependencies**, not a record-level contract. An activity
declares each content field it reads or writes as `Part.Field` with a **required** marker
and a read/write marker - in code (`[FieldDependency]`, which is what the system activities
do; Accounting's read the base `TransactionPart` fields it ships required and
module-locked) or from its bindings (`Copy fields`' mapping rows, `Journal entry`'s amount
paths). At publish, `WorkflowFieldDependencyAnalyzer` resolves them against the tenant's
definitions and stamps **warnings** on the version (`Crest.FieldWarnings`; the part or
field is missing, or a required read is optional or conditionally visible in the tenant's
definition) - never a refusal; `GET api/crest/workflows/definitions/{id}/field-dependencies`
resolves on demand. At runtime a missing value for a **required** dependency ends the
activity on `Failed` naming the field; an optional one reads null. Declared dependencies
ride the engine's activity descriptors (`crest:fieldDependencies`) and the registry.

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
| `Create transaction` (one palette entry per kind), `Post transaction`, `Void transaction`, `Convert transaction`, `Resolve account` | Accounting's document operations through its service |
| `Journal entry`, `Reverse journal entries` | the ledger |
| `Create party role`, `Resolve party` | Parties |

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

## The posting engine

Accounting owns the documents (quote, sales order, invoice, customer payment, journal
entry) as transaction kinds; **the posting engine is workflows**. Posting a document
(`POST api/crest/accounting/transactions/{id}/post`) numbers it, snapshots the party,
freezes tax, publishes it, then runs the `transaction.posting` hook slot inside the same
unit. Attached there as **system** flows:

- `accounting.post-invoice`: for an invoice, a `Journal entry` debiting accounts
  receivable for the total, crediting sales for the subtotal and sales tax payable for the
  difference (skipped when zero).
- `accounting.post-customer-payment`: debit undeposited receipts, credit accounts
  receivable for the total.
- `accounting.reverse-posting` on `transaction.voiding`: a reversing entry for every posted
  entry about the document.

The journal entry is itself a transaction (`JE-`): a `JournalEntry` item whose lines are
`JournalLine` items (account id and code, debit or credit, memo), posted through the same
service in the same unit. Posted lines are indexed; an account's balance and the trial
balance are sums over that index, signed by the account's normal side
(`api/crest/accounting/ledger`). Accounts are named by **registered kind**
(`accounts-receivable`, `sales`, ...) - the tenant maps each kind to one of its accounts -
so a system flow never names a code. The invoice posts with its entry or not at all; a
tenant attachment to `transaction.posting` that fails the unit leaves the invoice a draft.
After commit, `transaction.posted` fires for the invoice and for its entry (tenant flows
filter on `Kind`), plus `transaction.account-posted` once per account kind the invoice's
kind posts to.

## Where things are

- Module: `Crest.Workflows` - `Domain` (contracts), `Server` (the
  module: `Registry`, `Security`, `Units`, `Hooks`, `Fields`, `Contents`, `Connectors`,
  `Approvals`, `Orchard`, `Stores`), `engine` (the vendored engine), `designer` (the forked
  Studio), `tests`.
- The consuming module's part, e.g. an ERP module's `Server/Workflows` (provider,
  activities), `Server/Ledger`, `Server/Data/workflows/*.json` (shipped flows).
- Admin pages: Workflows › Definitions, Instances, Connections, Approvals, under the Crest
  admin; the designer opens from a definition.
