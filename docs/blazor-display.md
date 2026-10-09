# The display system for Blazor

**Status: design, nothing implemented (2026-10-09).** This is the backend the Blazor designer
([blazordesigner.md](blazordesigner.md)) will sit on: how templates, layouts, pages and content
items become Blazor components, as data a tenant can edit, rendered the same way on the server,
in WebAssembly and headless. It repurposes the platform's display system (shapes, drivers,
placement, zones, shape tables) rather than replacing it, and retires the parts of it that are
bound to Razor and Liquid.

Three reads fed this: the platform's display management code (`src/Crest/Crest.DisplayManagement`,
`Crest.ContentManagement.Display`, the Templates, Layers, Flows and Widgets modules), Crest's
current Blazor side (`Crest.Server`, `Crest.AdminTheme/wasm`, `Crest.SiteTheme`, `Crest.Components`),
and what .NET 10 Blazor allows for data-driven rendering.

## The thesis

**The platform already builds a type-neutral component tree. Only the last step, turning it into
HTML, is Razor's.** A display driver returns a `ShapeResult`; placement puts it in a zone at a
position with alternates; the display manager assembles an `IShape` tree (type, display type,
properties, items, zones, alternates, wrappers). None of that mentions HTML. Then
`DefaultHtmlDisplay` resolves each shape to a `Func<DisplayContext, Task<IHtmlContent>>` through
the theme's shape table, and a `.cshtml`, a `.liquid` file or a DB-stored Liquid template renders
it.

So the design is: keep the build side as it is, replace the binding with a **component binding**,
and replace the HTML renderer with a **tree renderer** that walks the shape tree and emits Blazor
components. Everything a tenant edits (templates, layouts, placement, pages) is data that feeds
that build side, and the same tree is the headless contract.

What this is *not*: Razor runtime compilation (tenant C# is code execution; Roslyn in WASM is
unusable), and not a second, Blazor-only display system beside the platform's (the client
`DisplayManager`/`Shape` in `Crest.AdminTheme/wasm` is exactly that today, and it is retired by
this design).

## Vocabulary

| Term | Meaning here |
| --- | --- |
| **Shape** | The platform's `IShape`: a node in the display tree. Keeps its name. |
| **Display tree** | The serializable form of a shape tree: `{ type, displayType, alternates, properties, zones: { name: [nodes] }, items: [nodes], metadata }`. What the renderer consumes and what the headless API returns. `CrestModel` in `Crest.Components/Models` is already most of this DTO (zones, alternates, metadata) and becomes it. |
| **Component** | Anything in the component registry: a name, a parameter schema, slots, the shells it may appear in. One concept with facets, not kinds: a component may be a **primitive** (text, container, input, button, link), a **composite** (a header built from primitives, as code or as a saved template), an **editor** (the input component for a value type), a **page** (it has a route), and/or a **host** (it exposes open zones others fill). The same entry can have several facets. |
| **Binding** | The rule that maps a shape (type + alternates + display type, per theme) to a component, or to a template. Replaces `ShapeBinding.BindingAsync`. |
| **Template** | A tenant-editable component tree bound to a shape name, stored as content. Replaces a Liquid template in the Templates module. A template *is* a binding whose target is a tree instead of a compiled component. |
| **Layout** | The template bound to the `Layout` shape for a shell and theme: the page frame, declaring the zones. |
| **Zone / slot** | A named list of child nodes; a `RenderFragment` parameter on the component that renders it. The two words are one concept. A zone is **closed** (only the template that declares it fills it) or **open**: a well-known key (`Navigation`, `Content`, `Footer`, `AdminMenu`) that other modules fill without owning the component, through placement, code registration or Layers. Open zones are the registration point; `IPageRegionContributor`/`PageRegionRegistry` ([page-regions.md](page-regions.md)) is this and folds in. |
| **Page** | A content item reached by a route (Autoroute, or a route a module declares) rendered through a layout. A Blazor `@page` component is a page too, with the tree built in code. |
| **Editor kind** | Not an enum: the editor components in the registry, each declaring the value types (CLR types, content field types) it edits. The designer's property pane, the content field editors and a template's inputs all draw from this one set (ruling 2026-10-09, resolving "custom data types" in blazordesigner.md: they are these editors). |
| **Expression** | A Liquid expression in a template or node property, evaluated at build time by the one expression engine ([architecture.md](architecture.md) › Direction). Never a template language for structure. |

## What stays, what goes

**Stays, unchanged** (the build side; all type-neutral):

