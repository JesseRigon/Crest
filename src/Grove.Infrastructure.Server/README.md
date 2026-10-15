# Grove

`Grove.Infrastructure.Server` is the backend overlay on top of Grove for the Grove admin experience. It plugs into an Grove host, serves the Blazor-based admin shell when the Grove admin theme is active, exposes Blazor-admin-specific JSON adapters, and keeps Grove as the authority for tenants, users, permissions, content, features, settings, themes, and navigation.

`Grove.Infrastructure.Server` should remain focused on Grove integration, middleware, controllers, services, permissions, backend adapter logic, and serving Blazor-capable theme assets.

Shared Radzen-backed UI primitives belong in `Grove.Display.Components`. Feature UI and assets belong with their feature modules, such as `Grove.Display.Icons`. Admin/Site themes are composition roots that reference those projects.

## Current rendering model

Grove is a **Blazor Web App** hosted from Grove: this module is the single host for every Grove shell (admin, site, member), pages prerender server-side and run `InteractiveAuto` (a server circuit on first visit, WebAssembly once cached), and the compiled Grove components call Grove or thin `api/grove/*` JSON adapters from this server module for data and actions. See [docs/blazor-web.md](../docs/blazor-web.md) and [docs/shells-and-themes.md](../docs/shells-and-themes.md).

It is not currently a Blazor Hybrid or MAUI component system. Those models are possible future directions, especially for sharing more UI across native, web, and Grove-hosted experiences.

Grove's MVC/Razor/shape system still remains in place for the Grove host and modules. Grove adapts Grove data, menus, auth, permissions, themes, and content to a WASM Blazor admin shell; it does not replace Grove's backend rendering pipeline.

## Versioning

Grove uses a five-part compatibility version:

```text
{platform-major}.{platform-minor}.{platform-patch}.{grove-security}.{grove-bug}
```

The first three parts identify the Grove version that this Grove build has been tested against. The last two parts are owned by Grove.

Examples:

- `3.0.0.0.0` - initial Grove release for Grove `3.0.0`.
- `3.0.0.0.1` - Grove bug fix for Grove `3.0.0`.
- `3.0.0.1.2` - Grove security fix `1` and bug fix `2` for Grove `3.0.0`.
- `3.0.1.2.1` - tested against Grove `3.0.1`, carrying Grove security line `2` and bug line `1`.

When the Grove compatibility line moves to a new main release, the Grove-owned counters reset to `.0.0`.

Current compatibility version: `3.0.0.0.0`.

The Grove manifests use the five-part version. .NET assembly and NuGet-compatible package properties keep their required SemVer/assembly-safe forms, with the five-part value stored as `AssemblyInformationalVersion` / `GroveVersion`.

## API strategy

Do not build a parallel admin API when Grove already provides one.

Preferred order for Grove Admin data access:

1. Use Grove's built-in JSON/REST APIs when they provide the needed contract.
2. Use Grove GraphQL for content/query/read models where the GraphQL module already fits.
3. Add or extend Grove GraphQL schema/mutations when a typed Blazor contract is useful and belongs to Grove data.
4. Keep a small `api/grove/*` adapter only when Grove exposes the behavior as MVC/Razor admin UI, service APIs, or shape/menu builders rather than a stable JSON endpoint.

Any `api/grove/*` endpoint must be a thin Blazor-admin adapter over Grove services. It must not own duplicate state, implement parallel CMS/auth concepts, or bypass Grove authorization.

```text
Grove Admin UI
  -> Grove REST/JSON API when available
  -> Grove GraphQL for content/query projections
  -> Thin Grove adapter only for Blazor-specific projections/actions
       -> calls Grove services
       -> checks Grove permissions
       -> returns Blazor-friendly JSON
```

## Controller audit

Current controllers are mostly thin adapters over Grove services. The main cleanup direction is to remove custom endpoints where a stable Grove API or GraphQL query can satisfy the Blazor UI.

