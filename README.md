# Crest

> **Please read this first.** Crest is a personal project. I am not a good programmer, and
> most of this code was written with AI assistance ("vibe coded"). It is not hardened, not
> security-reviewed and not stable. **If you need safe, stable code, don't use Crest** —
> use the original projects below, which are excellent and have wonderful communities
> behind them. Honestly, I hope no one actually uses it.
>
> Crest is **not affiliated with, or endorsed by,** OrchardCore, the .NET Foundation, Elsa
> Workflows, Radzen or any other project it builds on.

Crest is an **application layer**: Blazor admin, site and member shells, the content API,
Parties (people, organizations, roles), Members (member accounts, organizations, the
member portal) and Workflows (an Elsa-based workflow service with a registry, units of
work and connectors). See [docs/architecture.md](docs/architecture.md).

## Why Crest forks OrchardCore

Crest started as a set of modules on [OrchardCore](https://github.com/OrchardCMS/OrchardCore).
Over time I wanted a lot of opinionated changes — Blazor-only UIs, permissions injected
into queries, a different audit and media model — that the OrchardCore community
shouldn't have to carry. So Crest now contains a hard fork of OrchardCore under `src/` and
`test/` and develops it on its own. It is not meant to go back upstream.

Crest borrows heavily from several permissively licensed projects (MIT and BSD-3-Clause):
OrchardCore, Elsa, Radzen Blazor, OrchardCore.Commerce and others. Their licences and
copyright notices stay with the code — see [NOTICE.md](NOTICE.md). Crest itself is MIT, so
anything taken from here is as freely usable as what it was built from.

The repository is intentionally kept together for source management, but its projects are meant to remain independently packageable later.

```text
Crest/
  Crest.Server/
  Crest.Components/
  Crest.Iconify/
  Crest.Icons/
  Crest.AdminTheme/
  Crest.SiteTheme/
```

## Project Roles

`Crest.Server` is the backend overlay on top of Orchard Core. It integrates with Orchard, serves the active Blazor admin shell when a Crest-compatible admin theme is selected, exposes thin `api/crest/*` JSON adapters for Blazor admin needs, and owns shared Orchard-side infrastructure such as legacy frame theme selection. It should call Orchard services, enforce Orchard permissions, and avoid owning duplicate CMS/auth/menu/theme state.

`Crest.Components` is the shared Radzen-backed Blazor component layer. It owns reusable primitives, forms, model/editor UI, and client-safe UI contracts. It must not reference feature modules.

`Crest.Iconify` owns the Iconify-specific C# API, provider settings, and optional full-library cache integration. It does not depend on `Crest.Icons`.

`Crest.Icons` owns the generic icon provider contract, registry/search/used-icon pack services, tenant/media icon cache behavior, icon UI such as `IconSelector`, and icon-specific CSS/JS/assets. It depends on `Crest.Iconify` for the default Iconify provider.

Admin and Site themes are composition roots. They reference `Crest.Components`, `Crest.Icons`, and other feature UI modules they want compiled into the WASM app.

Application modules that build UI for the current Radzen line reference `Crest.Components` explicitly. For example, a new module `Example.BlazorWasm` would reference the components project and contributes Blazor routes/components to the admin WASM build.

In the future, I'd like 3rd party modules to be able to call a 'generic' components from the shared components module as a standard library. This would enable custom component libraries to recreate them in their own style.

Another future feature I'd like to implement in the future is a standard API for modules/routes to declare and for Orchard Crest UI Framework to serve as WASM on their behalf.

## Runtime Model

Orchard remains the system of record for tenants, users, permissions, content, features, settings, themes, admin menus, and navigation.

The Crest server module does not replace Orchard's APIs or rendering system. It is a backend adapter/overlay that uses Orchard services directly and exposes Blazor-friendly JSON only where Orchard's stock API surface is not enough for the client shell.

Preferred data-access order:

1. Use Orchard's built-in REST/JSON APIs when they satisfy the client contract.
2. Use Orchard GraphQL for content/query/read models where it fits.
3. Use Orchard Query API for configured reports and query-backed screens.
4. Use OpenID/JWT for external headless clients.
5. Add thin `api/crest/*` adapters only for Blazor-specific projections/actions or Orchard functionality exposed only through MVC/Razor UI, services, or shapes.

## Blazor Admin Theme Serving

`Crest.Server` installs middleware that checks the selected Orchard admin theme. If the selected admin theme is `Crest.AdminTheme` or carries the `crest-blazor` manifest tag (itself or through its `BaseTheme` chain), the middleware serves the Crest admin WASM files for admin routes and Blazor assets.

The current admin shell assets still live under:

```text
Crest.AdminTheme/wasm
```

The Orchard-loadable admin theme manifest project still lives at:

```text
Crest.AdminTheme
```

## Component System Boundary

The current concrete UI implementation is Radzen-based. Shared Radzen-backed primitives belong in `Crest.Components`. Feature UI and assets belong with their feature modules; theme chrome belongs with theme projects.

Shared runtime contracts and infrastructure should not depend on Radzen. Future component systems should be able to reuse the Orchard-side server runtime, JSON contracts, route/theme conventions, and legacy frame infrastructure without copying Radzen-specific code.

The planned neutral client/core package has not been extracted yet. Until it exists, some client contracts and display-management ideas still live inside `Crest.Components` or the Crest admin WASM project.

## Legacy Frame System

Legacy framing is shared Crest infrastructure. It exists so normal Orchard admin pages can render inside the Crest admin shell when no native Blazor route exists.

The Orchard-side legacy frame pieces live in `Crest.Server`:

```text
Crest.Server/LegacyFrameThemeSelector.cs
Crest.Server/Themes/Crest.LegacyFrame
```

Requests with `legacy-frame=1` or `legacy-frame=true` use the stripped `Crest.LegacyFrame` admin theme. That theme keeps Orchard admin resources available while hiding the normal admin chrome so the page can sit inside an iframe.

The current iframe UI is still implemented inside the Radzen admin shell. A future neutral client package should own the reusable iframe component and URL-building behavior.

## Packaging Direction

The repository can remain a single git repository while publishing separate NuGet packages. The intended package boundaries are:

- `Crest.Server`: Orchard runtime module and shared server infrastructure.
- `Crest.Components`: shared Radzen-backed component layer.
- `Crest.Iconify`: Iconify provider API and optional full-library cache.
- `Crest.Icons`: icon providers, icon UI, and icon-owned assets.
- Theme packages/projects: admin/site composition roots that reference components and feature modules.
- Future `Crest.Client`: UI-library-neutral client contracts, display manager, routing helpers, and legacy frame client component.

Project files are not fully package-ready yet. Some projects still have `IsPackable=false`; packaging metadata and dependency boundaries need to be finalized before publishing.

## Development Recipes

Reusable Crest development recipes live under `Crest.SiteTheme/Recipes`: `CrestBasicDev` for a focused Crest admin shell and `CrestFullDev` for broad Orchard/Crest feature testing. Host apps should keep tenant/user autosetup recipes in the host repo.

## Validation

Use the host application for end-to-end validation:

```bash
dotnet build OrchardCore.Crest.Host.csproj --no-restore
```

Browser validation should use reusable Playwright scripts under the owning project's `tests/playwright` directory, not one-off inline scripts.

## Licence

MIT — see [LICENSE](LICENSE).

Crest vendors, forks and adapts several permissively licensed works, each of which keeps
its own licence beside the code it covers: the platform under `src/` and `test/`
(OrchardCore, BSD-3-Clause), the workflow engine and designer (Elsa), the Blazor component
library (Radzen Blazor), the money types (OrchardCore.Commerce) and the default site
theme's front-end assets. [NOTICE.md](NOTICE.md) is the inventory.
