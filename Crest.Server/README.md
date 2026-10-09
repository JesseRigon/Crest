# Crest

`Crest.Server` is the backend overlay on top of Crest for the Crest admin experience. It plugs into an Crest host, serves the Blazor-based admin shell when the Crest admin theme is active, exposes Blazor-admin-specific JSON adapters, and keeps Crest as the authority for tenants, users, permissions, content, features, settings, themes, and navigation.

`Crest.Server` should remain focused on Crest integration, middleware, controllers, services, permissions, backend adapter logic, and serving Blazor-capable theme assets.

Shared Radzen-backed UI primitives belong in `Crest.Components`. Feature UI and assets belong with their feature modules, such as `Crest.Icons`. Admin/Site themes are composition roots that reference those projects.

## Current rendering model

Crest is a **Blazor Web App** hosted from Crest: this module is the single host for every Crest shell (admin, site, member), pages prerender server-side and run `InteractiveAuto` (a server circuit on first visit, WebAssembly once cached), and the compiled Crest components call Crest or thin `api/crest/*` JSON adapters from this server module for data and actions. See [docs/blazor-web.md](../docs/blazor-web.md) and [docs/shells-and-themes.md](../docs/shells-and-themes.md).

It is not currently a Blazor Hybrid or MAUI component system. Those models are possible future directions, especially for sharing more UI across native, web, and Crest-hosted experiences.

Crest's MVC/Razor/shape system still remains in place for the Crest host and modules. Crest adapts Crest data, menus, auth, permissions, themes, and content to a WASM Blazor admin shell; it does not replace Crest's backend rendering pipeline.

## Versioning

Crest uses a five-part compatibility version:

```text
{platform-major}.{platform-minor}.{platform-patch}.{crest-security}.{crest-bug}
```

The first three parts identify the Crest version that this Crest build has been tested against. The last two parts are owned by Crest.

Examples:

- `3.0.0.0.0` - initial Crest release for Crest `3.0.0`.
- `3.0.0.0.1` - Crest bug fix for Crest `3.0.0`.
- `3.0.0.1.2` - Crest security fix `1` and bug fix `2` for Crest `3.0.0`.
- `3.0.1.2.1` - tested against Crest `3.0.1`, carrying Crest security line `2` and bug line `1`.

When the Crest compatibility line moves to a new main release, the Crest-owned counters reset to `.0.0`.

Current compatibility version: `3.0.0.0.0`.

The Crest manifests use the five-part version. .NET assembly and NuGet-compatible package properties keep their required SemVer/assembly-safe forms, with the five-part value stored as `AssemblyInformationalVersion` / `CrestVersion`.

## API strategy

Do not build a parallel admin API when Crest already provides one.

Preferred order for Crest Admin data access:

1. Use Crest's built-in JSON/REST APIs when they provide the needed contract.
2. Use Crest GraphQL for content/query/read models where the GraphQL module already fits.
3. Add or extend Crest GraphQL schema/mutations when a typed Blazor contract is useful and belongs to Crest data.
4. Keep a small `api/crest/*` adapter only when Crest exposes the behavior as MVC/Razor admin UI, service APIs, or shape/menu builders rather than a stable JSON endpoint.

Any `api/crest/*` endpoint must be a thin Blazor-admin adapter over Crest services. It must not own duplicate state, implement parallel CMS/auth concepts, or bypass Crest authorization.

```text
Crest Admin UI
  -> Crest REST/JSON API when available
  -> Crest GraphQL for content/query projections
  -> Thin Crest adapter only for Blazor-specific projections/actions
       -> calls Crest services
       -> checks Crest permissions
       -> returns Blazor-friendly JSON
```

## Controller audit

Current controllers are mostly thin adapters over Crest services. The main cleanup direction is to remove custom endpoints where a stable Crest API or GraphQL query can satisfy the Blazor UI.