- `IDisplayDriver<T>`, `DisplayDriver<T, TDisplayModel>`, `IDisplayManager<T>`, `ShapeResult`
  (`Location`, `Differentiator`, `OnGroup`, `RenderWhen`), `BuildDisplayContext` / `BuildEditorContext`
  / `UpdateEditorContext`, display types (`Detail`, `Summary`, `SummaryAdmin`, `DetailAdmin`), editor
  groups.
- `IContentItemDisplayManager`, `ContentItemDisplayCoordinator`, the content, part and field
  display drivers, the alternates factories, `IContentDisplayHandler`.
- `IShape`, `ShapeMetadata` (minus `ChildContent`), `IShapeFactory`, alternates, wrappers, positions.
- `PlacementInfo`, `placement.json`, `IPlacementInfoResolver`, the Placements module's data, and the
  feature order that lets a theme's placement override a module's.
- `IZoneHolding`, `ILayoutAccessor`, zones as named, positioned lists.
- `ShapeTable`, `IShapeTableManager` (per theme, per tenant, invalidated on feature and theme
  change), `IShapeTableProvider`, `ShapeTableBuilder.Describe(...)` with `OnDisplaying`, `Placement`
  and the events.
- `IThemeManager`, `IThemeSelector`, `BaseTheme` layering.
- `IResourceManager`'s data (`GetRequiredResources`, links, metas, `ResourceDefinition`).
- The Layers module's rule evaluation (`ILayerService`) and the widget placement logic.

**Replaced:**

| Today | Becomes |
| --- | --- |
| `ShapeBinding.BindingAsync : Func<DisplayContext, Task<IHtmlContent>>`, `ShapeAlterationBuilder.BoundAs(...)` | A component binding: `{ ComponentName }` or `{ Template }`, resolved with the same alternates-last-to-first order, same shape table, same theme scoping. |
| `IHtmlDisplay` / `DefaultHtmlDisplay`, `IDisplayHelper.ShapeExecuteAsync` | The **tree renderer**: shape tree → display tree → `RenderTreeBuilder` calls. One implementation, compiled into a library both the server and the WASM client reference. |
| `IShapeTemplateViewEngine`, `RazorShapeTemplateViewEngine`, the Liquid shape engines, `ShapeTemplateBindingStrategy` (file harvesting of `.cshtml`/`.liquid`) | Component discovery through the registry; a module binds a shape to a component by attribute or by `Describe(...)`, never by a file name convention. |
| `TemplatesShapeBindingResolver` (DB Liquid → shape) | The same resolver shape, returning a component-tree template from content. |
| `LayerFilter` (an MVC `IAsyncResultFilter`) | A layout build step that runs the same rule evaluation and adds widget shapes to the layout's zones. |
| `IResourceManager.Render*(TextWriter)`, the resource tag helpers | A head/foot renderer from the resource registry data, emitted as `HeadContent` / markup nodes in the tree. |
| `Layout.liquid`, `Content-Page.liquid`, `Widget-*.liquid`, `Menu*.liquid` in `Crest.SiteTheme` | Layout, content and widget components (and templates for the tenant-editable ones). |
| `Crest.AdminTheme/wasm/DisplayManagement` (`DisplayManager`, `Shape`, `ShapeView`) and the duplicate `Crest.Components/Shapes/Shape.cs` | Retired as a display system. What remains of `DisplayManager` is shell state (user, manifest, menus, culture) and moves out of the "display management" name. |
| `[CrestBlazorComponent(name)]`, `ICrestBlazorComponentRegistry` (global singleton, name → Type), `IWorkbenchComponentRegistry` (planned), the route table's component scan | **One component registry**, below. |
| `CrestBlazorComponentShapeBindingResolver` (renders a component to static HTML with `HtmlRenderer` so Liquid can embed it) | Gone with Liquid views; `HtmlRenderer` stays for headless HTML (email, static pages). |

## The pieces

### 1. The component registry

One registry, per shell, replacing the three partial ones. An entry is:

```text
ComponentDescriptor
  Name            the shape type it binds by default ("Content", "Content__Page", "Widget__Image",
                  "Menu", "Zone", "Heading", ...). Alternates are names too.
  Type            the Blazor component type (statically referenced, trim-safe)
  Parameters      the schema: name, CLR type, editor kind (text, number, bool, enum, picker,
                  color, token, markup), required, default, group
  Slots           the RenderFragment parameters: name, allowed component names, min/max
  Facets          Page (has a route), Host (declares open zones), Editor (edits which value
                  types); primitive versus composite is only whether Type is code or a template
  Shells          admin | site | member (from the assembly's [CrestShell])
  Feature         the feature it belongs to; absent when the feature is disabled
  Permission      optional; the node is dropped from the tree for users without it
  Category, DisplayName, Icon   for the designer's palette
```

