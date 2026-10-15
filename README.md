# Grove

> **Please read this first.** Grove is a personal project. I am not a good programmer, and
> most of this code was vibe coded. It is not hardened, not
> security-reviewed and not stable. **If you need safe, stable code, don't use Grove** —
> use the original projects below, which are excellent and have wonderful communities
> behind them. Honestly, I hope no one actually uses it.
>
> Grove is **not affiliated with, or endorsed by,** OrchardCore, the .NET Foundation, Elsa
> Workflows, Radzen or any other project it builds on.

Grove is an **application layer**: Blazor admin, site and member shells, the content API,
Parties (people, organizations, roles), Members (member accounts, organizations, the
member portal) and Workflows (an Elsa-based workflow service with a registry, units of
work and connectors). See [docs/architecture.md](docs/architecture.md).

## Why Grove forks OrchardCore

Grove started as a set of modules on [OrchardCore](https://github.com/OrchardCMS/OrchardCore).
Over time I wanted a lot of opinionated changes — Blazor-only UIs, permissions injected
into queries, a different audit and media model — that the OrchardCore community
shouldn't have to carry. So Grove now contains a hard fork of OrchardCore, its projects beside
Grove's own, and develops it on its own. It is not meant to go back upstream.

Grove borrows heavily from several permissively licensed projects (MIT and BSD-3-Clause):
OrchardCore, Elsa, Radzen Blazor, OrchardCore.Commerce and others. Their licences and
copyright notices stay with the code — see [NOTICE.md](NOTICE.md). Grove itself is MIT, so
anything taken from here is as freely usable as what it was built from.

## Layout

One repository holds the platform and the application layer. The projects stay
independently packageable.

```text
Grove/
  src/                every project, platform and application alike
    Grove.Infrastructure/, Grove.Infrastructure.Abstractions/, Grove.Contents.Management/, Grove.Access.Users/, Grove.Media/, ...
                      the platform, forked from OrchardCore and renamed Grove.*:
                      tenants, users, permissions, content, features, settings, the
                      module system. Listed by kind in src/Grove.Infrastructure.Build/Platform.Projects.props.
    Grove.Infrastructure.Build/      the platform's build settings
    Grove.Infrastructure.Server/     the application layer's server module
    Grove.Display.Components/ the Blazor component library
    Grove.Display.Icons.Iconify/    Iconify provider
    Grove.Display.Icons/      icon registry and UI
    Grove.Parties/ Grove.Access.Members/ Grove.Workflows/ Grove.Data.Types.Money/ ...
    Grove.Themes.Admin/ Grove.Themes.Site/ Grove.Themes.Members/
  test/               the platform's tests (Grove's modules keep theirs in their own tests/ folders)
  tests/              the browser suites
  tools/              build tools (Grove.LazyModules)
  docs/               design docs and decisions
```

`Grove.Platform.slnx` builds the platform; `Grove.slnx` builds the Grove modules, which
reference the platform's projects directly (`$(PlatformSrcDir)`, the `src/` folder). Platform
and application projects sit side by side under `src/`; `src/Grove.Infrastructure.Build/Platform.Projects.props`
says which is which, and `Directory.Build.props` gives the platform projects their own build
settings.

## Project roles

`Grove.Infrastructure.Server` is the application layer's server module. It serves the active Blazor
admin shell when a Grove-compatible admin theme is selected, exposes the `api/grove/*`
JSON endpoints the Blazor shells need, and owns shared infrastructure such as legacy
frame theme selection. It calls the platform's services and enforces its permissions; it
does not keep a second copy of content, auth, menu or theme state.

`Grove.Display.Components` is the shared Radzen-derived Blazor component layer. It owns reusable
primitives, forms, model/editor UI, and client-safe UI contracts. It must not reference
feature modules.

`Grove.Display.Icons.Iconify` owns the Iconify-specific C# API, provider settings, and optional
full-library cache integration. It does not depend on `Grove.Display.Icons`.

`Grove.Display.Icons` owns the generic icon provider contract, registry/search/used-icon pack
services, tenant/media icon cache behavior, icon UI such as `IconSelector`, and
icon-specific CSS/JS/assets. It depends on `Grove.Display.Icons.Iconify` for the default Iconify provider.

Admin and site themes are composition roots. They reference `Grove.Display.Components`,
`Grove.Display.Icons`, and the feature UI modules they want compiled into the WASM app.

Application modules that build UI reference `Grove.Display.Components` explicitly. For example, a
new module `Example.BlazorWasm` would reference the components project and contribute
Blazor routes/components to the admin WASM build.

In the future, I'd like 3rd party modules to be able to call 'generic' components from the
shared components module as a standard library, so custom component libraries can recreate
them in their own style. I'd also like a standard API for modules and routes to declare
pages that Grove serves as WASM on their behalf.

## Runtime model

The platform is the system of record for tenants, users, permissions, content,
features, settings, themes, admin menus and navigation. Grove's modules build on its
services. When the platform lacks something Grove needs, the platform is changed: no
shims or parallel copies.

The end state is Blazor or headless only. The Liquid views and stock Razor/Vue admin pages
are removed as their Blazor replacements land (see
[docs/architecture.md](docs/architecture.md)).

Preferred data-access order for the Blazor shells:

1. The platform's REST/JSON APIs, when they satisfy the client contract.
2. GraphQL for content/query/read models where it fits.
3. Queries for configured reports and query-backed screens.
4. OpenID/JWT for external headless clients.
5. `api/grove/*` endpoints for Blazor-specific projections and actions, or functionality
   that is only reachable through Razor UI, services or shapes today.

## Blazor admin theme serving

`Grove.Infrastructure.Server` installs middleware that checks the selected admin theme. If it is
`Grove.Themes.Admin` or carries the `grove-blazor` manifest tag (itself or through its
`BaseTheme` chain), the middleware serves the Grove admin WASM files for admin routes and
Blazor assets.

The admin shell's assets live under `Grove.Themes.Admin/wasm`; the theme manifest project
is `Grove.Themes.Admin`.

## Component system boundary

The current UI implementation is Radzen-derived. Shared primitives belong in
`Grove.Display.Components`. Feature UI and assets belong with their feature modules; theme chrome
belongs with theme projects.

Shared runtime contracts and infrastructure should not depend on Radzen. Future component
systems should be able to reuse the server runtime, JSON contracts, route/theme
conventions without copying Radzen-specific code.

The planned neutral client/core package has not been extracted yet. Until it exists, some
client contracts and display-management ideas still live inside `Grove.Display.Components` or the
Grove admin WASM project.

## Packaging direction

The repository stays a single git repository while publishing separate NuGet packages. The
intended package boundaries are:

- The platform's packages (`Grove.*`; the implementation library is `Grove.Infrastructure`).
- `Grove.Infrastructure.Server`: the server module and shared server infrastructure.
- `Grove.Display.Components`: the component layer.
- `Grove.Display.Icons.Iconify`: Iconify provider API and optional full-library cache.
- `Grove.Display.Icons`: icon providers, icon UI, and icon-owned assets.
- Theme packages: admin/site/member composition roots.
- Future `Grove.Client`: UI-library-neutral client contracts, display manager, routing
  helpers, and the legacy frame client component.

Project files are not fully package-ready yet. Some projects still have
`IsPackable=false`; packaging metadata and dependency boundaries need to be finalized
before publishing.

## Development recipes

Reusable development recipes live under `Grove.Themes.Site/Recipes`: `GroveBasicDev` for a
focused admin shell and `GroveFullDev` for broad feature testing. Host apps keep
tenant/user autosetup recipes in the host repo.

## Validation

End-to-end validation runs in a host application (its own build and
test scripts). Browser validation uses reusable
Playwright scripts under the owning project's `tests/playwright` directory, not one-off
inline scripts.

## Licence

MIT — see [LICENSE](LICENSE).

Grove vendors, forks and adapts several permissively licensed works, each of which keeps
its own licence beside the code it covers: the platform projects (OrchardCore,
BSD-3-Clause, `LICENSE.platform`), the workflow engine and designer (Elsa), the Blazor component
library (Radzen Blazor), the money types (OrchardCore.Commerce) and the default site
theme's front-end assets. [NOTICE.md](NOTICE.md) is the inventory.
