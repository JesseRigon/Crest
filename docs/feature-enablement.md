# Feature enablement: dependencies vs. the setup recipe

When a Grove page, controller, or admin-menu entry needs an OrchardCore feature, there
are two places that need can be expressed. Picking the wrong one produces a tenant that
works on your machine and breaks on a fresh provision.

## Rule

**If Grove code cannot function without it, declare it in the manifest.** Never add it to
a recipe to "fix" a missing feature — a recipe entry only affects tenants provisioned
from that recipe, so the gap silently returns for every other host, and for anyone who
toggles features by hand.

**If it is a site-level choice about what this particular deployment runs**, it belongs
in the host's setup recipe.

## Why the manifest is the real fix

`Grove` is `IsAlwaysEnabled = true`, and Orchard's feature system refuses to
disable a feature while an enabled dependent declares it. So a correct manifest
dependency is self-healing: it auto-enables on setup, survives another recipe's
`feature.disable` list, and survives manual toggling in the admin UI. Every
constructor-injected `Grove.*` type in the Server assembly is matched by a manifest
dependency on the feature that registers it.

Note that the lazy `IRequestAccess.GetRequiredService<T>()` pattern does **not**
remove the need for a dependency. It defers the failure from startup to request time
instead of preventing it — the endpoint still throws "Unable to resolve service for type
T" when the providing feature is off.

## Features of one module

A module may ship several features ([architecture.md](architecture.md) › Modules, features and
how a module reaches the host): Option lists is `Grove.Contents.OptionLists`, a feature of
Grove.Contents, opt-in per tenant. Its startups, controllers and permissions are gated on the
feature (`[Feature]` on the startup and controller classes), and its client library is named for
the feature (`Grove.Contents.OptionLists.BlazorWasm`), so its page is served only while the feature
is enabled. A consumer declares the feature id, not the module's (`Grove.Parties`,
`Grove.Access.Members` and the Fruitful modules declare `Grove.Contents.OptionLists`).

## What is deliberately NOT a Grove dependency

- **`Grove.Infrastructure.Tenants`** — `DefaultTenantOnly = true`. A hard dependency from an
  always-enabled feature would be invalid on every non-default tenant.
  The tenants API (`TenantsApi`) is Grove.Infrastructure.Tenants' own, so it exists only where the feature
  is enabled: the default tenant.
- **`Grove.Data.Queries.Sql`** — a *provider* of `IQuerySource` over the data layer's SQL
  connections ([queries.md › Connections](queries.md#connections-the-protocol-modules-and-the-data-layer)), not a requirement.
  Grove enumerates whatever sources a tenant has and rejects unknown ones; declaring it
  would force a SQL query feature onto every tenant.
- **A host's business modules** — standalone modules that depend on Grove, not the reverse.

These are host choices, which is why the ones a host wants (its business modules,
`Grove.Infrastructure.Tenants`) are listed in its setup recipe's `feature`
step. Everything else Grove needs arrives through the dependency
graph.

## Every feature enabled: the dev recipe

The host's dev recipe enables every tenant feature, so a fresh provision is also the test that
all features coexist. That holds three rules for every feature:

- **Unconfigured infrastructure is inert, not broken.** A feature that needs external
  configuration (Redis, Azure or S3 media storage and image caches, Azure data protection,
  SignalR backplanes, ClamAV, external login providers, search back ends) logs one warning
  naming the missing setting and registers nothing that depends on it; setup, sign-in, uploads
  and the tenant's data-protection key ring keep their defaults. A setting that is present but
  invalid stays an error.
- **Migrations write schema and definitions only.** Content (list seeds, pickers attached to
  seeded lists, default records) is written in a deferred shell-scope task, which runs after
  every feature's migration ([content-engine.md](content-engine.md) › Option lists): during setup the features
  migrated later have no index tables yet.
- **Feature events do not assume tables.** `Installed`/`Enabled` events of a setup or an enable
  run before the activation that runs the migrations; a handler that needs its tables leaves the
  first run to its migration (as Sharing's index rebuild does).

## Verifying a change

A fresh provision is the only proof, because an existing tenant keeps whatever features
it was provisioned with:

```bash
bash dev/dev.sh reset
bash dev/dev.sh server        # wait for /Login -> 200
curl -s -b "<auth cookies>" http://localhost:5010/api/grove/features
```

A feature that is enabled without appearing in the recipe's `enable` list came from the
dependency graph — that is the result you want.
