# Orchard Crest design system plan

This plan defines the direction for tenant and user design systems in Orchard Crest UI Framework using Orchard as the system of record. The canonical `--crest-*` primitive token contract, its packaged default CSS and the current Site-Settings-backed Design System page are built: see [Crest.Components/README.md](../Crest.Components/README.md) › Design tokens.

Planned:

- Orchard content type and content-part storage;
- tenant/user selection and resolution;
- permissions and publishing workflow;
- recipe/deployment integration;
- full Design System editor.

## Content model

### Core decision

Design systems should be Orchard content, not Crest-only JSON files.

Use a first-party Orchard content type for design systems so Orchard owns:

- persistence;
- versioning/drafts/publishing;
- ownership;
- permissions;
- tenant scope;
- import/export through recipes and deployment plans;
- audit/history where enabled;
- search/indexing where enabled.

Crest.AdminTheme should provide the Blazor editor, preview, selector, and token application layer. It should not create a parallel storage or permission model.

- [ ] **Define token taxonomy and content part model.** Suggested `CrestDesignSystemPart` fields/properties:

  ```json
  {
    "name": "Default",
    "description": "Default Orchard Crest admin design system.",
    "scope": "Tenant | User | Shared",
    "target": "Admin | Site | Both",
    "baseSystemContentItemId": null,
    "isTenantDefault": false,
    "isUserDefault": false,
    "isPublishedToTenant": false,
    "tokens": {
      "color.accent.1": "...",
      "color.accent.2": "...",
      "color.surface.1": "...",
      "color.surface.2": "...",
      "color.text.1": "...",
      "color.text.muted": "...",
      "color.border.1": "...",
      "border.size.xs": "...",
      "radius.sm": "...",
      "radius.md": "...",
      "space.sm": "...",
      "font.size.md": "..."
    },
    "metadata": {
      "version": 1,
      "createdByCrestVersion": "3.0.0.0.0"
    }
  }
  ```

  The exact JSON shape can evolve, but the important rule is that the stored document describes Crest-owned primitive/semantic tokens, not direct Radzen token names and not component implementation variables.

  Component-specific variables such as primary-navigation width, panel-menu rail width, indentation, or animation timing remain component mechanics. They are configured by component parameters/settings when needed, not by the primitive token contract.

- [ ] **Add `CrestDesignSystem` content type/part and permissions.** Create a content type:

  ```text
  CrestDesignSystem
  ```

  Suggested parts:

  - `TitlePart`
  - `AliasPart`
  - `AutoroutePart` optional, mostly useful if design systems get public previews later
  - `OwnerEditor` / ownership through Orchard content ownership
  - custom `CrestDesignSystemPart`

  Use Orchard permissions. Do not add Crest-local role checks.

  Suggested permissions:

  - `ManageCrestDesignSystems`
  - `ViewCrestDesignSystems`
  - `CreateCrestDesignSystems`
  - `EditOwnCrestDesignSystems`
  - `EditTenantCrestDesignSystems`
  - `PublishCrestDesignSystems`
  - `SetTenantDefaultCrestDesignSystem`
  - `SetOwnCrestDesignSystem`

  Where possible, map to or compose with Orchard content permissions rather than duplicating them. If Orchard content permissions are sufficient for a step, use them directly.

- [ ] **Seed packaged default into tenant content via recipe/setup.** Keep a static CSS file such as:

  ```text
  Crest.AdminTheme/wasm/wwwroot/CrestAdmin.DesignSystem.Default.css
  ```

  Purpose:

  - fresh-install fallback;
  - cross-tenant default seed;
  - readable reference for the default token map;
  - emergency fallback if tenant content is unavailable.

  Non-purpose:

  - not the tenant source of truth;
  - not where tenant/user overrides live;
  - not the runtime-only customization model.

  On setup, Crest can seed a `CrestDesignSystem` content item from this default token map. After setup, tenant/user choices come from Orchard content.

  Recipes can create the default `CrestDesignSystem` content item on setup.

  - [x] The default file's token contract is built: see [Crest.Components/README.md](../Crest.Components/README.md) › Design tokens.

## Resolution and runtime

### Scope model

#### System fallback

Static packaged defaults loaded from Crest.AdminTheme.

Used only when no tenant/user design system can be resolved.

#### Tenant design systems

Tenant-scoped Orchard content items.

Tenant admins can create, edit, publish, archive, and set tenant defaults according to Orchard permissions.

Multiple tenant design systems should be allowed, for example:

- Default Admin;
- Compact Admin;
- High Contrast;
- Seasonal;
- Department-specific.

- [ ] **Add resolver service for current user/tenant design system.** When building the admin shell manifest, resolve design system in this order:

  1. Authenticated user's selected personal design system, if valid and permissioned.
  2. Authenticated user's selected shared tenant design system, if valid and permissioned.
  3. Tenant default design system content item.
  4. Crest packaged default design system.

  Every resolution step must verify Orchard content permissions for the current user.