- **Discovery:** an attribute on the component (the existing `[CrestBlazorComponent]` grows into
  it; per X3 the *component* prefixes stay, the *service* names are neutral) and a **source
  generator** that emits the descriptors and a `name → Type` map at build time. Reflection over
  `[Parameter]` properties is the dev-time fallback; in WASM the generated map is what keeps
  trimming from stripping parameters.
- **Scope:** the registry is built per tenant and theme like the shape table and the route table
  (same scanner, same cache, same invalidation), as blazordesigner.md already rules. It is also
  what the route table reads: a component with `@page` is a page *and* a registry entry.
- **The schema is shared** between the server (validation, defaults), the client renderer and the
  designer's property pane. One source.
- **Schema vocabulary** (ruling 2026-10-09): modelled on Plasmic's MIT `registerComponent`
  metadata. Parameter types: `string`, `number`, `boolean`, `choice` (static or computed
  options), `slot` (`allowedComponents`, `defaultValue`), `object`, `array`, `dataSource` (a
  binding to a query or the context), `eventHandler` (an action: a workflow). Entries may declare
  `templates` (presets the palette offers), `defaultStyles`, `isAttachment` (a wrapper that
  takes one child), `isRepeatable`, and per-parameter `hidden`/`advanced`/`readOnly` rules
  computed from other parameters. Value types from `Crest.Data.Types` plug in as parameter types
  beside the base ones.

### 2. Bindings: shape → component or template

The shape table keeps its per-theme descriptor list. Each binding becomes:

- **Code binding:** `builder.Describe("Content__BlogPost").BindTo<BlogPost>()` or the attribute
  on the component. Modules ship these.
- **Template binding:** a resolver (the Templates module, reworked) that answers "is there a
  tenant template for shape `X` with these alternates in this theme?" and returns a tree.
- **Resolution order** is today's: alternates last to first, then the type's segments; template
  bindings are asked before code bindings, as `TemplatesShapeBindingResolver` is today. A tenant
  template therefore overrides a module's component for that shape, and the theme's binding
  overrides the module's, with no new rules.

### 3. The tree renderer

A recursive walker over the display tree, in a library referenced by server and client:

- For each node: look up the component by the resolved binding; `OpenComponent(type)`;
  `AddComponentParameter` for each schema parameter present in the node's properties (converted
  by the schema's CLR type); each zone becomes a `RenderFragment` that renders the zone's nodes;
  `SetKey(node.Id)` for stable diffing; constant sequence numbers per call site, `OpenRegion` per
  node. A template node renders the template's tree with the node's properties as its inputs.
- **Wrappers** are components with one slot that take the wrapped node as their child.
- **Markup leaves** (a rich-text field, Liquid output) render as sanitized `MarkupString`. Liquid
  is evaluated on the server at build time into a leaf; the client never runs it.
- **The same renderer** runs under static SSR, inside a Server or WebAssembly island, and under
  `HtmlRenderer` for headless HTML. Nothing in it touches `HttpContext`, JS or `IHtmlContent`.

### 4. One render mode

**Every shell renders `InteractiveAuto` at its root, prerendered** (ruling 2026-10-09). The page
arrives as server-rendered HTML, so it reads without JavaScript and never "dies" waiting for the
runtime, then hydrates (Server circuit first, WebAssembly once cached). One mode everywhere keeps
the system reasonable: no per-node or per-shell render modes to encode, no island boundaries, no
casing per shell. The admin shell already works this way.

What that implies for the renderer:

- The display tree **crosses the boundary once, at the root**: the shell's root component takes
  the tree the server built (`[PersistentState]`, .NET 10), so the client renders the same tree
  without a second fetch. The tree is JSON; `RenderFragment`s are built on the client by the
  renderer from the tree's zones, never serialized.
- The renderer is therefore the same code on both sides, with no island special-casing; the
  component registry needs no `RenderMode` facet.
- Per-node islands (`AddComponentRenderMode`) are **not** used. They stay possible later as an
  optimization for public pages, but are not part of this design.
- Data a component needs beyond the tree comes through the API, never a server service.

### 5. Templates

A template is content (`Template` content type, in the reworked Templates module):