| Controller | Current purpose | Preferred source | Decision |
| --- | --- | --- | --- |
| `AppController` | Blazor app manifest: tenant, site, admin base path, feature hash, enabled features, admin menu | Grove services: `ShellSettings`, `IShellDescriptorManager`, `ISiteService`, `INavigationManager` | Keep as Blazor shell adapter. No single stock Grove API provides this combined app manifest. |
| `AuthController` | JSON `me`, login, logout for Blazor | Grove Users/authentication | Keep only as a Blazor auth adapter unless a stock JSON login/session endpoint is enabled and suitable. Must continue to use Grove user services/cookies, and MUST fire the standard `ILoginFormEvent` sequence exactly like the stock MVC AccountController (it does) - a veto result is TRANSLATED into the 401 payload (redirect target + TempData error_* messages), never silently skipped, so moderation/confirmation/audit/host login policies apply to Grove logins identically. |
| `ThemeController` | Module-specific Radzen/theme token settings from site properties | Grove site settings | Keep while these settings are Grove-specific. If the settings become a normal Grove settings section with a standard API, switch to that. |
| `ContentItemsController` | Read content item by handle | Prefer Grove GraphQL or Grove Contents API | Candidate for replacement. Blazor content reads should use GraphQL/standard content APIs where possible. Keep only as temporary compatibility or for a missing by-handle projection. |
| `ContentTypesController` | List/read content type definitions | Grove content definition services; possible GraphQL/schema projection | Audit further before expanding. Keep as a thin read adapter only if Grove has no stable JSON content-definition endpoint for the needed UI. |
| `FeaturesController` | List enabled features | Grove Features admin/services | Keep as read-only adapter for Blazor if stock feature admin remains MVC/Razor-only. Any enable/disable action should use Grove permissions and services, not custom state. |
| `NavigationController` | Build admin or named menus as JSON | `INavigationManager` | Keep. Grove navigation is built through menu services/shapes; Blazor needs a JSON projection of the resolved menu. |
| `RolesController` | List roles | Grove roles services/admin | Keep as read-only adapter only if no stock JSON role endpoint is available. Must enforce Grove role-management permissions before mutating or exposing admin-only role details. |
| `SiteController` | Read/update site settings | Grove settings services/admin | Keep only as a settings adapter if stock settings APIs are not suitable. Must enforce Grove settings permissions for reads/writes. |
| `ThemesController` | List/select/enable/disable site/admin themes | Grove themes services/admin | Keep. Grove's standard Themes admin is MVC/Razor; this adapter mirrors it as JSON and already checks `Grove.Themes.Permissions.ApplyTheme`. |

## Server/UI boundary rules

- Keep backend overlay logic in `Grove.Infrastructure.Server`: Grove middleware, controllers, services, permissions, theme selection, and legacy frame selection.
- Keep shared UI primitives in `Grove.Display.Components`.
- Keep feature UI and assets with the owning feature module, for example `Grove.Display.Icons` owns icon selector UI and icon-specific CSS/JS.
- Keep Admin/Site themes as composition roots that reference components and feature modules.
- Do not add Radzen or browser-specific component code to `Grove.Infrastructure.Server`.
- Do not add Grove server dependencies or direct CMS state management to browser component projects.

## Rules for adding endpoints

Before adding a new `api/grove/*` endpoint:

1. Check whether Grove already exposes a JSON/REST API for the feature.
2. Check whether GraphQL can query or mutate the data cleanly.
3. If a Grove endpoint is still needed, keep it projection-oriented and call Grove services directly.
4. Use Grove permission constants and `IAuthorizationService` for admin-level data/actions.
5. Avoid host-specific names, tenants, credentials, recipes, or local dev settings in this module.

## Near-term API cleanup

- Replace `ContentItemsController` reads with GraphQL or Grove Contents API if the enabled Grove modules provide the required by-handle lookup.
- Verify whether Grove 3.0 exposes stable JSON endpoints for content definitions, roles, site settings, and features in the host configuration.
- Add explicit authorization checks to all admin adapters that expose settings, roles, features, content definitions, or content data.
- Keep `ThemesController` as the pattern for Blazor admin adapters: Grove service calls plus Grove permission checks, returning only UI-friendly JSON.
