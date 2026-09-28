# Upstream

Forked from [elsa-workflows/elsa-orchard-core](https://github.com/elsa-workflows/elsa-orchard-core)
(BSD-3-Clause, see LICENSE), cloned into Crest around 2026-09-15 (Crest commit `2f8dd4d`);
the exact upstream commit was not recorded at clone time. Upstream ids
`OrchardCore.ElsaWorkflows*` became `OrchardCore.Crest.Workflows*`.

Elsa and Elsa Studio are **not** vendored: they are pinned NuGet packages (versions in
`modules/OrchardCore.Crest/Directory.Packages.props`, "Elsa" group). Bump on purpose, and
re-check every package's licence (MIT / Apache-2.0 / BSD only) when you do.

## What diverged (2026-09-28, plans/workflows.md phase 0a)

- Built: `src/modules/OrchardCore.Crest.Workflows` (engine) and `.Contents`. Everything
  else under `src/modules` (Timers, Queries, Data, UI, Designer*) is kept for reference,
  unbuilt and unreferenced, until its phase.
- Removed: sample hosts (`src/apps`), the port's own solutions, `Directory.*.props`,
  `NuGet.config`, the MVC designer pages/controllers/admin menu, the OpenID dependency,
  `PermissionsClaimsProvider` (granted `permissions=*` to every user), the Taxonomies
  dependency of Contents (`ResolveTerm`).
- Added: `Security/ElsaApiSecurityMiddleware` (the API gate: Orchard permission,
  antiforgery, per-request Elsa grant), `Contexts/*` (acting-user snapshot and
  authorizer), `Activities/RequirePermission`, per-shell file lock directory,
  `RequiredPermission` on the content triggers, acting user on every content stimulus,
  `Controllers/WorkflowTriggersController` (stored-trigger diagnostics and re-index),
  `Services/SyncDisposableTenantService` (Orchard disposes containers synchronously;
  Elsa's tenant service is async-only and crashed the process on shell release).
- Fixed: `ContentItemWorkflowDefinitionPublisher.PublishAsync` stores the draft before
  publishing a brand-new definition (save with `publish=true` used to NRE); YesSql 6
  signatures in the stores (`collection:` named arguments); `Permissions` reuses the
  stock `WorkflowsPermissions.ManageWorkflows` instead of redeclaring it.