| Controller | Current purpose | Preferred source | Decision |
| --- | --- | --- | --- |
| `AppController` | Blazor app manifest: tenant, site, admin base path, feature hash, enabled features, admin menu | Crest services: `ShellSettings`, `IShellDescriptorManager`, `ISiteService`, `INavigationManager` | Keep as Blazor shell adapter. No single stock Crest API provides this combined app manifest. |
| `CrestAuthController` | JSON `me`, login, logout for Blazor | Crest Users/authentication | Keep only as a Blazor auth adapter unless a stock JSON login/session endpoint is enabled and suitable. Must continue to use Crest user services/cookies, and MUST fire the standard `ILoginFormEvent` sequence exactly like the stock MVC AccountController (it does) - a veto result is TRANSLATED into the 401 payload (redirect target + TempData error_* messages), never silently skipped, so moderation/confirmation/audit/host login policies apply to Crest logins identically. |
| `CrestThemeController` | Module-specific Radzen/theme token settings from site properties | Crest site settings | Keep while these settings are Crest-specific. If the settings become a normal Crest settings section with a standard API, switch to that. |
| `ContentItemsController` | Read content item by handle | Prefer Crest GraphQL or Crest Contents API | Candidate for replacement. Blazor content reads should use GraphQL/standard content APIs where possible. Keep only as temporary compatibility or for a missing by-handle projection. |
| `ContentTypesController` | List/read content type definitions | Crest content definition services; possible GraphQL/schema projection | Audit further before expanding. Keep as a thin read adapter only if Crest has no stable JSON content-definition endpoint for the needed UI. |
| `FeaturesController` | List enabled features | Crest Features admin/services | Keep as read-only adapter for Blazor if stock feature admin remains MVC/Razor-only. Any enable/disable action should use Crest permissions and services, not custom state. |
| `NavigationController` | Build admin or named menus as JSON | `INavigationManager` | Keep. Crest navigation is built through menu services/shapes; Blazor needs a JSON projection of the resolved menu. |
| `RolesController` | List roles | Crest roles services/admin | Keep as read-only adapter only if no stock JSON role endpoint is available. Must enforce Crest role-management permissions before mutating or exposing admin-only role details. |
| `SiteController` | Read/update site settings | Crest settings services/admin | Keep only as a settings adapter if stock settings APIs are not suitable. Must enforce Crest settings permissions for reads/writes. |
| `ThemesController` | List/select/enable/disable site/admin themes | Crest themes services/admin | Keep. Crest's standard Themes admin is MVC/Razor; this adapter mirrors it as JSON and already checks `Crest.Themes.Permissions.ApplyTheme`. |

## Server/UI boundary rules

- Keep backend overlay logic in `Crest.Server`: Crest middleware, controllers, services, permissions, theme selection, and legacy frame selection.
- Keep shared UI primitives in `Crest.Components`.
- Keep feature UI and assets with the owning feature module, for example `Crest.Icons` owns icon selector UI and icon-specific CSS/JS.
- Keep Admin/Site themes as composition roots that reference components and feature modules.
- Do not add Radzen or browser-specific component code to `Crest.Server`.
- Do not add Crest server dependencies or direct CMS state management to browser component projects.

## Rules for adding endpoints

Before adding a new `api/crest/*` endpoint:

1. Check whether Crest already exposes a JSON/REST API for the feature.
2. Check whether GraphQL can query or mutate the data cleanly.
3. If a Crest endpoint is still needed, keep it projection-oriented and call Crest services directly.
4. Use Crest permission constants and `IAuthorizationService` for admin-level data/actions.
5. Avoid host-specific names, tenants, credentials, recipes, or local dev settings in this module.

## Near-term API cleanup

- Replace `ContentItemsController` reads with GraphQL or Crest Contents API if the enabled Crest modules provide the required by-handle lookup.
- Verify whether Crest 3.0 exposes stable JSON endpoints for content definitions, roles, site settings, and features in the host configuration.
- Add explicit authorization checks to all admin adapters that expose settings, roles, features, content definitions, or content data.
- Keep `ThemesController` as the pattern for Blazor admin adapters: Crest service calls plus Crest permission checks, returning only UI-friendly JSON.
