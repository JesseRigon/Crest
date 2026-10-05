# OrchardCore.Crest.Components

`OrchardCore.Crest.Components` is the shared Radzen-backed Blazor component layer for Orchard Crest UI Framework. It owns reusable client-side primitives, forms, model/editor UI, shared client models, and experimental shape/model helpers.

Components render under `InteractiveAuto` (SSR prerender, a server circuit on first visit, WebAssembly once cached — see [docs/blazor-web.md](../docs/blazor-web.md)) and call Orchard or thin `api/crest/*` adapters exposed by `OrchardCore.Crest.Server`; Orchard remains the backend authority for tenants, users, permissions, content, features, settings, themes, and navigation.

Boundary rule: shared Radzen-backed UI primitives belong here; backend integration belongs in `OrchardCore.Crest.Server`; feature-specific UI belongs with the feature module that owns it. This project must not reference feature modules such as `OrchardCore.Crest.Icons` or line-of-business modules.

## Project layout

```text
OrchardCore.Crest.Components/
  Components/                    # reusable Blazor components
    <Component>/                 # each component with its .razor and companions
    Utilities/                   # shared cross-component implementation
  Models/                        # client-safe DTOs and request models
  Shapes/                        # early client-side shape/model experiments
  RadzenSource/                  # complete MIT Radzen source baseline; not compiled
```

`RadzenSource/` retains only the five intentionally isolated duplicate/reference
components. The owned Crest component source lives under `Components/`, with shared
cross-component implementation in `Components/Utilities/`. Crest no longer
uses the `Radzen.Blazor` NuGet package. Its initial intended behavioral divergence
is replacing the Radzen Material icon font with Crest/Iconify icon references.

## Theme boundary

Admin and Site themes are composition roots. They should reference `OrchardCore.Crest.Components`, `OrchardCore.Crest.Icons`, and other module UI assemblies they need.

Reusable layout and navigation mechanics belong in this project. Theme-specific components should pass options into these primitives rather than hard-coding rail widths, indentation, animation timings, or compact behavior in feature CSS.

## Design tokens

Shared components consume the canonical `--crest-*` primitive token contract; they do not consume Admin or Radzen token names directly. In place today:

- packaged default primitive token CSS (`OrchardCore.Crest.Admin/wasm/wwwroot/CrestAdmin.DesignSystem.Default.css`), loaded by the admin document and applied via the shell;
- shared component cleanup so primitives do not consume Admin or Radzen token names directly;
- Admin shell/primary-navigation/titlebar/shared tab components moved toward canonical `--crest-*` tokens;
- a Design System page (`wasm/Pages/DesignSystem.razor`) + `CrestThemeSettings` covering a small fixed token set (Radzen theme, 4 colors, radius) via Site Settings.

The default file defines the canonical primitive Crest token names. It may use the active Radzen variables as source values for the packaged default mapping, but only the `--crest-*` token names are consumed by Crest primitives.

Example:

```css
:root,
.admin-shell {
  --crest-color-accent-1: var(--rz-primary, #2f6f4e);
  --crest-color-surface-1: var(--rz-base-background-color, #fff);
  --crest-color-text-1: var(--rz-text-color, #1f2937);
  --crest-radius-md: var(--rz-border-radius, 4px);
}
```

Primitive components should not know Radzen or Admin token names. They consume the canonical Crest primitive token contract directly for normal design values.

Themes and design-system documents provide concrete values by setting the canonical `--crest-*` variables on the shell/root:

```text
Design system content item or packaged default
  -> canonical --crest-* primitive variables
  -> component scoped CSS
```

Do not create component-local aliases when the value is a direct one-to-one primitive token use. For example:

```css
.primary-nav-menu {
  background: var(--crest-color-surface-1);
  border-right: var(--crest-border-size-xs) solid var(--crest-color-border-1);
}
```

Use component-local variables only for true component mechanics or derived values:

```css
.primary-nav-menu {
  --primary-nav-menu-expanded-width: 18rem;
  --primary-nav-menu-compact-width: 4rem;
}
```

Tenant and user design systems stored as Orchard content are planned in [design-systems.md](../docs/design-systems.md).

## Admin master-detail layout standard

Every list-plus-detail admin screen follows it: the page root fills the admin content area (`height: 100%`) and never scrolls as a whole; the master-detail region is a plain flex row (`flex: 1`, `min-height: 0`).

- Left pane: **`CrestModelList`** — title, create button and search are fixed; only
  the items region scrolls. Pages cap it at `MaxWidth="350px"`.
- Right pane: **`CrestDetailPane`** — a fixed `Header` (title, badges, the record's
  main settings; horizontally scrollable if wide, flattens away when absent) over one
  scrollable body that assumes nothing about its contents (grids, fieldsets, multiple
  sections).
- Both use a plain scoped root div + `::deep` internally: a class placed on a child
  component (CrestCard) never receives Blazor's CSS scope attribute, so styling the
  card directly is dead CSS.

Used by the Content Part Lists, Content Parts and Content Types pages.

## Feature UI boundary

Feature modules own their larger UI components and assets.

For example, `OrchardCore.Crest.Icons` owns `IconSelector`, icon registry/search UI, and icon-specific CSS/JS. Components may provide shared primitives used by those modules, but it should not reach into them.

## Module component convention

Third-party Orchard modules can currently contribute Crest admin UI at build time by adding a WASM project shaped like:

```text
modules/{ModuleName}/blazor-wasm/*.csproj
```

The admin WASM project discovers these projects, references them, generates a module assembly registry, and passes those assemblies to the Blazor router as additional assemblies. This is a build-time convention today, not a finalized runtime plugin API. Member and public pages go in separate `member-wasm/` and `site-wasm/` libraries — see [docs/shells-and-themes.md](../docs/shells-and-themes.md); hosting details are in [docs/blazor-web.md](../docs/blazor-web.md).

Future work should formalize this into a module manifest/registry model so enabled Orchard modules can declare routes, assemblies, scripts, styles, editor components, and permissions more explicitly. A later runtime lane may serve compiled WASM module bundles from `App_Data/wasm/{module}`.

## Versioning

Orchard Crest UI Framework uses a five-part compatibility version:

```text
{orchard-major}.{orchard-minor}.{orchard-patch}.{crest-security}.{crest-bug}
```

The first three parts identify the Orchard Core version tested with this build. The last two parts are Orchard Crest UI Framework's security and bug-fix counters.

Current compatibility version: `3.0.0.0.0`.