```text
Template
  Name          the shape name it binds ("Content__Page", "Widget__Hero", "Layout", "Menu")
  Shell, Theme  where it applies (theme optional: a template for all themes of the shell)
  DisplayType   optional (Detail, Summary, ...)
  Tree          the component tree: nodes { component | template, properties, zones }
  Inputs        the properties the bound shape must supply (declared, validated against the
                shape's known properties: ContentItem, the part, the field)
  Version       content versioning: draft, published, history
```

- Node properties may be **literals**, **bindings** to the shape's properties (`{{ Model.ContentItem.DisplayText }}`,
  a field value, a picker's resolved label) or **expressions** (Liquid, the one engine).
- The schema of every component is known, so a template is validated on save: unknown component,
  unknown parameter, wrong type, a slot's allowed children.
- **A template need not bind a driver-produced shape.** A template named `Header` with no driver
  behind it is a composite: a reusable node any other template places by name (what Liquid's
  `shape_new "Header"` was). Composites in code and composites as templates resolve through the
  same binding, so a tenant can replace a module's header component with a template of its own.
- Admin templates and site templates are separate sets, as the two resolvers are today.
- **Liquid templates are not migrated.** Per the Direction ruling they go with the stock UIs; the
  Templates module's data becomes the new shape when it is reworked, and dev tenants reset.

### 6. Layouts, zones, pages

- **Layout** is the template bound to `Layout` for a shell and theme. It declares the zones
  (Header, Navigation, Content, Footer, …) as slots, so the zone list is data, read by the Layers
  rules, the Placements editor and the designer alike. `ILayoutAccessor` stays the way the layout
  shape is reached while a page is built.
- **Page content** is what it is today: a content item (`Page` with `FlowPart`/`BagPart`, or any
  type with `AutoroutePart`) built by `IContentItemDisplayManager` into the Content zone; widgets
  from Layers into their zones; menus as `Menu` shapes. All of that is already shapes.
- **Routing a content page in Blazor:** one content route component in the site shell
  (`/{**path}` after the declared `@page` routes) resolves the path through Autoroute, builds the
  layout and the item, and renders the tree. Today that is `Content-Page.liquid` through MVC.
  Module `@page` components keep working beside it and may build a tree in code
  (`IDisplayManager<T>`) or render directly.
- **The site shell gets what it lacks:** a layout component, design-system CSS in `App.razor`'s
  site branch, and navigation, all through the same tree.
