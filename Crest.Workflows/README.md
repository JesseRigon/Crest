# Crest.Workflows

The workflow service of the Crest application layer, and the registry every module
contributes its triggers, activities, hook slots and flows to. Elsa 3, integrated the way
`OrchardCore.Workflows` is: per-shell YesSql stores, definitions as content items,
Orchard permissions on the API. Plan and rulings: `../../plans/workflows.md`. Origin:
`UPSTREAM.md`.

Layout: `Server/` (engine, feature `Crest.Workflows`), `Contents/` (content
triggers and tasks, feature `Crest.Workflows.Contents`), `tests/`, and `reference/`
(the port's other modules and the designer, kept unbuilt until their phase).

## Features

- `Crest.Workflows` — the engine and the engine's HTTP API (`~/crest-workflows/api`) inside
  the tenant. Depends on `OrchardCore.Workflows` (the upstream modules' workflow startups
  are gated on that id) and `OrchardCore.Contents`.
- `Crest.Workflows.Http` — Elsa HTTP endpoint/request activities.
- `Crest.Workflows.Contents` — content triggers and tasks.

## Security model

- **Who may call the API:** `CrestWorkflowsApiSecurityMiddleware`. Anonymous → 401; authenticated
  without Orchard's `ManageWorkflows` → 403 (evaluated per request through Orchard's
  authorization pipeline, so every module's `IAuthorizationHandler` applies — the member
  permission ceiling lists `ManageWorkflows`); non-GET without a valid antiforgery token →
  400. Only then does the request get the engine's `permissions=*` grant, on a per-request
  identity, never in the cookie.
- **Tenant isolation:** one Elsa per shell — services, stores (tenant `ISession`),
  hosted services and the file lock directory (`<tenant App_Data>/locks`) are all the
  shell's own. Nothing addresses another tenant.
- **Acting user:** every content stimulus carries a `User` input
  (`WorkflowUserContext`: id, name, tenant, claims snapshot). Triggers can require a
  permission (`RequiredPermission`) and the `RequirePermission` activity gates any flow;
  both evaluate the snapshot through Orchard's real `IAuthorizationService`.
- **Activities run as system code** once a definition is published — the gate is on
  authoring, as in the stock module.

## The registry (Domain)

A consuming module depends on `Crest.Workflows.Domain` and registers:

- `IWorkflowTriggerProvider` — the triggers its registry raises (`transaction.posted`).
- `IWorkflowActivityProvider` — activities it contributes to the palette for the objects it
  owns; the activity classes live in the module and are added to the engine from its
  startup with `services.ConfigureCrestWorkflows(w => w.AddActivitiesFrom<Startup>())`.
- `IWorkflowFlowProvider` — flows it ships as the engine's definition JSON (embedded under
  `Data/workflows`), imported and published once per tenant, found again by key.
- and calls `IWorkflowTriggerPublisher.PublishAsync(key, correlationId, payload)` from its
  service. The publisher refuses unregistered keys and adds the acting user.

Flows subscribe with the `Crest trigger` activity (`Crest.Workflows.CrestTrigger`,
input `TriggerKey`, optional `RequiredPermission`). `GET api/crest/workflows/registry`
shows what is registered and where each shipped flow landed.

## Stock Orchard activities

The tenant's `IWorkflowManager` is this module's façade, so the upstream modules' events
(Contents, Users, Email, ...) reach the engine unchanged as `Orchard event` triggers
(`Crest.Workflows.OrchardEvent`: `EventName`, `PropertiesJson`; the stock event's own
`CanExecute` filter applies) and their tasks run through `Orchard task`
(`Crest.Workflows.OrchardTask`: `ActivityName`, `PropertiesJson`; the task's outcomes are
the ports). Stock workflow *types* are not run; definitions live here.

## Tests

`tests/playwright/checks/` (registered in the shared suite, `../../tests/playwright/run-admin-suite.js`;
a consuming host's own checks cover the registry end to end through its modules) and
`tests/Crest.Workflows.Tests` (xUnit, discovered by `../../tests/run-tests.sh`). A host
runs everything through its own test entrypoint.
