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

## What is deliberately NOT a Grove dependency

- **`Grove.Tenants`** — `DefaultTenantOnly = true`. A hard dependency from an
  always-enabled feature would be invalid on every non-default tenant.
  The tenants API (`TenantsApi`) is Grove.Tenants' own, so it exists only where the feature
  is enabled: the default tenant.
- **`Grove.Queries.Sql`** — a *provider* of `IQuerySource` over the data layer's SQL
  connections ([queries.md › Connections](queries.md#connections-the-protocol-modules-and-the-data-layer)), not a requirement.
  Grove enumerates whatever sources a tenant has and rejects unknown ones; declaring it
  would force a SQL query feature onto every tenant.
- **A host's business modules** — standalone modules that depend on Grove, not the reverse.

These are host choices, which is why the ones a host wants (its business modules,
`Grove.Tenants`) are listed in its setup recipe's `feature`
step. Everything else Grove needs arrives through the dependency
graph.

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
