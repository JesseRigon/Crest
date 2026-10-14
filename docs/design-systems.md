# Design systems

Tenant and user design systems, stored as content so the platform is the system of record. A design system is the **visual language and the branding**: tokens, type scales, heading settings, presets; the theme is structure ([blazor-display.md](blazor-display.md) › decision 8). The canonical `--crest-*` primitive token contract, its packaged default CSS and the current Site-Settings-backed Design System page are built: see [Crest.Components/README.md](../Crest.Components/README.md) › Design tokens.

Not built:

- the content type and content-part storage;
- tenant/user selection and resolution;
- permissions and publishing workflow;
- recipe/deployment integration;
- full Design System editor.

## Content model

Design systems are content, not JSON files of their own. A first-party content type means the platform owns:

- persistence;
- versioning/drafts/publishing;
- ownership;
- permissions;
- tenant scope;
- import/export through recipes and deployment plans;
- audit/history where enabled;
- search/indexing where enabled.

Crest.AdminTheme provides the Blazor editor, preview, selector, and token application layer, with no parallel storage or permission model.

- [ ] **Content part model.** The token taxonomy is fixed ([Token taxonomy](#token-taxonomy)); the part shape is open (see Open questions). Suggested `DesignSystemPart` fields/properties:

  ```json
  {
    "name": "Default",
    "description": "Default admin design system.",
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

  The exact JSON shape can evolve; the rule is that the stored document describes Crest-owned semantic tokens, not direct Radzen token names and not component implementation variables.

  Component-specific variables such as primary-navigation width, panel-menu rail width, indentation, or animation timing remain component mechanics. They are configured by component parameters/settings when needed, not by the primitive token contract.

- [ ] **`DesignSystem` content type/part and permissions.** The content type is `DesignSystem`.

  Suggested parts:

  - `TitlePart`
  - `AliasPart`
  - `AutoroutePart` optional, mostly useful if design systems get public previews later
  - `OwnerEditor` / content ownership
  - `DesignSystemPart`

  Permissions are the platform's; there are no local role checks.

  Suggested permissions:

  - `ManageDesignSystems`
  - `ViewDesignSystems`
  - `CreateDesignSystems`
  - `EditOwnDesignSystems`
  - `EditTenantDesignSystems`
  - `PublishDesignSystems`
  - `SetTenantDefaultDesignSystem`
  - `SetOwnDesignSystem`

  Where the content permissions suffice for a step, they are used directly instead of a new permission.

- [ ] **Seed the packaged default into tenant content via recipe/setup.** A static CSS file stays:

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

  On setup, a recipe creates the default `DesignSystem` content item from this default token map. After setup, tenant/user choices come from content.

  - [x] The default file's token contract is built: see [Crest.Components/README.md](../Crest.Components/README.md) › Design tokens.

## Resolution and runtime

### Scope model

#### System fallback

Static packaged defaults loaded from Crest.AdminTheme.

Used only when no tenant/user design system can be resolved.

#### Tenant design systems

Tenant-scoped content items.

Tenant admins create, edit, publish, archive, and set tenant defaults according to their permissions.

A tenant may have several design systems, for example:

- Default Admin;
- Compact Admin;
- High Contrast;
- Seasonal;
- Department-specific.

- [ ] **Resolver for the current user/tenant design system.** The shell manifest resolves the design system in this order:

  1. Authenticated user's selected personal design system, if valid and permissioned.
  2. Authenticated user's selected shared tenant design system, if valid and permissioned.
  3. Tenant default design system content item.
  4. Crest packaged default design system.

  Every resolution step checks the current user's content permissions.

- [ ] **The design-system payload in the shell manifest.** Crest.AdminTheme converts the selected design system content item into a compact client payload:

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

  **Token source mapping.** A provider pattern exists only at the source-to-Crest-token boundary.

  Examples:

  - Radzen theme variables to Crest primitive tokens.
  - MudBlazor theme variables to Crest primitive tokens.
  - Bootstrap variables to Crest primitive tokens.
  - A stored `DesignSystem` content item to Crest primitive tokens.

  The provider does not style individual components and does not emit primary-navigation/panel-menu implementation variables. Its output is the canonical primitive token contract.

## Admin UI and user workflow

- [ ] **Blazor Design System page over the content services**, extending the Design System page in Crest.AdminTheme.

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

  **API boundary.** The design-system API is declared by the module that owns design systems and registered with Crest.Server's API registry, like every module API ([server.md](server.md)). It uses the content API where that suffices and adds only admin-specific projections, validation, token compilation and current-user resolution. It:

  - authorizes through the platform's permissions;
  - uses the real request principal;
  - uses the content manager/services;
  - avoid parallel state;
  - not expose private user design systems to other users.

- [ ] **User duplicate/edit/select/publish.** Users with the permission create personal design systems.

  Users can:

  - duplicate an available tenant design system;
  - edit their own copy;
  - set it as their user default;
  - keep it private;
  - publish/share it to the tenant if granted permission.

  Publishing a user design system goes through the normal draft/publish flow; it never bypasses moderation.

## Recipes and deployment

Because design systems are content, defaults are seeded through recipes and design systems move through deployment plans; there is no hand-maintained JSON store.

- [ ] **Deployment/recipe export path.** Host apps can provide their own design-system recipe steps without modifying Crest source.
- [ ] **Cache invalidation/refresh on design system changes.** The client may cache the resolved token payload, but the server is authoritative. A changed user/tenant design system invalidates or refreshes the client design payload through the same manifest refresh mechanism used for admin permissions/navigation.

**Localizable by default.** Tokens and the style schema expose only
direction-aware, logical values; the shell root sets `dir` from the culture. A Master mode allows
raw CSS and physical properties, with a notice on every such edit that it breaks localization.
Detail in [blazor-display.md](blazor-display.md) › Design systems.

## Token taxonomy

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

## Open questions

- [ ] **Part, fields or hybrid.** Whether design system content should use a custom part only, fields only, or a hybrid.
- [ ] **Where the user default is stored.** Whether user default selection is stored on the User object, User profile content item, or as a small user-scoped setting.
- [ ] **Where the tenant default is stored.** Site Settings or a flag on the design system content item (Site Settings preferred, since the setting is tenant-wide).