- **Two kinds of contribution, kept distinct.** Filling an *open zone* puts a component into a
  region (a widget into `Footer`, a module's panel into a page region). Contributing to a *model*
  puts an item into data a component renders (a menu item through `INavigationProvider`, which the
  `Menu` component then renders). The admin menu and the site navigation are both: a model built
  by providers, rendered by a `Menu` component, placed in an open zone of the layout.

### 7. Content items

- Rendering stays driver-driven: a part's display driver emits a shape, placement puts it in a
  zone, a binding picks the component. `CrestBlazorComponentPart` (a widget that names a
  component and its parameters) is one widget among others, with its parameters validated against
  the registry schema instead of free strings.
- **Field editors** are components too: the editor tree (`BuildEditorAsync`) resolves to editor
  components, and `UpdateEditorAsync` consumes the posted values. The generic content-item editor
  in the admin shell becomes a rendered editor tree instead of a hardcoded form, which is what
  "per-type designed pages" in [content-items.md](content-items.md) needs.
- The display tree carries **resolved** values (a picker's labels, a reference's display text), so
  the renderer and headless clients do not re-resolve. The raw `?parts=` read stays for editing.

### 8. Headless

- `GET …/content-items/{id}/display?displayType=Detail` returns the display tree: the same JSON
  the client renderer consumes. A headless client gets structure, resolved values and component
  names; it renders them with its own components. That is the parity rule ("every capability a
  Blazor page has is reachable through the API") met by construction, since the Blazor page *is*
  that tree.
- `HtmlRenderer` renders the same tree to static HTML for email bodies, PDF sources and cached
  static pages.

### 9. Design systems

- Tokens (`--crest-*`) stay the styling contract. A component's style-typed parameters have the
  `token` editor kind, so a template picks a token, not a color.
- The planned `DesignSystem` content type ([design-systems.md](design-systems.md)) resolves per
  user, shared, tenant, default, and the result is loaded for the site shell as it is for admin.
- A template may carry **responsive variants**: per-breakpoint property overrides on a node. Data
  only; the renderer applies the variant for the current breakpoint. Later.
- **Localizable by default; "Master" mode for full control** (ruling 2026-10-09). The style
  schema exposes only what survives localization: logical properties (`inline-start`, not
  `left`), tokens, direction-aware layout, no fixed widths on text. The shell root sets `dir`
  from the culture, so every tree flips without a component knowing its side; captions stay
  localized data. A **Master** mode unlocks raw CSS and physical properties for people who want
  complete control, and every such edit carries a notice that it will break localization (right-
  to-left layout, text expansion) and is excluded from the localizable guarantee. Master edits are
  marked in the template so the designer and the headless API can tell them apart.

### 10. Security

- **Tenant templates are data, never code.** The tree is validated against the registry; the
  renderer instantiates only registered types; `Type.GetType` on a stored name is never used.
- **Expressions** run on the server through the sandboxed engine (member allow-list), before the
  tree leaves the server.
- **Permissions are applied when the tree is built, on the server:** a node whose component or
  content the user may not see is not in the tree. The per-part read permission question in
  [permissions.md](permissions.md) lands here. The client never decides visibility.
- **Markup** is sanitized on save and on render.
- A template's author needs a template permission; the designer is gated like any admin page.

## Data model

| Entity | Where | Notes |
| --- | --- | --- |
| Component descriptor | Code, generated at build; cached per tenant/theme at run time | The registry. Not content. |
| Binding (code) | Shape table, from attributes and `Describe(...)` | Per theme, per feature. |
| Template (and Layout) | Content item, versioned | The tenant's bindings. |
| Placement | `placement.json` per feature + the Placements module's document | Unchanged; its editor becomes a Blazor page. |
| Page | Content item (Autoroute) or `@page` component | Unchanged. |
| Widget placement / layers | Layers module documents | Unchanged; the MVC filter becomes a build step. |
| Design system | Content item (planned) | design-systems.md. |
| Display tree | Not stored; built per request, cacheable per (item version, display type, user scope) | The wire format. |

## Rework plan (in order, each a module reworked in place)

1. **Registry and schema.** The source generator, the descriptor, the attribute, one registry
   replacing the three. The route table reads it. No rendering change yet.
2. **Display tree and renderer.** The DTO (`CrestModel` becomes it), the tree renderer library,
   the component binding in `ShapeBinding`, a Blazor `IDisplay` beside `IHtmlDisplay`. First
   consumer: the admin content-item display page renders a content item through it.
3. **Site shell on the tree.** Layout component, content route component, menus and widgets as
   components, `App.razor`'s site branch gets the design-system CSS. `Content-Page.liquid` and the
   other site Liquid files go. Islands with `AddComponentRenderMode`; `BlazorCounter.razor` is
   deleted (it becomes a widget).
4. **Templates module rework.** `Template` content type, the tree editor's data contract (the
   designer UI is blazordesigner.md's), the template resolver, validation. Liquid templates
   removed.
5. **Editors as trees.** `BuildEditorAsync` through the renderer; the generic editor page
   consumes it.
6. **Headless endpoint, HtmlRenderer path, Layers as a build step, Placements editor.**
7. **Retire** `IHtmlDisplay`, the Razor and Liquid shape engines, the tag helpers, the client
   `DisplayManager`'s display half, `CrestBlazorComponentShapeBindingResolver`.

Each step compiles and runs on its own; the Liquid path is removed per page as its Blazor
replacement lands (Direction ruling), not all at once.

## Reference systems for the backend

Permissively licensed systems whose *data model* this design borrows from (verify licences
before porting code; WordPress is GPL and is consulted for vocabulary only):

- **Oqtane** (MIT, Blazor, copyright the .NET Foundation; every repository in the `oqtane` org
  is MIT; checked 2026-10-09). **The primary reference** (ruling 2026-10-09): the only
  permissive, Blazor-native CMS backend. Cloned for local reference at
  `/workspaces/oqtane.framework` (with `oqtane.docs`, `oqtane.theme.bootswatch`,
  `oqtane.blogs`). What to study and port from: the data model (Site, Page, PageModule,
  ModuleDefinition, Theme, Container, Permission, Setting, Folder/File), how pages and module
  instances are stored and permissioned, the module/theme packaging and install model, the
  render-mode handling (static, Server, WebAssembly, MAUI hybrid from one codebase), and the
  module definition metadata. Its fixed *panes* per theme are the one thing not borrowed: this
  design's slots are nodes in the tree.
- **Umbraco CMS** (MIT, .NET): the Block Grid and Block List editors. Element types as block
  schemas, blocks nested through *areas* (fluid nesting, no fixed zones), content versus
  settings per block, allowed-block rules per area, the Delivery API as the headless tree. The
  closest .NET backend to this design.
- **Payload CMS** (MIT): the `blocks` field (discriminated `{blockType, fields}`), per-field
  access control, versions and drafts, localization of block content.
- **Strapi** (MIT, community edition): reusable *components* and *dynamic zones* (a list of
  components of allowed kinds), which is the template-part and slot model.
- **Plasmic**: split licence, checked 2026-10-09. Everything outside `platform/` (the SDKs,
  loaders, `plasmicpkgs`, the `registerComponent` API and its docs) is **MIT**; the Studio and
  its backend under `platform/` (`wab`: React client, TypeScript server, codegen server) are
  **AGPL-3.0**, so by the licence rule they are never ported from or consulted for design. What
  this design takes is the MIT component-registration model: the prop-type vocabulary (`string`,
  `number`, `boolean`, `choice` with static or computed options, `slot` with `allowedComponents`
  and `defaultValue`, `object`, `array`, `dataSource`, `eventHandler`), `templates` (presets),
  `defaultStyles`, `isAttachment` (wrapper components), `isRepeatable`, and conditional prop
  visibility. The registry schema in § 1 is modelled on it.

## Rulings (2026-10-09)

- **Where the standards live** (module layout):
  - `Crest.Data` holds the **value type registry** (`Crest.Data.Types`): the one place a value
    type is declared with its field type, editor, display primitive, query column type and
    workflow type. A module that owns a type (Money) stays its own module and *registers* its
    types there. This is where the type system meets the query system, so it sits with data,
    not with the UI.
  - `Crest.Global` becomes **`Crest.Data.Global`**: the standardized global data set imports and
    loading ([global-store.md](global-store.md)) plus the global data registry, so no module
    reimplements either.
  - `Crest.Components.Primitives` holds the primitive components; modules register their
    primitives there.
  - `Crest.Components.Templates` holds the higher-level template trees modules ship (composites,
    default templates); the Templates module's tenant data layers over them.
  - The rest of the data-layer chain (`Crest.Data`, connectors, query, the API surfaces) is
    decided later ([queries.md](queries.md)); the order sketched so far is not committed.

- **Shape names are the registry keys** (`Content__Page`, `Widget__Hero`, `Zone`), so placement,
  alternates and templates all bind by the same names.
- **The renderer, the display tree DTO and the registry contracts live in a small neutral
  library** below `Crest.Components`, referenced by the server and every theme client; no UI
  framework in it.
- **The Templates module is reworked in place**, keeping its feature id, as with Media and
  Queries; the data shape changes and dev tenants reset.
- **Editor kinds are editor components in the registry**, not a vocabulary in code; modules add
  one by registering a component that declares what it edits.
- **One render mode, `InteractiveAuto` at every shell root**, prerendered; no islands (§ 4).
- **Components are one concept with facets** (primitive, composite, editor, page, host), and open
  zones are the universal registration point; page regions fold into zones (Vocabulary, § 6).
- **The design system is localizable by default**; a Master mode unlocks raw control with
  break-localization notices (§ 9).
- **Custom data types are not a prerequisite** for rendering: a tenant content type has parts and
  fields with drivers and builds a tree like any other; what blazordesigner.md meant by them is the
  editor components above.

## Decisions needed

- [ ] **Per-part display permissions.** Where the rule is declared and when it is enforced; see
  the discussion in the session of 2026-10-09. Options: (1) a setting on the part or field
  definition in the type editor ("viewable with permission X"), enforced by a node filter when the
  tree is built and by `UpdateEditorAsync` on save; (2) the access-policy seam of
  [members.md](members.md) (groups grant read) answering per node; (3) only item-level
  `ViewContent` until the permission system work lands. Recommendation: build the node filter seam
  now (every node carries what admitted it; the coordinator asks the filter before adding a part
  or field shape), with item-level rules as its first implementation, and let (1) and (2) plug in.

## Out of scope here

The designer UI (canvas, palette, property pane, drag and drop, undo) is
[blazordesigner.md](blazordesigner.md); it edits the `Template.Tree` this document defines and
reads the registry's schema. The expression engine choice is [architecture.md](architecture.md) ›
Direction. Shell registry and per-shell hosts are [shells-and-themes.md](shells-and-themes.md).
