# OrchardCore.Crest.Workflows

Elsa 3 as the tenant's workflow engine, integrated the way `OrchardCore.Workflows` is:
per-shell YesSql stores, definitions as content items, Orchard permissions on the API.
Plan and rulings: `plans/workflows.md` in the Fruitful host repo. Origin: `UPSTREAM.md`.

## Features

- `OrchardCore.Crest.Workflows` — the engine and Elsa's HTTP API (`~/elsa/api`) inside
  the tenant. Depends on `OrchardCore.Workflows` (the upstream modules' workflow startups
  are gated on that id) and `OrchardCore.Contents`.
- `OrchardCore.Crest.Workflows.Http` — Elsa HTTP endpoint/request activities.
- `OrchardCore.Crest.Workflows.Contents` — content triggers and tasks.

## Security model

- **Who may call the API:** `ElsaApiSecurityMiddleware`. Anonymous → 401; authenticated
  without Orchard's `ManageWorkflows` → 403 (evaluated per request through Orchard's
  authorization pipeline, so every module's `IAuthorizationHandler` applies — Fruitful's
  member ceiling lists `ManageWorkflows`); non-GET without a valid antiforgery token →
  400. Only then does the request get Elsa's `permissions=*` grant, on a per-request
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

## Tests

`tests/playwright/checks/workflows-api.js` (in the Crest shared admin suite) and
`tests/OrchardCore.Crest.Workflows.Tests` (xUnit). Run everything with
`bash dev/dev.sh test` from the host.
