# Crest.Workflows

The workflow service of the Crest application layer, and the registry every module
contributes its triggers, activities, hook slots and flows to. Elsa 3, integrated the way
`Crest.Workflows.Platform` is: per-shell YesSql stores, definitions as content items,
Crest permissions on the API. How it works: `../docs/workflows.md`; design not yet built
and rulings: `../docs/workflows.md`. Origin: `UPSTREAM.md`.

Layout: `Server/` (engine, feature `Crest.Workflows`), `Contents/` (content
triggers and tasks, feature `Crest.Workflows.Contents`), `Domain/` (the contracts modules
bind to), `engine/` and `designer/` (vendored Elsa and Elsa Studio), `blazor-wasm/` and
`studio-host/` (the admin pages and the lazily loaded Studio), `tests/`, and `reference/`
(the port's other modules, kept unbuilt for diffing).

## Features

- `Crest.Workflows` — the engine and the engine's HTTP API (`~/crest-workflows/api`) inside
  the tenant. Depends on `Crest.Workflows.Platform` (the upstream modules' workflow startups
  are gated on that id), `Crest.Contents` and `Crest`.
- `Crest.Workflows.Http` — Elsa HTTP endpoint/request activities.
- `Crest.Workflows.Contents` — content triggers and tasks.

## Security model

- **Who may call the API:** `CrestWorkflowsApiSecurityMiddleware`. Anonymous → 401; authenticated
  without *View workflows* → 403 (evaluated per request through Crest's
  authorization pipeline, so every module's `IAuthorizationHandler` applies — the member
  permission ceiling included); non-GET without a valid antiforgery token →
  400. Only then does the request get the engine permission names its Crest workflow
  permissions map to (`EnginePermissions`), on a per-request identity, never in the cookie.
- **Tenant isolation:** one Elsa per shell — services, stores (tenant `ISession`),
  hosted services and the file lock directory (`<tenant App_Data>/locks`) are all the
  shell's own. Nothing addresses another tenant.
- **Acting user:** every content stimulus carries an `Actor` input
  (`WorkflowUserContext`: id, name, tenant, claims snapshot). Triggers can require a
  permission (`RequiredPermission`) and the `RequirePermission` activity gates any flow;
  both evaluate the snapshot through Crest's real `IAuthorizationService`.
- **Activities run as system code** once a definition is published — the gate is on
  authoring, as in the stock module.

## The registry (Domain)

A consuming module depends on `Crest.Workflows.Domain` and registers:

- `IWorkflowTriggerProvider` — the triggers its registry raises (`party.role-created`).
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

## Stock Crest activities

The tenant's `IWorkflowManager` is this module's façade, so the upstream modules' events
(Contents, Users, Email, ...) reach the engine unchanged as `Crest event` triggers
(`Crest.Workflows.PlatformEvent`: `EventName`, `PropertiesJson`; the stock event's own
`CanExecute` filter applies) and their tasks run through `Crest task`
(`Crest.Workflows.PlatformTask`: `ActivityName`, `PropertiesJson`; the task's outcomes are
the ports). Stock workflow *types* are not run; definitions live here.

## Tests

`tests/playwright/checks/` (registered in the shared suite, `../tests/playwright/run-admin-suite.js`;
a consuming host's own checks cover the registry end to end through its modules) and
`tests/Crest.Workflows.Tests` (xUnit, discovered by `../tests/run-tests.sh`). A host
runs everything through its own test entrypoint.