- [ ] **Add the full content-item design-system payload to the admin manifest.** Crest.AdminTheme should convert the selected design system content item into a compact client payload:

  ```json
  {
    "contentItemId": "...",
    "versionId": "...",
    "name": "Default",
    "tokens": {},
    "componentSlots": {}
  }
  ```

  The admin shell applies this payload by setting CSS custom properties on the admin root element. How components consume those `--crest-*` variables is built: see [Crest.Components/README.md](../Crest.Components/README.md) › Design tokens.

  **Token source mapping.** Keep a provider pattern only at the source-to-Crest-token boundary.

  Examples:

  - Radzen theme variables to Crest primitive tokens.
  - MudBlazor theme variables to Crest primitive tokens.
  - Bootstrap variables to Crest primitive tokens.
  - A stored `CrestDesignSystem` content item to Crest primitive tokens.

  The provider does not style individual components and does not emit primary-navigation/panel-menu implementation variables. Its output is the canonical primitive token contract.

## Admin UI and user workflow

- [ ] **Build Blazor Design System page over Orchard content services.** Add or extend the Design System page in Crest.AdminTheme.

  Primary areas:

  - available design systems;
  - active tenant default;
  - active user default;
  - create/duplicate/import/export actions;
  - token editor;
  - component slot editor;
  - live preview;
  - publish/share controls;
  - permission-aware action buttons.

  The page should use Orchard content APIs or thin Crest adapters over Orchard content services only when the built-in APIs do not provide the needed admin projection.

  **API boundary.** Preferred access order:

  1. Orchard content APIs if enabled and sufficient.
  2. Orchard GraphQL for read projections if it fits.
  3. Thin `api/crest/*` adapter only for admin-specific projections, validation, token compilation, or current-user resolution.

  Any Crest adapter must:

  - authorize through Orchard permissions;
  - use the real request principal;
  - use Orchard content manager/services;
  - avoid parallel state;
  - not expose private user design systems to other users.

- [ ] **Add user duplicate/edit/select/publish workflow.** Users can create personal design systems if granted permission.

  Users can:

  - duplicate an available tenant design system;
  - edit their own copy;
  - set it as their user default;
  - keep it private;
  - publish/share it to the tenant if granted permission.

  Publishing a user design system should not bypass Orchard moderation/publishing rules. If the content type has drafts, the normal Orchard draft/publish flow applies.

## Recipes and deployment

Because design systems are Orchard content:

- seed default design systems through Orchard recipes;
- export/import through Orchard deployment plans where possible;
- avoid hand-maintained Crest-specific JSON stores.

- [ ] **Add deployment/recipe export path.** Host apps can provide their own design-system recipe steps without modifying Crest source.
- [ ] **Add cache invalidation/refresh for active design system changes.** The client may cache the resolved token payload for performance, but the server remains authoritative. A changed user/tenant design system should invalidate or refresh the client design payload through the same manifest refresh mechanism used for admin permissions/navigation.

**Localizable by default (ruling 2026-10-09).** Tokens and the style schema expose only
direction-aware, logical values; the shell root sets `dir` from the culture. A Master mode allows
raw CSS and physical properties, with a notice on every such edit that it breaks localization.
Detail in [blazor-display.md](blazor-display.md) › Design systems.

## Token taxonomy (ruling 2026-10-09)

A design system is the **visual language and the branding**: tokens, type scales, heading
settings, presets. It is separate from the theme, which is structure
([blazor-display.md](blazor-display.md) › decision 8).

- **Two layers.** A **reference palette** (raw values: `blue.500`, `space.4`) feeds the
  **semantic tokens**, so `color.bg.dark` and `color.accent.shadow` can point at the same palette
  entry and change together. Templates and primitives bind semantic tokens only; the palette is
  internal to the design system and its editor.
- **One flat semantic namespace.** `color.surface.1`, `radius.md`, `button.radius` and
  `nav.width` are all semantic tokens and live side by side, keyed by name, with no duplicates in
  the layer. "Component tokens" are not a layer of the design system: at the component layer a
  primitive *declares* which semantic tokens its style parameters default to.
- **Categories:** `color` (surface, text, border, accent, status: success, warning, danger,
  info), `font` (family, size, weight, line height), `heading` (one group per level 1–6),
  `space`, `radius`, `border.size`, `shadow`, `motion` (duration, easing), `breakpoint`. All
  logical and direction-free.
- **Scales:** numeric for ordered sets without a natural middle (`surface.1`, `accent.2`),
  t-shirt sizes (`xs`–`xl`) for sizes.
- **Extensible, keyed by name.** Modules register tokens through the registry, with a default
  expressed in existing tokens. Token keys are the same across tenants; values have per-tenant and
  per-user overrides (the override model of blazor-display.md § 5a). Crest and registering modules
  provide common-sense defaults for the tokens and pages they register. **A module does not
  change another module's defaults**, unless a Crest build-time setting lets the instance owner
  allow it, knowing it can break existing pages.
- **Presets** are switchable token sets a user picks between. **Who may override which tokens is
  a tenant-level setting**, per audience: for example admin-shell users get presets that change
  almost every token, members may change colour themes only (not spacing), site visitors get
  light and dark presets only.
- **Inheritance, compiled at publish.** A design system is a node definition under the override
  model: a tenant's or user's system is overrides over a base, and the compiled token set is
  produced on publish, which gives drafts and preview for branding.

## Decisions needed

- [ ] **Part, fields or hybrid.** Whether design system content should use a custom part only, fields only, or a hybrid.
- [ ] **Where the user default is stored.** Whether user default selection is stored on the User object, User profile content item, or as a small user-scoped Orchard setting.
- [ ] **Where the tenant default is stored.** Whether tenant default selection should be stored in Site Settings or as a flag on the design system content item. Prefer Site Settings if the setting is tenant-wide.
