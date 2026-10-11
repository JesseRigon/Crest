# Upstream

Forked from [elsa-workflows/elsa-orchard-core](https://github.com/elsa-workflows/elsa-orchard-core)
(BSD-3-Clause, see LICENSE), cloned into Crest around 2026-09-15 (Crest commit `2f8dd4d`);
the exact upstream commit was not recorded at clone time. Upstream ids
`Crest.ElsaWorkflows*` became `Crest.Workflows*`, and the package is now
`Crest.Workflows*`: the application layer's workflow service.

**Renaming and upstream merges.** Every identifier, namespace and assembly in the vendored
subtree carries the `Crest.Workflows` name (`tools/rename-upstream.py` applies the mapping),
so Crest contains no trace of a consuming product's branding. The cost is that an upstream
Elsa change has to be applied against renamed files — run the incoming diff through the same
mapping before merging it.

**Elsa and Elsa Studio are vendored source** (2026-09-28), not packages:

- `engine/` = the 22-project closure of [elsa-workflows/elsa-core](https://github.com/elsa-workflows/elsa-core)
  at tag 3.6.0 (commit in `engine/UPSTREAM-COMMIT`; MIT): the runtime, management, API,
  HTTP, expressions, mediator, common, caching, key-values, SAS tokens, scheduling,
  tenants, resilience, features, and the API client. Same relative layout as upstream
  (`src/common`, `src/modules`, `src/clients`) so upstream project references and diffs
  still apply; `src/Directory.Build.props` pins `net10.0` only.
- `designer/` = the 15-project closure of [elsa-workflows/elsa-studio](https://github.com/elsa-workflows/elsa-studio)
  at tag 3.6.0 (commit in `designer/UPSTREAM-COMMIT`; MIT): Core, Shell, Shared,
  DomInterop, UIHints, ActivityPortProviders, Workflows, Workflows.Core,
  Workflows.Designer, Login, and the BlazorWasm/BlazorServer shims. `UseProjectReferences`
  points it at the vendored `Elsa.Api.Client`. Radzen 11 (Crest's version) and MudBlazor come from its
  own `Directory.Packages.props`; the transition to Crest components is in
  `../docs/workflows.md`.
- `Server/` and `Contents/` reference engine projects directly; no `Elsa*` package is in
  the graph. The Elsa pins were removed from the root `Directory.Packages.props`.
- Not vendored: elsa-extensions (`Elsa.Scheduling.Quartz`, `Elsa.Sql`, `Elsa.Data.Csv`),
  which only the unbuilt `reference/` projects mention.
- **Renamed by script** (`tools/rename-upstream.py`, 2026-09-28): `Elsa.Workflows.X` and
  every other `Elsa.X` → `Crest.Workflows.X` (namespaces, assembly and package ids,
  project folders, static-asset paths, the API client, the check's activity type ids);
  the meta project `Elsa` → `Crest.Workflows.Engine`; `namespace Elsa` / `using Elsa`
  / the `"Elsa"` activity-namespace literal → `Crest.Workflows` (so `Elsa.WriteLine`
  is now `Crest.Workflows.WriteLine`); `Elsa`-prefixed identifiers →
  `CrestWorkflows...`. **Second pass the same day** (rules 6-8 of the script):
  upstream repo and docs URLs → the fork's home and plan; "Elsa Workflows" → "Crest
  Workflows" and the word Elsa in comments and strings → `Crest.Workflows`; `Elsa_*`
  YesSql collection names → `CrestWorkflows_*` (a fresh tenant is required: run
  `bash dev/dev.sh full build` on a dev instance that provisioned before this);
  `Elsa` embedded in identifiers (`AddElsa`, `ConfigureElsa`) → `AddCrestWorkflows`,
  `ConfigureCrestWorkflows`; lowercase `elsa` → `crest-workflows` in the API
  route prefix (`crest-workflows/api`), CSS classes and file names, `crestWorkflows`
  as an identifier (lambda parameters, JS variables). The only Elsa tokens left in the
  tree are this note, the LICENSE files, the recorded upstream commits, and Studio's
  `Crest.Workflows.Studio.Workflows.*` stutter, accepted for now.
  To map an upstream fix: copy the upstream files over the same paths, run the script.

To pull an upstream fix: diff the upstream tag range against `engine/` or `designer/`
and apply by hand; re-check every transitive package licence (MIT / Apache-2.0 / BSD
only) when you do.

## What diverged from the port

- Built: `Server/` (engine) and `Contents/`. Everything else from the port (Timers,
  Queries, Data, UI, Designer*) is under `reference/`, unbuilt and unreferenced.
- Removed: sample hosts (`src/apps`), the port's own solutions, `Directory.*.props`,
  `NuGet.config`, the MVC designer pages/controllers/admin menu, the OpenID dependency,
  `PermissionsClaimsProvider` (granted `permissions=*` to every user), the Taxonomies
  dependency of Contents (`ResolveTerm`).
- Added: `Security/ApiSecurityMiddleware` (the API gate: Crest permission,
  antiforgery, per-request engine grant), `Contexts/*` (acting-user snapshot and
  authorizer), `Activities/RequirePermission`, per-shell file lock directory,
  `RequiredPermission` on the content triggers, acting user on every content stimulus,
  `Controllers/WorkflowTriggersController` (stored-trigger diagnostics and re-index),
  `Services/SyncDisposableTenantService` (Crest disposes containers synchronously;
  Elsa's tenant service is async-only and crashed the process on shell release).
- Fixed: `ContentItemWorkflowDefinitionPublisher.PublishAsync` stores the draft before
  publishing a brand-new definition (save with `publish=true` used to NRE); YesSql 6
  signatures in the stores (`collection:` named arguments); `Permissions` reuses the
  stock `WorkflowsPermissions.ManageWorkflows` instead of redeclaring it.
