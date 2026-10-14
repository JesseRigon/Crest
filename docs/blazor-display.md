# The display system for Blazor

**Status: rework steps 1–6 landed 2026-10-12 with the interaction model (§ 4a); step 7 and the widget deletions are partial (see the Rework plan).** This is the backend the Blazor designer
([blazordesigner.md](blazordesigner.md)) will sit on: how templates, layouts, pages and content
items become Blazor components, as data a tenant can edit, rendered the same way on the server,
in WebAssembly and headless. It repurposes the platform's display system (shapes, drivers,
placement, zones, shape tables) rather than replacing it, and retires the parts of it that are
bound to Razor and Liquid.

Three reads fed this: the platform's display management code (`src/Crest.DisplayManagement`,
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
| **Display tree** | The serializable form of a shape tree: `{ type, displayType, alternates, properties, zones: { name: [nodes] }, items: [nodes], metadata }`. What the renderer consumes and what the headless API returns. Its node is `DisplayNode` in `Crest.DisplayManagement.Abstractions` (ruling 2026-10-12); `CrestModel` stays the content-item wrapper, its unused tree parts removed. |
| **Component** | Anything in the component registry: a name, a parameter schema, zones, the shells it may appear in. One concept with facets, not kinds: a component may be a **primitive** (text, container, input, button, link), a **composite** (a header built from primitives, as code or as a saved template), an **editor** (the input component for a value type), and/or a **host** (it exposes open zones others fill). The same entry can have several facets. A **page is not a component** (ruling 2026-10-09): pages are their own kind, § 6a. |
| **Binding** | The rule that maps a shape (type + alternates + display type, per theme) to a component, or to a template. Replaces `ShapeBinding.BindingAsync`. |
| **Template** | A tenant-editable component tree bound to a shape name, stored as content. Replaces a Liquid template in the Templates module. A template *is* a binding whose target is a tree instead of a compiled component. |
| **Layout** | The template bound to the `Layout` shape for a shell and theme: the page frame, declaring the zones. |
| **Zone** | A named list of child nodes; a `RenderFragment` parameter on the component that renders it (ruling 2026-10-09: the word stays "zone"; it is not a slot). A zone is **closed** (only the template that declares it fills it) or **open**: a well-known key (`Navigation`, `Content`, `Footer`, `AdminMenu`) that other modules fill without owning the component, through placement, code registration or Layers. Under the fluid model a `Zone` node may be placed anywhere, any number of times; open keys are the registration point, and `IPageRegionContributor`/`PageRegionRegistry` ([page-regions.md](page-regions.md)) is this and folds in. |
| **Slot** | A data binding on a component: a parameter whose value comes from data (the item, a query, the context) rather than a literal (ruling 2026-10-09). A picker's `source` is a slot; a heading bound to `TitlePart.Title` is a slot. Slots are what overrides may rebind (§ 5a). |
| **Page** | The unit that owns a URL: its route, shell bucket, route authorization and document concerns. Three kinds (module `@page` component, content page through Autoroute, designed page from data) share one route table; see § 6a. Distinct from a component and from a template. |
| **Editor kind** | Not an enum: the editor components in the registry, each declaring the value types (CLR types, content field types) it edits. The designer's property pane, the content field editors and a template's inputs all draw from this one set (ruling 2026-10-09, resolving "custom data types" in blazordesigner.md: they are these editors). |
| **Expression** | A Liquid expression in a node property, evaluated on the server at build time by the one expression engine ([architecture.md](architecture.md) › Direction). |
| **Liquid template** | The one authoring form of a template (ruling 2026-10-09): Fluid (MIT) with the platform's tags emitting tree nodes, evaluated on the server into the display tree. The designer edits this text, Gutenberg-style. Data, never code; see § 5 and § 11. |

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
| `[BlazorComponent(name)]`, `ICrestBlazorComponentRegistry` (global singleton, name → Type), `IWorkbenchComponentRegistry` (planned), the route table's component scan | **One component registry**, below. |
| `BlazorComponentShapeBindingResolver` (renders a component to static HTML with `HtmlRenderer` so Liquid can embed it) | Gone with Liquid views; `HtmlRenderer` stays for headless HTML (email, static pages). |

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
  Zones           the RenderFragment parameters: name, allowed component names, min/max
  Facets          Host (declares open zones), Editor (edits which value
                  types); primitive versus composite is only whether Type is code or a template
  Shells          admin | site | member (from the assembly's [CrestShell])
  Feature         the feature it belongs to; absent when the feature is disabled
  Permission      optional; the node is dropped from the tree for users without it
  InstancesPerPage single | multiple (ruling 2026-10-09): several instances of some components break their logic
  Category, DisplayName, Icon   for the designer's palette
```

- **Discovery:** an attribute on the component (the existing `[BlazorComponent]` grows into
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
  options), `zone` (Plasmic's `slot`: `allowedComponents`, `defaultValue`), `object`, `array`, `dataSource` (a
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
- **Wrappers** are components with one zone that take the wrapped node as their child.
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
- Kept after review (2026-10-12): the one interactive root is also what lets components on a
  page share state (§ 4a); separate islands could not.

### 4a. Interaction: outputs, page state, client actions

(ruling 2026-10-12, extending decisions 4 and 5) Components declared in a Liquid template talk
to each other through the tree, never through code in the template. Liquid only writes the
wiring down; the renderer connects it in the browser.

- **Outputs.** A component declares outputs beside its parameters: a value it publishes
  (`[ComponentOutput]` on an `EventCallback<T>`, the list's `SelectedItem`, a form's `Value`). The
  descriptor lists them with their type, so the designer can offer them and the server can
  validate a binding to one.
- **Page state.** Each rendered tree has one state store, scoped to the tree (`DisplayState`): a
  map from `{instance id}.{output}` to the latest value, plus named page variables. Nothing in it
  is trusted; it is the viewer's own input.
- **Bindings to outputs.** A parameter, or a query parameter of a slot, may bind to another
  node's output (`{ node, output, path }`) or to a page variable. When the value changes, the
  bound nodes re-render, and a bound slot re-runs its query through the API, with the caller's
  scope applied by the query system as always. In a template:
  `{% compose "ItemList", id: "invoices" %}{% slot "items", "RecentInvoices" %}{% endcompose %}` then
  `{% compose "PropertyList" %}{% slot "value", "Invoice", id: nodes.invoices.selected_item.id %}{% endcompose %}`.
- **Event payloads.** An event carries a payload (the selected row, the right-clicked row, the
  submitted values). Actions bound to the event may read it as `event` (`event.item.id`).
- **Client actions** join the server actions of decision 5 (run a workflow, navigate, submit):
  - **set** a page variable (from a literal, an output or the event payload);
  - **open** and **close** an overlay node (a modal, a drawer, a context menu, a popover)
    declared in the tree, positioned at the event when it has a position (a right-click);
  - an action is a list, run in order, so "set the row, open the menu" is one handler.
  Client actions only change what the viewer sees; anything that reads or writes data is a
  query or a workflow on the server, checked there.
- **Context menus** are overlay nodes whose items are actions; an item's workflow takes its
  parameters from the event payload or page state. Items the viewer may not run are dropped
  by the node filter (§ 10) when the tree is built, so a menu is context-aware by what was
  clicked and by who clicked it.
- **Validation**: a binding to an unknown node or output, or of the wrong type, is a save error;
  a cycle of bindings (A's parameter from B's output from A's) is refused.
- **What landed (2026-10-12).** Contracts in `Crest.DisplayManagement.Abstractions/Components`:
  `[ComponentOutput]`, `IsOverlay`, `ParameterKind.Output`, `DisplayNode.Bindings`/`Slots`/`Events`,
  `DisplayBinding`, `DisplaySlot`, `DisplayAction`, `DisplayState`, `DisplayActionRunner`,
  `DisplayOverlay`, `IDisplayActionDispatcher`, `IDisplayDataSource`, `DisplayTreeValidator`; the
  renderer owns a tree's state (or the page's, passed as `State`), feeds bound parameters and
  slots, records outputs and runs event actions, and renders overlays only while open. On the
  server: `DisplayTreeFilter` with `IDisplayActionFilter` (the workflows module answers for
  workflow actions with the Run decision and Run list), `POST api/crest/workflows/actions/{id}`,
  and the query module's `IDisplayDataSource`. The shells' `HttpDisplayActionDispatcher` runs
  actions and bound slots over the API (in a circuit too). Primitives: `ItemList` (selection
  output, positioned context-menu event), `ContextMenu` and `Dialog` (overlays), `MenuAction`,
  `PropertyList`, `Container`, `Fragment`; `Button`, `Link`, `Image`, `Card`, `Stack` are
  registered.

- **Comparisons and item bodies (2026-10-13, rulings in [widgets.md](widgets.md)).** A binding
  may be a predicate (`Operator`/`Operand`, `Combinator`/`Terms`) evaluated by `DisplayState`;
  an item's own tree is a `DisplayTreePart` bound through the template binding seam. Details in
  widgets.md › What landed.

### 5. Templates

A template is content (`Template` content type, in the reworked Templates module):

```text
Template
  Name          the shape name it binds ("Content__Page", "Widget__Hero", "Layout", "Menu")
  Theme         optional: the theme (and so the shell) it applies to; absent = every theme
  DisplayType   optional (Detail, Summary, ...)
  Tree          the component tree: nodes { component | template, properties, zones }
  Inputs        the properties the bound shape must supply (declared, validated against the
                shape's known properties: ContentItem, the part, the field)
  Version       content versioning: draft, published, history
```

- Node properties may be **literals**, **bindings** to the shape's properties (`{{ Model.ContentItem.DisplayText }}`,
  a field value, a picker's resolved label) or **expressions** (Liquid, the one engine).
- The schema of every component is known, so a template is validated on save: unknown component,
  unknown parameter, wrong type, a zone's allowed children.
- **A template need not bind a driver-produced shape.** A template named `Header` with no driver
  behind it is a composite: a reusable node any other template places by name (what Liquid's
  `shape_new "Header"` was). Composites in code and composites as templates resolve through the
  same binding, so a tenant can replace a module's header component with a template of its own.
- Admin templates and site templates are separate sets, as the two resolvers are today.
- **One authoring form: Liquid** (ruling 2026-10-09, superseding the two-form ruling of the same
  day). A template is a Fluid template whose platform tags (`{% shape "Heading", level: 2 %}`,
  `{% zone "Navigation" %}…{% endzone %}`) emit nodes into the tree rather than HTML; free text
  between tags becomes `Markup` leaves. It is evaluated on the server into the display tree,
  validated against the registry, and the client never runs Liquid. The display tree is the
  runtime and wire format only, never authored by hand. The designer edits the Liquid text
  (§ 11). Comparison and the engine plan: § 12.
- **Today's HTML Liquid templates are not migrated.** Per the Direction ruling they go with the
  stock UIs; the Templates module's data becomes the new shape when it is reworked, and dev
  tenants reset.

### 5a. Overrides are a basic tenet

(ruling 2026-10-09) A tenant never edits the base component or template everyone on the system
shares; every edit is an **override** (a fork) saved over the default and keyed to what it
overrides:

- **What may be overridden:** a node's literal settings (visual settings: font, background,
  spacing, variant) and its **slots** (a picker rebinds from one data target to another, as
  long as the picker's declared value types accept the target). The override stores only the
  changed values, over the component's or template's defaults.
- **What may not:** values derived from data. A content-driven page title, an item's price, a
  query result are not editable in the designer; only their visual settings are. Data belongs to
  data.
- **Three ids and a map** (ruling 2026-10-09). A **component id** is cross-tenant: the component
  a module declares at build time, editable by the default tenant only. Each tenant gets its own
  copy of the declared set, so every component has a per-tenant **node id** (the definition as
  that tenant sees and edits it); the map from component id to node id is per tenant. An
  **instance id** is one specific placement. None of these is scoped to a page: an instance can
  be referenced from any page (in practice harder to wire up than a node, but allowed).
- **One parent per override.** An override binds to exactly one parent: a node id **or** an
  instance id, never both. Binding to a node id makes a new node definition (a fork with its own
  node id); binding to an instance id makes a new instance derived from that instance. An
  override stores only the values it sets; every other value is inherited through its parent
  chain.
- **Resolution order**, from base to most specific: base node → node overrides → the instance
  of that node → instance overrides. The whole chain never appears on one override: a node
  override has only node ancestors, an instance override has an instance parent and then that
  instance's node chain.
- **Propagation:** changing a value at any point updates every descendant that does not set that
  value somewhere between itself and the change. A value set on a descendant shields it and its
  own descendants.
- **Worked example** (titles are nodes of one `Title` component):

  ```text
  Page 1
    title 1                                    base
      title 2   bound to title 1's node id      sets background = blue, text = "test"
        title 3 bound to title 2's instance id  inherits blue, sets text = "test1"
      title 4   bound to title 1's node id      sets text = "test2"
  Page 2
    title 5     bound to title 2's node id      inherits blue and "test"
  ```

  - Set title 1's background to green: titles 1 and 4 change; 2 overrides it, so 3 and 5 keep blue.
  - Set title 1's text: only title 1 changes; 2 and 4 override it, and 3 and 5 descend from 2.
  - Set title 1's font: all five change.
  - Set title 2's **instance** font: 2 and 3 change.
  - Set title 2's **node** font: 2, 3 and 5 change.

- **What may be overridden:** a node's literal settings (visual settings: font, background,
  spacing, variant) and its **slots** (a picker rebinds from one data target to another, as
  long as the picker's declared value types accept the target).
- **What may not:** values derived from data. A content-driven page title, an item's price, a
  query result are not editable in the designer; only their visual settings are.
- **Survival:** an override keeps working when its parent changes, provided the referencing
  system it was saved against (the parent's id and the parameters it overrides) has not changed
  in between. An override whose target parameter is gone is reported, not silently dropped.
- **The editor is explicit about which it edits.** Editing a base node is its own mode in the
  designer; the default mode edits the instance. Instance edits are under the page's design
  permission; node edits are under the shared-component permission (§ 10), admins only by
  default.
- **The component library** (the designer's left-hand palette) lists the declared components
  and the overrides modules register, first and always. Any component on any page is reachable
  through its search, as a second-class entry. A user may mark any component a favorite. The
  library's own layout is a tree too: declared at build time, overridable per tenant and per
  user by node-id reference through the same override system.
- **An instance used on a page the user cannot access** is checked at edit and at publish time.
  A tenant setting chooses one of two behaviours: the user cannot update that instance at all, or
  the user may edit it for preview but cannot publish it even with publish on the page they are
  working on, and a manager publishes on their behalf. The default is the second (ruling
  2026-10-09).

### 6. Layouts, zones, pages

- **Layout** is the template bound to `Layout` for a shell and theme. It declares the zones
  (Header, Navigation, Content, Footer, …) as zones, so the zone list is data, read by the Layers
  rules, the Placements editor and the designer alike. `ILayoutAccessor` stays the way the layout
  shape is reached while a page is built.
- **Page content** is what it is today: a content item (`Page` with `FlowPart`/`BagPart`, or any
  type with `AutoroutePart`) built by `IContentItemDisplayManager` into the Content zone; widgets
  from Layers into their zones; menus as `Menu` shapes. All of that is already shapes.
- **Routing a content page in Blazor:** one content route component in the site shell
  (`ContentPage`, `@page "/"` only; the shell middleware rewrites every site path no module
  page claims to it and hands it the real path, [shells-and-themes.md › Shell
  dispatch](shells-and-themes.md#shell-dispatch)) resolves the path through Autoroute, builds the
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

### 6a. Pages stay a distinct thing

A page is **not** a component with a route facet and nothing more. It is the unit that owns a
URL, and everything a URL implies, and that does not fold into the tree model (ruling
2026-10-09: pages remain first-class beside components and component trees).

What a page owns that a component or template never does:

- **A route**: the pattern, its parameters, the shell bucket (`admin`, `site`, `member`) it
  belongs to, and so the base path, the route gate and the client router that may resolve it
  ([blazor-web.md](blazor-web.md) › Route reachability, [shells-and-themes.md](shells-and-themes.md)
  › Shell dispatch). A shell must never resolve another shell's page; that is a property of the
  page's assembly or data, never of a node in a tree.
- **Authorization at the URL**: the route permission (`CrestRouteAuthorizationService`),
  `[AllowAnonymous]`, the login redirect, and the auth-cookie render-mode exception. The tree
  filter (§ 10) runs *after* the page admitted the request.
- **Document concerns**: title, metas, canonical URL, culture and `dir`, head and foot
  resources, the response status (404 for an unresolved path, 301 for a moved alias).
- **A template choice**: which layout and which template render it, resolved per shell, theme
  and display type (§ 5, § 6).

Three kinds of page, one route table:

| Kind | Route comes from | Tree comes from |
| --- | --- | --- |
| **Module page** (`@page` component in a `blazor-wasm/`, `member-wasm/` or `site-wasm/` library) | the `[Route]` attribute, scanned per bucket into the route table | code: the component renders itself, or builds a tree with `IDisplayManager<T>` and hands it to the renderer |
| **Content page** (a content item with `AutoroutePart`, or a type a module routes) | Autoroute (the content route component, "/" in the site bucket; the shell middleware rewrites every unclaimed site path to it) | the item's template, else the driver/placement generator |
| **Designed page** (a tenant-authored tree with its own URL: a landing page, a dashboard, a per-type admin page) | data: a `Page` record with route pattern + bucket, registered in the same route table as module pages | its stored tree |

The third kind is what blazordesigner.md's "page/app IDE" and content-items.md's "per-type
designed pages" need, and it is why the route table ([blazor-web.md](blazor-web.md)) must
accept data-defined entries beside scanned ones, with the same bucket gate, the same
permission check and the same per-tenant cache invalidation. A designed page in the admin
bucket is reachable only through the admin shell; in the site bucket only through the site
shell; it never changes shell by being edited.

A page is **its own kind, not a component** (ruling 2026-10-09): it is never in the component registry, never a child of anything, and carries the fields only a page has; components and composites are placed under it. What a page is *not*: a page is not a template. A template has no URL; the same template renders
many pages (every `Content__BlogPost`). A page may be rendered by a template, and a designed page
may embed its tree inline, but the route, the bucket and the authorization are the page's.

### 7. Content items

- Rendering stays driver-driven: a part's display driver emits a shape, placement puts it in a
  zone, a binding picks the component. ~~`CrestBlazorComponentPart` (a widget that names a
  component and its parameters) is one widget among others~~ Retired with widgets (2026-10-12):
  a component is placed by a template, a page's tree or a layer rule.
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

- **The trust boundary is the installer, not the tenant** (ruling 2026-10-09). Code enters the
  system only as modules the host's installer builds in and has vetted; a tenant can enable and
  disable those features and edit UIs as data (trees, Liquid templates, placement, design systems),
  never add code. The workflow system will later admit tenant code and needs its own sandbox
  ([architecture.md](architecture.md) › Direction); until then its expressions are Liquid too.
- **Tenant templates are data, never code.** The tree is validated against the registry; the
  renderer instantiates only registered types; `Type.GetType` on a stored name is never used.
- **Expressions** run on the server through the sandboxed engine (member allow-list), before the
  tree leaves the server.
- **Three permission sets** (ruling 2026-10-09), assigned **per page** (or shell), never per
  component, which would be impossible to nest and track:
  1. **Design**: may edit this page or shell (its template, overrides, zones).
  2. **Schema**: may see the schema available for design work (types, fields, queries, components
     and their parameters) without seeing any data. A designer builds the marketplace page knowing
     the item schema and never an item.
  3. **Data**: may read the data, which applies only at run time (viewing, or testing a design
     with real data). Design and data are separate: no data permission is implied by a design one.
  4. **Shared components** (ruling 2026-10-09): may edit a node definition (a base or a node
     override) that other instances and pages inherit from (§ 5a). Tenant-wide, admins only by
     default; the designer's base-edit mode is gated by it.
  5. **Publish** (ruling 2026-10-09): may make a draft live. It applies **per page and per
     component** (a node definition or an instance), because a component has a draft state of
     its own: without it there is nothing to preview before a change reaches every page that
     inherits it. Editing produces drafts; publish is the separate act, on either object.
  Workflows care about schema and data only.
  **What landed (2026-10-13).** Per designed page, `DesignedPagePermissions` generates the sets
  as permissions a role is granted: `DesignPage_{name}` (edit the draft, preview), 
  `ViewPageSchema_{name}` (list the page and read `api/crest/components?page=`, implied by
  Design), `ViewPage_{name}` (Data: view it at run time, unless the page allows anonymous
  callers) and `PublishPage_{name}`; `ManageDesignedPages` creates pages and implies every page's
  sets (the shell-level assignment, administrators by default). Shared components stay
  `ManageSharedComponents`. Left: Publish per component (a node definition or an instance has no
  draft of its own yet), and the sets of a shell's templates beyond `ManageTemplates` /
  `ManageAdminTemplates`.
- **Permissions are applied when the tree is built, on the server:** a node whose component or
  content the user may not see is not in the tree. The per-part read permission question in
  [permissions.md](permissions.md) lands here. The client never decides visibility.
- **Markup** is sanitized on save and on render.
- A template's author needs a template permission; the designer is gated like any admin page.

### 11. The designer edits Liquid

With Liquid the only authoring form, the designer works the way Gutenberg works on its text
format: it parses the template (Fluid exposes the parsed statements), shows the tags it knows
(`shape`, `zone`, the primitives) as editable nodes with the registry's schema in the property
pane, and shows everything else (loops, conditions, raw markup) as opaque blocks it preserves
verbatim. A template a person hand-edits may therefore become partly uneditable in the
designer; that is the accepted trade for one canonical form.

- **Preview is evaluation, just in time** (ruling 2026-10-09). On each edit the designer writes
  the tag back into the text and sends the template to the server, which parses it (cached by
  source), evaluates it with the real display context, applies the overrides and returns the
  display tree; the canvas renders that tree with the same renderer the live page uses. Liquid
  never runs in the browser: the tags call services, and preview enforces the data permission.
- **Overrides are not in the text.** The template text defines the base nodes; an override is a
  record bound to a node id or an instance id (§ 5a) and applied at evaluation. The designer
  therefore writes the **instance id into the tag** (`{% shape "Title", instance: "…" %}`) when a
  node is inserted, and never regenerates it, so overrides survive edits around them.

### 12. One expression engine

(ruling 2026-10-09: adopt this plan.) Surveyed on the same day, the code has three Liquid stacks
and two JavaScript stacks:

| Stack | Where | Notes |
| --- | --- | --- |
| Platform Liquid | `Crest.Liquid`, `Crest.DisplayManagement.Liquid` | Fluid 2.40; all tags and filters; strict member allow-list; cached parse; no step, time or recursion limits |
| Platform workflow evaluator | `Crest.Workflows/Server/Platform/Runtime` | the same parser, uncached, exposes `Workflow.Input/Output`; used by the stock tasks |
| Engine Liquid | `engine/…/Expressions.Liquid` | Fluid 2.31; its own parser and manager; exposes `Variables`, `Input`; none of the platform's tags, filters or types |
| Engine JavaScript | `engine/…/Expressions.JavaScript` | Jint; no CLR access; no limits configured |
| Platform scripting | `Crest.Scripting`, used by the stock tasks | different globals (`input()`, `output()`) |

No Crest code hooks the engine's Liquid context, so a workflow expression cannot see content,
users or queries, and a display template cannot see workflow variables.

**What the engine has that the display system needs.** An activity is described once from
attributes into an `ActivityDescriptor` with `InputDescriptor`s (type, default, UI hint, UI
handler). An enum input is a dropdown automatically; a dynamic picker is a
`DropDownOptionsProviderBase` registered in DI and named on the input (Crest already does this
for permissions, connections and content types); options can be fetched lazily per property;
the studio renders every hint through registered `IUIHintHandler`s. A value is an
`Expression { Type, Value }` resolved by a descriptor registry (Literal, Variable, Input, Liquid,
JavaScript) in an `ExpressionExecutionContext`. That is the component schema, the property pane
and the binding model this design needs, so:

1. The engine's `Expression` model, descriptor registry, handlers and input metadata are the one
   contract for workflows **and** display. A component descriptor is the activity describer
   applied to Blazor parameters; a node property and a slot are `Expression`s; the designer's
   property pane reuses the studio's UI-hint handlers and option providers.
2. The engine's Liquid handler is replaced by one that runs the platform's parser and template
   context, so the platform's tags, filters and member allow-list are the only Liquid. One Fluid
   version.
3. The platform-side workflow evaluators go: the stock tasks are ported to `Input<T>` expressions.
4. **`Crest.Scripting` is removed** (ruling 2026-10-09) and replaced by workflows entirely. A
   workflow can be written as a script or viewed and edited as a flow diagram; both are front ends
   for the same backend objects. Everything that calls `IScriptingManager` today (recipes'
   `[js: …]`, rules, the stock tasks) moves to the one engine.
5. A display `ExpressionExecutionContext` (item, route, user, tenant, query) joins the workflow
   one; the tree-emitting tag mode is added to the platform's shape tags.
6. Jint and Fluid limits (steps, time, recursion, memory) are configured in one place: the start of
   the sandbox that admin-granted JavaScript, C# or Python needs. Neither a C# nor a Python
   provider exists in the vendored engine today.

**What landed (2026-10-13).** Step 2: `PlatformLiquidTemplateManager` (`Crest.Workflows/Server/
Expressions`) replaces the engine's template manager, so a workflow's Liquid expression is
parsed and rendered by the platform's (cached parser, tags, filters, member allow-list) with
`Variables`, `Input` and the workflow ids as properties; the workflow's variable types are
admitted to the platform's allow-list. One Fluid: the engine builds against Fluid 2.40. Steps 3
was done with the stock activities (workflows.md). Not done: step 4 (`Crest.Scripting` removed),
which workflows.md sequences in its Retire step after the generated surfaces, with recipes'
`[js: …]` and the layer rules moving to the engine; step 5 (a display
`ExpressionExecutionContext`); step 6 (limits in one place), which the ruling ties to the start
of the sandbox that is still parked; the engine's own Liquid filters (`base64`, `keys`) are not
in the platform's set.

The remaining parts of the engine's model (how a template declares inputs, limits, the designer
hints for zones) are built on this, not beside it. The registry, the request path, the four pipelines and
the one access machinery are planned in [workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery) (2026-10-10).

## Data model

| Entity | Where | Notes |
| --- | --- | --- |
| Component descriptor | Code, generated at build; cached per tenant/theme at run time | The registry. Not content. |
| Binding (code) | Shape table, from attributes and `Describe(...)` | Per theme, per feature. |
| Template (and Layout) | Content item, versioned | The tenant's bindings. |
| Placement | `placement.json` per feature + the Placements module's document | Unchanged; its editor becomes a Blazor page. |
| Page | Content item (Autoroute) or `@page` component | Unchanged. |
| Layer rules | Layers module documents | Widgets are retired (ruling 2026-10-12): a layer rule places a component instance into a zone key while the tree is built. |
| Design system | Content item (planned) | design-systems.md. |
| Display tree | Not stored; built per request, cacheable per (item version, display type, user scope) | The wire format. |

## Rework plan (in order, each a module reworked in place)

1. **Registry and schema.** ✅ 2026-10-12 (except the route table, below). The source generator,
   the descriptor, the attribute, one registry replacing the three. The route table reads it. No
   rendering change yet. What landed:
   - **Contracts** (`Crest.DisplayManagement.Abstractions/Components`, browser-safe):
     `[BlazorComponent]` with palette and rule metadata, `[ComponentParameter]` (now also
     `Choices`, static options that make a string parameter a `Choice`), `[ComponentZone]`,
     `[assembly: ComponentCatalog]`, `ComponentDescriptor`, `IComponentCatalog`,
     `IComponentRegistry`/`ComponentRegistry`. Added beside them: `ComponentSchema` (CLR type →
     kind and editor: string/char/Guid/Uri → text, numbers → number, bool, enum → `Choice` with
     the names, dates and times, `EventCallback` → `EventHandler`/action, collections → `Array`,
     `RenderFragment`/`RenderFragment<T>` → a zone, anything else `Object`),
     `ReflectionComponentCatalog` (the dev fallback, same output as the generator),
     `ComponentCatalogs.ForAssembly` (the generated catalog if the assembly names one, else
     reflection; read once per assembly) and `ComponentParameters` (schema validation and
     conversion, sharing its JSON options with `DisplayTreeRenderer`, so what validates renders;
     enums by name).
   - **Source generator** (`Crest.SourceGenerators/ComponentCatalogGenerator.cs`): per assembly,
     an internal `IComponentCatalog` with full descriptors (inherited `[Parameter]`s base first,
     capture-unmatched and cascading ones left out, `[EditorRequired]` → required), the shells
     from `[assembly: CrestShell]` (every shell without one), `Module` = the assembly name, and
     `[assembly: ComponentCatalog(typeof(...))]`. It reads C# only: Razor's own generator output
     is invisible to other generators, so a component is marked and its parameters declared in
     the code-behind. OCSG003/OCSG004 warn about a marker or `[Parameter]`s left in `.razor`
     markup; OCSG005 about a marked component it cannot catalog (generic, abstract, inaccessible).
   - **Generator wiring**: `buildTransitive/Crest.DisplayManagement.Abstractions.targets`, shipped
     in the abstractions package with the generator in `buildTransitive/analyzers/`, so every
     package consumer gets it, directly or through `Crest.Components` (a host's `blazor-wasm`
     libraries). It applies to Razor projects named `*.BlazorWasm` or `*.Client`, or any project
     setting `<ComponentCatalog>true</ComponentCatalog>` (`Crest.Components`, `Crest.Icons`), and
     enables the interceptor namespace the same assembly's shape generators need. In this repo
     `Directory.Build.targets` imports the same file for the non-platform projects with the
     generator as a project reference.
   - **Server registry** (`Crest.DisplayManagement/Components`): `IComponentRegistryManager`
     builds one registry per shell and theme from the enabled features' assemblies plus
     `ComponentRegistryOptions` (Crest.Server adds `Crest.Components`, `Crest.Icons`, the theme
     clients and the module client libraries, each library limited to its shell bucket). A
     component's feature is its type's (`ShellBlueprint.Dependencies`), else the module its
     library is named after (`X.BlazorWasm` → `X`); a disabled feature's component is absent;
     catalogs apply in feature order, a theme's only for that theme and its base themes. Cached
     in a tenant singleton like the shape table, so it goes with the shell. The scoped
     `IComponentRegistry` is the request's shell (`IShellKindAccessor`; Crest.Server's reads the
     shell bucket, else admin for an admin request, else site).
   - **Browser registry** (`Crest.Shell/ShellComponentRegistry.cs`): the document's shell over the
     shared libraries, its theme client, and the shell's eager and lazily loaded module libraries
     (`LazyModules`), rebuilt when a lazy module loads. Registered at WASM boot
     (`ClientWebAssemblyHost`, the one boot path every shell shares), so `DisplayTreeRenderer`
     resolves `IComponentRegistry` in the browser.
   - **Replaced**: `ICrestBlazorComponentRegistry` and `AssemblyScanningCrestBlazorComponentRegistry`
     are deleted; `BlazorComponentShapeBindingResolver` and `CrestBlazorComponentPart`'s
     driver use `IComponentRegistry`; the part's parameters are validated against the schema on
     save (`CrestBlazorComponentPartHandler`) and bound to their types on display.
   - **Retired**: the admin client `DisplayManager`'s shape bindings (`NewShape`, `RenderShape`,
     `ResolveComponentType`, `BuildShapeBindings`), `ShapeView.razor`, the client `Shape`/
     `ShapeTemplate`/`ShapeAttribute` and the unused `AdminStatus.razor`, and the duplicate
     `Crest.Components/Shapes/Shape.cs`. `DisplayManager` keeps only shell state.
   - **Examples**: `CrestHeading` and `CrestQuote` carry display name, category, icon and parameter
     metadata (the heading's level is a `Choice` of H1–H6); `CrestComponent`'s base parameters are
     grouped (`Style` advanced, the events advanced, the cultures hidden).

   Left (what landed 2026-10-13 is struck through):
   - ~~**The route table reads the registry**~~: the catalogs describe an assembly's module
     pages (`IComponentCatalog.DescribePages`, `PageDescriptor`: routed type, template,
     anonymous), never as registry entries (§ 6a); `AssemblyRouteComponentScanner` reads them
     through `ComponentCatalogs.ForAssembly`.
   - ~~**Page regions as zone keys** (decision 6)~~: done 2026-10-13, see decision 6.
   - **Annotating the Radzen-derived library**: 2026-10-13 annotated `Label`, `Badge`,
     `Alert`, `Row`, `Column`, `Panel`, `Fieldset` (beside `Image`, `Button`, `Link`, `Card`,
     `Stack`). Not annotated: `CrestIcon` and `CrestColorPicker` declare their parameters in
     markup (OCSG003), `CrestMarkdown` renders markdown in the browser (no server sanitizing), the
     layout hosts' names collide with the theme layouts and the zone keys, and the form editors
     are replaced by the form primitives (widgets.md). The original list of candidates, as primitives
     (decision 2) — `CrestText`, `CrestLabel`, `CrestLink`, `CrestImage`, `CrestIcon`,
     `CrestButton`, `CrestBadge`, `CrestAlert`, `CrestMarkdown`, `CrestStack`, `CrestRow`/
     `CrestColumn`, `CrestCard`, `CrestPanel`, `CrestFieldset`; layout hosts with zones —
     `CrestLayout`, `CrestHeader`, `CrestBody`, `CrestFooter`, `CrestSidebar`; menus —
     `CrestMenu`, `CrestPanelMenu`; editors (the editor facet) — `CrestTextBox`, `CrestTextArea`,
     `CrestNumeric`, `CrestCheckBox`, `CrestSwitch`, `CrestDatePicker`, `CrestDropDown`,
     `CrestColorPicker`, `CrestHtmlEditor`. Generic components (`CrestDataGrid<TItem>`, charts)
     cannot be named in a tree as they are; they need a closed wrapper.
   - ~~The registry has no facet or editor-kind entries yet~~ (the editor facet is `Edits`
     since step 5, and `api/crest/components` lists it since 2026-10-13), and a theme scope is only applied when a caller passes the theme
     id; the scoped registry passes none and admits every enabled theme serving the shell.
2. **Display tree and renderer.** ✅ 2026-10-12. The DTO (`DisplayNode`, not `CrestModel`, per
   the 2026-10-12 ruling), the tree renderer library, the component binding in `ShapeBinding`, a
   Blazor `IDisplay` beside `IHtmlDisplay`. First consumer: the admin content-item display page
   renders a content item through it. What landed:
   - `IDisplayTreeBuilder` / `DisplayTreeBuilder` (`Crest.DisplayManagement/Components`): the
     shape-to-tree mapper. Binds through `IComponentRegistry.TryResolve` in `DefaultHtmlDisplay`'s
     order, checks properties against the schema (unknown or wrong-typed ones dropped and
     diagnosed), maps named zones and a shape's items (`ChildContent`) with the zone's allowed
     list and min/max, maps wrappers to wrapper components, gives deterministic instance ids
     (item, part, field, shape type, position), drops a component whose permission the caller
     lacks (`IAccessDecision`), and runs under `DisplayTreeOptions` (time, nodes, depth, size;
     an overrun becomes a `DisplayError` node, never retried).
   - Unbound shapes fall back to `IHtmlDisplay` and enter the tree as a sanitized `Markup` leaf
     with a `markup-fallback` diagnostic: the transition path, removed in step 7.
   - `ShapeBinding.ComponentName` / `ShapeBinding.ForComponent`: a component binding renders the
     shape's node and zones in one pass (`ComponentShapeRenderer`: `HtmlRenderer` +
     `DisplayTreeRenderer`); `BlazorComponentShapeBindingResolver` returns it instead of
     rendering each shape with its own `HtmlRenderer`.
   - `GET api/crest/content-items/{id}/display?displayType=Detail` returns the tree (ViewContent
     through the decision; an editor may display the latest draft).
   - `/Contents/ContentItems/{ContentItemId}/Display` in the admin shell, with the tree handed
     over by `[PersistentState]`, a route permission and a "Display" action in the list.
   - `CrestModel`'s unused tree parts (`CrestShapeMetadata`, `CrestZone`, `CrestShapeDiagnostic`,
     `CrestModelSource`, items, classes, attributes) are removed.

   Left: ~~the browser registers no `IComponentRegistry` yet~~ (step 1 registers it, 2026-10-12);
   ~~the tree is not cached~~ (versioned and validated by ETag since 2026-10-13, decision 12); templates as a binding target (step 4); ~~the
   `DefaultHtmlDisplay` events (`Displaying`, `Processing`) do not run for bound nodes~~ (the
   tree builder runs the shape descriptor's and the shape's own displaying and processing phases
   before it resolves a binding since step 3, and for a caller-chosen component too since
   2026-10-13; the global `IShapeDisplayEvents` and the `Displayed` phase stay with the HTML
   render: they serve its output cache and profiler); ~~the generic resolver still keys by the old `ICrestBlazorComponentRegistry`~~ (it keys
   by `IComponentRegistry` since step 1).

   Verified live (2026-10-12, dev host): setup imports the recipe's `CrestCounter` part, the
   endpoint returns the tree to an admin and to anonymous callers for published content (404
   for a workflow definition), and the admin page prerenders it (`Count: 5`). Fixed on the way:
   the part's driver was registered as a bare `IContentPartDisplayDriver`, which the display
   coordinator never calls, so the part had never displayed (now `UseDisplayDriver`); the Razor
   shape engine rendered templates outside a view with `isMainPage: true`, so a fallback leaf
   came back wrapped in the site layout (now as a partial, as inside a view); stored `"true"`
   did not bind a `bool` (a lenient converter in `ComponentParameters.JsonOptions`). Until
   step 3 the item's root (`Widget`, `Content`) has no component, so the whole item is one
   markup leaf and its components are rendered to HTML inside it, the "render and feed in" the
   2026-10-12 ruling forbids; step 3 removes it by giving those shapes components.
3. **Site shell on the tree.** ✅ 2026-10-12, except the widget deletions (below). What landed:
   - `SitePageBuilder` (`Crest.Server/Display`) and `GET api/crest/site/page?path=`: the path
     through Autoroute (the home route for `/`, `Contents/ContentItems/{id}` for an item without a
     route), `ViewContent` for the caller, decision 10 (a type needs a `Content__{Type}` or
     `Content` template unless the site setting `SiteDisplaySettings.RenderUnassignedContent` is
     on), then the `Layout` shape filled (the item into `Content`, the `alias:main-menu` menu into
     `Navigation`, the `ILayoutTreeContributor`s) and mapped and filtered as one tree. A miss is
     the layout with `NotFound` (404), a refusal with `Forbidden` (403).
   - The site client (`Crest.SiteTheme/wasm`): `ContentPage` (`@page "/"` only since the
     2026-10-13 ruling - the real path comes from `IShellRequestPath` while prerendering and
     from the navigation manager once interactive, and the site `Routes` renders it for every
     URL it has no page for; the page as `[PersistentState]`, the status set while
     prerendering through `IShellResponseStatus`), and the components `Layout` (zones `Navigation`, `Header`,
     `Messages`, `Content`, `Sidebar`, `Footer`), `Content` (zones `Header`, `Meta`, `Content`,
     `Tags`, `Actions`, `Footer`), `NotFound`, `Forbidden`; `CrestSite.css` on the tokens.
   - `App.razor`'s site branch loads the design system (tokens, component styles, icons,
     scripts). `Menu`/`MenuItem` components (`Crest.Components/Interaction/LinkMenu.cs`); the
     menu module resolves each item's text and link when it is displayed, and the tree builder
     now runs the processing phase, where a menu reads its items.
   - The MVC site routes are out of the request path: Autoroute's and the home route's dynamic
     routes are gone (their address schemes still generate URLs), and `Contents/ContentItems/{id}`
     only generates (`GenerationOnlyRouteConstraint`). The site Liquid views, the Bootstrap Clean
     Blog assets and `BlazorCounter.razor` are deleted; a minimal `Layout.cshtml` stays for the
     modules' remaining MVC site pages (account screens) until step 7 replaces them.
   - Two hosting details the live run found: component endpoints are ordered ahead of
     conventional routes and the `/{*Path}` catch-all swallowed `/Error/{status}`, every MVC
     site route, the legacy admin frame and every module asset (the "last order" convention
     never took effect) - resolved 2026-10-13 by the ruling that there is one routing system
     on the Blazor side: the catch-all is gone, the shell middleware resolves site paths
     through the route table and forwards a legacy-owned path to MVC
     (`ILegacyRouteOwnership`); and Blazor writes no body for a page that set
     404 while rendering, so the page's status is applied as the response starts and the status
     code pages are switched off for it (it renders its own error page).
   - Verified live (2026-10-12): `/` and unknown paths answer 404 with the site frame; a published
     `Widget__BlazorComponent` template turns the counter item into `Layout → Container →
     CrestCounter`; a layer placement shows on the home page; the editor tree, the HTML path, the
     template preview and the admin Templates, Layers and Placements pages work.
   - FlowPart (the widget flow) is retired; BagPart stays in Flows, which now holds the content
     card views and shapes the BagPart editor uses.
   - **Widgets deleted (2026-10-12, after composites, the UI action contract and overrides
     landed):** `Crest.Widgets`, `Crest.Forms` (every Forms type was a widget), ReCaptcha's Forms
     feature, Facebook's Widgets feature, the `BlazorComponent` type with `CrestBlazorComponentPart`,
     the Alias and Autoroute widget shape providers, Search's `SearchForm` widget type,
     `ListWidgetTypeDefinitionsAsync`, the widget browser tests, and the widget types and content
     in the recipes (the SiteTheme blog recipe's footer widget and flow bodies included). The dev
     recipe places the counter in the site footer with a layer rule instead. What each deleted
     widget did and what replaces it (most of the forms and content widgets still need a
     component) is tracked in [widgets.md](widgets.md).
4. **Templates module rework.** ✅ 2026-10-12. Templates are content (`Template` type,
   `TemplatePart` { Name, Theme, DisplayType, Source, Description }, drafts and publish), the
   published set cached and re-read on publish. `TemplateTreeEvaluator` runs Fluid with the
   platform's filters and member access but its own tags (`TemplateTreeParser`), so a template
   emits nodes, never HTML:
   - `{% component "Name", id: "x", text: Model.Title %}` (a leaf; arguments are parameters,
     snake case for Pascal, checked against the schema), `{% compose "Name" %}…{% endcompose %}`
     (with children), `{% zone "Header" %}…{% endzone %}` inside a compose, `{% children "Content" %}`
     (the bound shape's own zone, filled by the tree builder in the same build), `{% shape "Menu",
     alias: "alias:main-menu" %}` (a platform shape built by its shape table and mapped in).
   - § 4a: `nodes.{id}.{output}` and `page.{variable}` are bindings, `event` inside
     `{% on "Event" %}…{% endon %}`; actions `set_variable`, `open`, `close`, `toggle`,
     `run_workflow "id", into: "var", arg: value`, `navigate`, `submit`; slots
     `{% slot "parameter", "Query", param: value, path: "items" %}` inside a compose (tag arguments
     take plain expressions only, no filters or function calls).
   - Free text becomes sanitized markup leaves; explicit ids are scoped under the instance id
     (`{instance}.{id}`); a name that is a published template and not a component is a
     composite, parsed and evaluated on its own with only its arguments as `Model` (audit item 1),
     cycles and depth capped; every evaluation shares the page's deadline and node budget and has
     a step budget (`DisplayTreeOptions.MaxTemplateSteps`, `MaxTemplateDepth`).
   - `TemplateResolver` implements decision 7 (names most specific first, then theme and display
     type, theme, display type, none); `TemplateDisplayBinding` is the `IDisplayTemplateBinding`
     the tree builder asks before the component; `TemplateValidator` validates on save and on
     publish. `api/crest/templates` (list, get, save draft, publish, delete, validate, preview) and
     the admin Templates page (edit, validate, publish, preview a draft for a content item).
   - Removed: the HTML Liquid shape binding, the MVC editor and preview, the admin-template
     variants (an admin template is one whose theme is the admin theme, under
     `ManageAdminTemplates`).
5. **Editors as trees.** ✅ 2026-10-12. Editor components declare what they edit
   (`[BlazorComponent(Edits = [...])]`, `IComponentRegistry.TryGetEditor`, `*` for the JSON
   fallback): text, number, yes/no, date, content picker, option picker, HTML, Markdown, link,
   title, permalink. They read and write through one `ContentEditorContext` (moved out of
   `CrestFormManager`, with its visibility conditions and picker cascades), cascaded by the
   `ContentEditor` root, which publishes the edited content as an output. The server builds the
   tree as shapes (`ContentEditorTreeBuilder`, `GET api/crest/content-items/{id}/editor`,
   `new-{type}` for a new item), so a `ContentEditor__{Type}` template lays a type's editor out.
   The admin editor page renders it and saves the published content; `CrestFormManager` renders
   the same editors from the type's metadata. Rendering the option picker in the admin editor (it had only
   rendered in the Parties pane) surfaced three bugs in it, fixed 2026-10-12: its text binding read
   a dictionary entry as a member (rows now carry `Text`), a single-choice picker passed its id
   list as the value, and its columns passed a `TItem` the column does not take.
6. **Headless endpoint, HtmlRenderer path, Layers as a build step, Placements editor.** ✅
   2026-10-12. `?format=html` on the display endpoint and `IDisplayHtmlRenderer`; Layers keeps its
   layers and rules and gains `LayerPlacement`s (a component, its parameter values, a zone key, a
   position) that `LayerTreeContributor` adds to the layout while a page is built; the result
   filter, `LayerMetadata` and the widget admin are gone; `api/crest/layers` and the admin Layers
   page; `api/crest/placements` and the admin Placements page replace the MVC editor and its
   content-type shortcuts. `GET api/crest/components?shell=` serves the registry's schema.
7. **Retire** — partly, 2026-10-12. Done: the site's Liquid views, the Templates HTML binding, the
   admin client's `DisplayManager` (pure shell state) is `AdminShellState` in
   `Crest.AdminTheme.State`. Not done: `IHtmlDisplay`, the Razor and Liquid shape engines and the
   tag helpers still serve the stock MVC pages (the legacy admin frame, the account screens) and
   the markup fallback leaf; `BlazorComponentShapeBindingResolver` still renders a component
   inside such a fallback. They go page by page as each MVC page gets a component page. 2026-10-13:
   the HTML and Markdown body shapes have components (`HtmlBodyPart`, `MarkdownBodyPart`, a
   sanitized markup parameter), so the sample site's bodies no longer fall back. The retirement
   itself still waits on the stock MVC pages (about 1,250 Razor views across the modules, the
   account screens and the legacy admin frame among them) getting component pages.

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
  design's zones are nodes in the tree.
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
    loading ([globals.md](globals.md)) plus the global data registry, so no module
    reimplements either.
  - `Crest.Components.Primitives` holds the primitive components; modules register their
    primitives there.
  - `Crest.Components.Templates` holds the higher-level template trees modules ship (composites,
    default templates); the Templates module's tenant data layers over them.
  - The rest of the data-layer chain (`Crest.Data`, connectors, query, the API surfaces) is
    decided later ([queries.md](queries.md)); the order sketched so far is not committed.

  **What landed (2026-10-13).** `Crest.Data.Types` (a platform library, no server framework,
  referenced by `Crest.Data`): `ValueTypeDescriptor` (name, query column type, workflow type,
  field types, editor and display components, CLR type, owning module), the base types over the
  content fields, `IValueTypeRegistry` and `AddValueType`; `Crest.Server` registers the registry
  and `Crest.Money` registers `Money` (`PriceField`). `Crest.Global` is renamed
  `Crest.Data.Global` (project, feature id, namespaces, and its users in Crest, Fruitful and the
  host; Fruitful needs the repacked package). Not done: the global data registry beyond the
  store, the query system and the display schema reading the value type registry, and the
  `Crest.Components.Primitives` / `.Templates` projects (open question: the registered
  primitives derive from the Radzen-derived base classes in `Crest.Components`, so a separate
  primitives project means moving that whole library or leaving those primitives behind).
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
- **Components are one concept with facets** (primitive, composite, editor, host); pages are a separate kind; open
  zones are the universal registration point; page regions fold into zones (Vocabulary, § 6).
- **The design system is localizable by default**; a Master mode unlocks raw control with
  break-localization notices (§ 9).
- **Custom data types are not a prerequisite** for rendering: a tenant content type has parts and
  fields with drivers and builds a tree like any other; what blazordesigner.md meant by them is the
  editor components above.

## Rulings (2026-10-12)

- **Shortcodes are retired into Liquid templates.** Every shortcode becomes a component or a
  template; the Shortcodes module, template shortcodes and the `shortcode` Liquid filter go. Body
  text holding `[image]`/`[asset_url]` is migrated to media component nodes, keeping the id and
  path references ([shortcodes.md](shortcodes.md) becomes the migration record).
- **Content editing is a stripped-down designer.** The marketing team's WYSIWYG is the designer
  in a reduced mode: the same component trees, a palette limited by role. There is one system for
  content and UI structure; "content" is a tree an editor fills, not a separate body format.
- **Composition is one tree, rendered once.** A sub-component is never rendered and fed into its
  parent. The whole tree is assembled at run time and rendered in one pass.
- **Render limits.** Every evaluation has a time limit and step, recursion, depth and size caps.
- **Order:** the display system is implemented first (the rework plan below); the security
  audit of these rulings follows.
- **Work within the platform's display management, no parallel modules.** The display tree's
  node is a **new record** (not `CrestModel`, which stays the content-item wrapper the editors,
  Parties and host modules use; its unused tree parts fold into the node) and it lives, with the
  component registry contract, the descriptors and the tree renderer, in
  **`Crest.DisplayManagement.Abstractions`**, reworked in place. The server-only pieces (the
  shape-to-tree mapper, the component and template bindings, the Blazor `IDisplay`) go in
  `Crest.DisplayManagement`. The component source generator extends `Crest.SourceGenerators`.
- **Widgets are retired** (second batch, 2026-10-12). A widget was a content item placed into a
  zone; components placed into zone keys do that now. The Widget stereotype, `WidgetsListPart`,
  the widget content types, the `BlazorComponent` type and `CrestBlazorComponentPart` go. What
  Layers adds survives as **layer rules**: a rule (all blog posts, signed-in users) places a
  component instance into a zone key as a conditional fill applied while the tree is built.
- **Custom UI at runtime** (third batch, 2026-10-12). What widgets gave tenants (a configured
  block saved and reused) is met without tenant code:
  - **Composites are first-class components.** A composite template declares its inputs (typed
    parameters with defaults and choices), its outputs (each one an inner node's output) and its
    palette entry (display name, category, icon); it is validated like a coded component,
    listed with them in the registry schema, and placed by name anywhere a component is.
  - **Behaviour is workflows only**, through a typed contract: a workflow opts in to being run
    from the UI with a *UI action* trigger that declares its inputs; a display action may run only
    such a workflow, and its arguments are checked against the declared inputs on save and on run.
  - **Overrides (§ 5a)** let a tenant save a configured component as its own variant (a node
    override, placed by name like a component) and adjust one placement (an instance override),
    literal values only.
  - **Widgets retire after these land**, not before; the counter stays a component, placed by
    templates and layers. (Done the same day.)
  - **Sandboxed custom elements are parked** as a later, separately permissioned ruling: tenant
    HTML/JS in an isolated iframe (own origin, no cookies, strict CSP) talking to the tree over a
    narrow message bridge that can only read bound values, publish outputs and request actions.
    Tenant C#/Razor compiled at run time stays ruled out; real code enters as vetted plugins.
  - **What landed (2026-10-12).** Composites: `TemplatePart.IsComposite` with `Inputs` (name, type
    `string`/`number`/`boolean`/`choice`/`object`/`array`, required, default, choices), `Outputs`
    (name → an inner node's output) and a palette entry; placing one checks the arguments
    (unknown dropped, converted, defaults filled, required reported) and wraps it in a node whose
    `OutputAliases` re-publish the outputs under the instance id (`DisplayState.RegisterAlias`);
    save validation samples the inputs and checks that each output names a real inner output;
    `api/crest/components` lists composites (`source: "template"`) with the coded components. The
    UI-action contract: the `UiActionTrigger` workflow activity declares `name: type` inputs; the
    run endpoint refuses a workflow without it (404) and arguments that do not match (400), the
    tree filter leaves out actions on such workflows, and template validation checks each
    `run_workflow` through `IDisplayActionValidator`. Overrides: `ComponentOverridesDocument`
    with variants (a name, a base component, composite or variant, literal values; resolved down
    the chain base first, cycles refused; placed by name, the tag's own arguments winning; listed
    as components) under `ManageSharedComponents`, and instance overrides applied by the tree
    builder to every tree (`IDisplayTreeOverrides`), never over a binding, a slot or an event;
    `api/crest/templates/overrides` and a section on the admin Templates page. Still to do:
    the designer UI for all of this.
- **Interaction through the tree** (second batch, 2026-10-12): component outputs, a page state
  store, bindings to outputs, event payloads and client actions (set, open, close), § 4a.
- **`InteractiveAuto` at every shell root stays** (second batch, 2026-10-12); per-node islands
  are not added. Shared page state needs one interactive root.
- **`Crest.Mvc.Abstractions`** (new, 2026-10-12) holds the MVC contracts that lived in the display
  abstractions (`IUpdateModel` and model binding, tag helper discovery, filter extensions), same
  namespaces, so `Crest.DisplayManagement.Abstractions` drops the server framework and loads in
  the browser too.

### Security audit of the 2026-10-12 rulings (parked until the display system lands)

Recorded so it is not lost; to be ruled on after the rework plan's first steps.

1. **Compose parsed templates, never concatenated sources.** Joining template sources into one
   text lets a child's syntax (an unclosed `{% if %}`, a stray `{% endif %}`, `{% raw %}`) and its
   `{% assign %}` reach the parent. Parse and validate each template on its own, cache it by
   version, link the parsed templates into one tree, evaluate in one pass with a scope per child
   holding only its declared parameters. This meets "one tree, rendered once" without textual
   inclusion (§ 3 already reads this way).
2. **Typed text is data.** In the stripped designer, what an editor types (including `{{` or a
   code sample) is stored escaped as a markup value, never written into template source. Migrated
   bodies enter as markup leaves, never as live Liquid.
3. **The member allow-list is the sandbox (§ 10) and needs trimming for tenant-authored
   templates.** A template is evaluated as the viewer, so an editor's template opened by an admin
   reads with admin rights (confused deputy) and can exfiltrate through markup. Not for tenant
   templates: `Request` (cookies, headers, form, query), `encrypt`/`decrypt`, the `liquid` filter,
   `users_by_id`/`users_by_name`/`user_email`, `Site.SuperUser`, `shape_render`/`shape_stringify`,
   `console_log`. Data reads go through the query pipeline only. (Proposed; the user's position was
   "no sandbox needed" - to discuss.)
4. **Preview** of another user's draft runs with the reviewer's rights: evaluate under the
   author's data rights, or block outbound requests on preview with a strict CSP.
5. **A Content-Security-Policy** on rendered pages (`img-src`, `connect-src`, `form-action`) as a
   backstop against exfiltration through injected URLs.
6. **Typed parameters validated**: URLs refuse `javascript:`/`data:`; raw CSS ("Master" mode)
   allows `url()` exfiltration and overlays, so it takes its own permission.
7. **The palette is enforced on the server** on every path (designer, API, recipes, deployment),
   not only in the designer UI.
8. **Actions only on interaction**: no tag starts a workflow during evaluation; event handlers fire
   on a user's action with antiforgery.
9. **Caches vary by viewer** (the scope signature): a tree built with an admin's permissions is
   never served to anyone else; signed URLs inside trees follow the media rule.
10. **The tree reaches the browser** as persisted state: everything in it, resolved values on
    undisplayed nodes included, must be the viewer's to see.
11. **Overrides** that target inherited nodes take the shared-components permission, checked on
    the server.
12. **Cycles**: a composite that places itself (directly or through others) is refused on save and
    capped by depth at render.
13. **Limits fall back, they do not retry**: evaluation is deterministic, so a timed-out template
    times out again. On timeout or error serve the last good published tree (cached) or an error
    node for the subtree, record it, and trip a per-template breaker; retry only transient data
    fetches (connections already do).

## Audit (2026-10-09): what is settled, what contradicts, what is open

A docs-only pass over this document, [blazordesigner.md](blazordesigner.md),
[page-regions.md](page-regions.md), [shells-and-themes.md](shells-and-themes.md),
[blazor-web.md](blazor-web.md), [design-systems.md](design-systems.md),
[content-items.md](content-items.md) and [admin-routes.md](admin-routes.md), to find what a
concrete implementation plan still lacks. Nothing here is implemented.

### Settled (rulings recorded above and in the linked docs)

| # | Standard | Ruling |
| --- | --- | --- |
| 1 | Value types | One registry in `Crest.Data.Types`; a type links field type, editor, display primitive, query column type, workflow type. Owning modules register into it. |
| 2 | Primitives | Modules supply primitives too, through the component registry (ruling 2026-10-09): a module that registers a value type registers the custom-coded primitives that edit and display it, so the type system and the UI system connect through the registry. `Crest.Components.Primitives` ships the base set; the registry's `primitive` facet, versioned per module, is the headless contract. Trust sits with the installer who builds the module in. |
| – | Authoring form | Liquid only (Fluid, MIT): the platform's tags emit nodes; the display tree is the runtime format, never authored (§ 5). |
| – | One engine | The workflow engine's expression model (descriptors, handlers, `Expression {Type, Value}`, input metadata and UI hints) is the one contract; the platform's Liquid is its Liquid implementation; `Crest.Scripting` is removed and replaced by workflows (§ 12). |
| 5 | Actions | Workflows only (follows from the scripting ruling): a button or form runs a workflow, navigates, or submits the current item; nothing else is code. |
| 4 | Context and bindings | `Route` plus queries only; `Item`, `User`, `Tenant` are system queries; slots bind `{ query, parameters, path }` with parameters as bindings (filtering); queries read and own connections, workflows act and call queries, never the reverse; a slot never binds a write; the query contract moves ahead of the Templates rework. |
| – | Overrides | Every tenant edit is an override over a shared default, keyed by stable node id; data-derived values are not editable, slots are rebindable within the component's value types (§ 5a). |
| – | Permissions | Design, schema, data and publish per page; publish also per component; shared-component editing tenant-wide (§ 10). |
| 6 | Zone keys | Registry entries with a schema: well-known keys shipped by Crest, module-owned keys under a prefix, allowed fillers, required-on-layout; ordering by default and settable priorities, reorderable per zone or per page's zone through overrides; zones are components; every component declares single or multiple instances per page. |
| 7 | Template hierarchy | The alternates chain as data: shape name + optional display type + theme, most specific first; theme is the one placement qualifier (shell and theme are one for this); versioning from the publish model; recipes export by component id. |
| 8 | Theme as data | Both: compiled module themes that ship a set, and data themes (the set alone), one registration and import/export path; public marketplace for data themes only; a theme = structure + design system, to be separated in a later `Crest.Themes` plan. |
| 9 | Designed pages | Data-defined route-table entries, published only; a page is its own kind, not a component: never a child, holds the route/Autoroute/document fields, has components or composites under it, drafts and publish but no overrides. |
| 10 | Tree format | One typed node format; `IShape` mapped once where drivers still build it; no page is generated for a content type without a template; a default `Content` Liquid template of permitted fields comes later, on by a per-tenant setting. |
| 11 | Edit mode | An overlay layer above the unmodified page: nodes located by instance-id markers, measured in JS, hover/selection/snap drawn from rectangles; a drop becomes a template edit; inline text editing is the only temporary surface. |
| 12 | Caching | Client-held filtered trees validated by a composite version (ETag); a small server cache of unfiltered trees per page, item version and culture; no per-user server cache; anonymous pages share; drafts never cached. |
| 14 | Tokens | Reference palette → one flat semantic namespace; categories and scales fixed; extensible by name with per-tenant/user overrides and audience-scoped presets; inheritance compiled at publish (design-systems.md). |
| – | Node identity | Component id (cross-tenant, default tenant edits) → node id (per-tenant copy) → instance id (a placement, not page-scoped); an override binds to exactly one of node id or instance id; resolution base node → node overrides → instance → instance overrides; a change propagates to every descendant that does not set the value (§ 5a). |
| 3 | Style schema | Localizable by default (logical, token-bound, direction-aware); Master mode unlocks raw CSS with break-localization notices; Master edits marked in the template. |
| – | Model | Fluid (WordPress-FSE-like): zones are nodes a template author places anywhere; modules contribute by **key**; drivers + placement are a **generator** for editors and field lists, not the model, and never produce a page for an unassigned content type (decision 10). "Zone" is the word for a child list; "slot" is a data binding on a component. |
| – | Components | One registry, one concept with facets (primitive, composite, editor, host); pages are separate; schema vocabulary from Plasmic's MIT `registerComponent`. |
| – | Pages | First-class and distinct from components and templates: they own the URL, the shell bucket, route authorization and document concerns (§ 6a). |
| – | Rendering | `InteractiveAuto` at every shell root, prerendered; the tree crosses once via `[PersistentState]`; no per-node islands. |
| – | Expressions | Liquid now; the one engine later (architecture.md › Direction). |
| – | Module homes | `Crest.Data.Types`, `Crest.Data.Global`, `Crest.Components.Primitives`, `Crest.Components.Templates`; a small neutral library for the renderer, tree DTO and registry contracts. |
| – | References | Oqtane primary (MIT, cloned); Umbraco, Payload, Strapi for data models; Plasmic MIT parts only; WordPress vocabulary only. |

### Contradictions to resolve by rewriting, not by ruling

These are places where a doc says the old thing and the ruling above says the new; the rewrite
of this document (after the open rulings below) closes them.

1. **This document's § 6 still declares zones in the layout** ("Header, Navigation, Content,
   Footer, … as zones, so the zone list is data"). Under the fluid ruling there is no fixed zone
   list: a `Zone` node carries a contribution key, may appear anywhere and any number of times,
   and the Layers rules and the Placements editor target keys. § 6 is rewritten around that.
2. **[page-regions.md](page-regions.md)** describes `IPageRegionContributor` / `PageRegionRegistry`
   / `<CrestPageRegion>` with region keys owned by the page's module. That is exactly a zone with
   a contribution key; the doc should say it is the first implementation of the key mechanism
   and will be renamed and generalized, not kept as a second seam.
3. **[blazordesigner.md](blazordesigner.md) § 3** defines `IWorkbenchComponentRegistry`,
   `[WorkbenchComponent]`, `[WorkbenchProperty]` and a `ComponentNode` with a fixed grid
   (`Row`, `Column`, `RowSpan`, `ColumnSpan`). The registry is this document's one registry; the
   attributes are its attribute; the node is `Template.Tree`'s node, whose layout is a container
   primitive's parameters (stack, grid, flow), not fixed grid coordinates on every node. The
   designer doc's Phase 1 is to be rewritten to consume, not define, these.
4. **[blazordesigner.md](blazordesigner.md) § 0** says a placed component instance is content
   (`CrestBlazorComponentPart`), "not a route". True for a widget, but a **designed page** (§ 6a)
   is both a stored tree and a route-table entry; the designer doc must distinguish editing a
   template, a page and a widget instance.
5. **[shells-and-themes.md](shells-and-themes.md) › Theme compatibility** defines a theme as a
   compiled module with a manifest, a bucket tag and a `BaseTheme` chain. Standard 8 (theme as
   data) is open; whichever way it goes, the compatibility contract (`crest-blazor`, buckets,
   the two guards) must still hold for the compiled part of a theme.
6. **[admin-routes.md](admin-routes.md)** lists Design › Templates as "native list/editor" and
   Placements, Zones, Widgets as deferred stock UI. Those pages edit Liquid templates and zone
   names, both retired by this design; the inventory should mark them "replaced by the designer"
   once the Templates rework lands.
7. **[design-systems.md](design-systems.md)** keeps two open items (token taxonomy, inheritance
   versus compiled sets) that the style schema (standard 3) depends on: a style parameter binds a
   token by name, so the taxonomy must be fixed before the primitive schema is.
8. **`CrestBlazorComponentPart`** (`Crest.Server/Models`) stores `Dictionary<string,string>`
   parameters by design ("deliberately string-keyed, mirroring Template"). § 7 says parameters are
   validated against the registry schema; the part's comment and shape change with it.
9. **Vocabulary › Page** here said "a Blazor `@page` component is a page too, with the tree built
   in code"; § 6a now makes pages a distinct kind with three sources. The Vocabulary row is to be
   rewritten to point at § 6a.

### Decisions needed before the plan is concrete

Each item names the options and a recommendation. The rewrite of this document follows the
rulings.

- [x] **2. Primitive vocabulary.** Ruled (b): modules add primitives, through the registry, because
  a module's value types need custom-coded primitives to reach the UI. The base set
  (`Text`, `Heading`, `Markup`, `Image`, `Link`, `Button`, `Container`, `Stack`/`Grid`, `Input` per
  value type, `Zone`, `Repeater`, `Form`) ships in `Crest.Components.Primitives` with semantics and
  accessibility (heading level, landmark role, label) in the schema. Headless parity is per
  module: a headless client renders the base set plus the primitives of the modules it chooses to
  support, and the display tree names the owning module on every node so it can tell.
- [x] **4. Context and bindings.** Ruled (2026-10-09): **everything a template reads comes
  through the query system.** The context is `Route` (the page record's typed parameters, the
  one non-query input) plus named queries. `Item`, `User` and `Tenant` are built-in **system
  queries** (the item by its route parameter, the current principal, the site settings) served
  by an in-process source with no SQL behind it and memoised per request; they are display
  facts, never permissions. A slot binds `{ query, parameters, path }`; a parameter is itself a
  binding (to a route parameter, to another query's result, or to a component's own input, so a
  list's search box re-runs its query), which is how **filtering** works; build-time filters
  stay in the saved query. Permissions arrive by construction: the query system injects the
  caller's scope into every query ([queries.md](queries.md)) and the node filter (§ 10) drops
  what remains. A template declares the queries it uses (its inputs, § 5), so bindings validate
  against the builder's typed column cache and the designer's slot picker is the query picker.
  **Writes go through workflows:** a button or form binds an action to a workflow (decision 5),
  declared in the Liquid the same way, permissioned and audited by the workflow; a template
  exported to a tenant lacking that workflow has a button that does nothing, reported by
  validation, and touches nothing else. **Two systems, one direction** (ruling 2026-10-09):
  queries are the read system and own the connections (a write request through a connection is
  a query too, per queries.md); workflows are the action system and call queries, write queries
  included. Queries never call workflows, and a slot never binds a write query; a write is
  reachable from a workflow action only. Consequence for sequencing: the query contract (typed,
  paged, caller-scoped source, the system sources) moves ahead of the Templates rework in the
  plan; the SQL rewriter, the builder and the connectors follow.
- [x] **5. Actions.** Ruled (a), workflows only, by the scripting ruling (§ 12): run a workflow,
  navigate, submit the current item; everything else is a workflow.
- [x] **6. Zone keys.** Ruled (option 3, 2026-10-09): keys are registry entries with a schema.
  Crest ships the well-known keys (`Header`, `Navigation`, `Content`, `Toolbar`, `Status`,
  `Sidebar`, `Footer`); a module adds keys it owns under its own prefix (`{module}:{page}:{region}`,
  as page-regions.md does today). A key declares what may fill it (allowed component names,
  single or multiple), whether it is required on a layout. **Ordering is by priority**, the way
  module admin-menu contributions work (ruling 2026-10-09): a contribution ships a default
  priority, priorities are settable, and once contributions have loaded they appear in the zone's
  settings, where the tenant reorders them through the override system, on the zone (for every
  page) or on a page's zone. The registry lists every key, the designer's `Zone` node offers a
  picker, an unknown key is a validation error on save, and a layout missing a required key gets
  a warning. A module may fill a well-known key without declaring it; any other key only its
  owner declares.
  **Zones are components** and carry the full override model of § 5a (component id, node id,
  instance id, one parent per override). The setting a zone needs, **single or multiple
  instances per page**, is a base setting of *every* component (ruling 2026-10-09): a component
  declares whether more than one instance per page is allowed, because several instances of some
  components break their logic; the designer refuses a second placement where one is declared.
  **What landed (2026-10-13).** Zone keys are registry entries: `ZoneKeyDescriptor` (key,
  owning module, allowed components, single or multiple, required on a layout), the well-known
  keys in `ZoneKeys.WellKnown` (`Content` required), module keys declared with
  `[assembly: ZoneKey("prefix:*")]` (a trailing `*` declares a family under a prefix; a module
  cannot redeclare a well-known key). The page-region seam is folded in: the catalogs collect a
  library's `IPageRegionContributor`s as `ZoneContribution`s (key, component, default priority),
  the registry serves `ZoneKeys`, `TryGetZoneKey` and `Contributions(key)` on the server (feature
  filtered) and in the browser (rebuilt on a lazy load), `CrestPageRegion` renders from it, and the
  static `PageRegionRegistry` is gone. A layer placement into an unknown key, or of a component
  the key does not admit, is a save error; a layout without a required key and a contribution to
  an undeclared key are logged warnings; `GET api/crest/components/zone-keys?shell=` lists the
  keys; `DisplayTreeValidator` refuses a second instance of a component declared single.
  Crest.Parties declares `Parties.Pane:*` and `Parties.Detail:*`. Left: reordering a zone's
  contributions per zone or per page through the override system, and a `Zone` component node
  (zones as components with the § 5a model); both wait for the designer's zone settings.
- [x] **7. Template hierarchy.** Ruled (2026-10-09): the platform's alternates chain, as data,
  with qualifiers. A template binds a shape name plus optional **display type** and **theme**;
  resolution is most specific first, dropping one qualifier at a time, down to the module's
  shipped default, and below that to the **error pages** (404, 403, 500), which are the ultimate
  fallback of the chain (ruling 2026-10-09). Theme is the only placement qualifier: a shell and its theme are one thing for
  this purpose (the admin, site and member shells each have an active theme), so a `Content`
  fallback can differ on the admin theme and the member theme without a separate shell
  qualifier. Composites are named templates placed by name; a pattern is a node override with
  no further link. Versioning (draft, published, history) comes from § 5a and § 10, not a second
  mechanism. Recipe export carries a template with its overrides as one unit and references
  components by component id, so another tenant's map resolves them to its own node ids.
- [x] **8. Theme as data.** Ruled (2026-10-09): **both kinds exist.** Most shipped themes are
  compiled modules (manifest, bucket, `crest-blazor`, base-theme chain, code components) that
  ship their set as a recipe; a data theme is that set alone, created in a tenant or imported.
  A **public marketplace carries data themes only**, because a public marketplace for code is
  risky; plugins (code) have a separate marketplace, and a plugin that includes a theme still
  registers it through the same mechanism a data theme uses, so every theme imports and exports
  between systems the same way.
  **A theme is page structures plus branding.** The theme system (`Crest.Themes`) is to be
  centralized so that the two layers separate: the **structure** (page and layout templates,
  composites, zone settings) and the **design system** on top of it (branding: fonts, heading
  settings, corner radius, tokens; [design-systems.md](design-systems.md)). Then a theme can be
  swapped while the branding stays, or a single page imported and made to conform to the
  tenant's branding. This likely means the admin, site and member themes never change the way
  OrchardCore themes change, only the selections inside them, which needs the theme registry
  and selection system reworked without losing any shell injection mechanism (permissions,
  required types, contracts, [shells-and-themes.md](shells-and-themes.md)). **That rework is a
  separate plan**, after the display system; recorded there as an open item.
- [x] **9. Designed pages.** Ruled (option 1, 2026-10-09): data-defined route-table entries.
  A page record (route pattern with typed parameters from `Crest.Data.Types`, bucket, permissions,
  what it renders) is served by an `IRouteComponentTableProvider` that reads content, cached per
  tenant and invalidated on publish; the entry exists once the page is published and the draft
  is reachable only through preview. The same bucket gate, route authorization and cache apply
  as to a module `@page`, so an admin designed page is reachable only through the admin shell.
  **A page is its own kind, kept separate from components** (ruling 2026-10-09): it is not a
  registry entry, can never be the child of another component, and carries the fields only a
  page has (route and parameters, Autoroute and alias, bucket, title and metas, the permission
  sets). Its children are components placed directly under it, or composites.
  **Pages have no overrides** (ruling 2026-10-09): an override of a page would change routing,
  so a page record is edited directly, with drafts and publish (§ 10) but no node or instance
  override chain; the components under it keep the full § 5a model. The top of a data-defined
  page's tree is therefore the page record itself, rendered through the layout the template
  hierarchy (7) resolves for it.
  **What landed (2026-10-13).** The page record is the `DesignedPage` content type
  (`DesignedPagePart`: name, route pattern, bucket, title, meta description, anonymous access,
  Liquid source; drafts and publish, no overrides) in the Templates module, read through
  `DesignedPageStore` (the published set cached and re-read on the publish signal). Its tree is
  the shipped template `DesignedPage__{Name}` (`DesignedPageTemplates`), evaluated with the
  route's values as `Model.Route`. `IDataRouteComponentTableProvider` adds data-defined entries
  to the route table per request after the scanned pages; `DesignedPageRouteComponentTableProvider`
  registers the site bucket's pages on the site's content route component, and `SitePageBuilder`
  answers a matching path ahead of Autoroute, inside the site layout. `api/crest/pages` (list,
  get, save draft, publish, delete, preview a draft at a path) validates the name, the bucket,
  the route (pattern and uniqueness in the bucket) and the tree; the `DesignedPages` recipe step
  creates and publishes them. Left: admin and member pages are stored and validated but not
  routed, since neither shell has a component that renders a data-defined page (their client
  routers know only scanned pages); route parameters are strings until `Crest.Data.Types` gives
  them types; the designer's page editor.
- [x] **10. One format, and no generated pages.** Ruled (2026-10-09). The display tree is one
  typed node format. **The shape system stays as the backend**: drivers and placement keep
  building `IShape` trees, and a mapper converts them once (shape type → component, alternates resolved to the binding, properties
  checked against the schema, items and zones → zones, deterministic instance ids from item,
  part and field), and nothing downstream knows `IShape`.
  But **a content type without an assigned template does not get a page generated for it.**
  The alternates chain (7) ends at the error pages, not at a driver-built page. A standard
  default **`Content` Liquid template** will exist later, listing the fields the requesting
  user is permitted to see, and it is a **per-tenant setting** whether it runs at all, because a
  tenant may not want unassigned types to render. Drivers and placement remain the generator for
  what still needs one, the admin editor trees (§ 7) and the field list that default template
  binds to. **The API is on by default; pages are not** (ruling 2026-10-09): a content type's
  items are served by the content API (REST, GraphQL, § 8) under the data permission as soon as
  the type exists, while a page and a URL exist only when a template is assigned (or the default
  template is enabled). This is a deliberate departure from OrchardCore, where any item with an
  Autoroute part gets a URL and renders through the default `Content` shape.
  **What landed (2026-10-13).** Shipped templates are the chain's last level: modules register
  an `IShippedTemplateProvider` and `TemplateResolver` tries, per name, the tenant's templates and
  then the shipped ones. `DefaultContentTemplate` (`Crest.Server/Display`) is the default
  `Content` template for the Detail display type (the title, then the item's `Header`, `Meta`,
  `Content`, `Tags` and `Footer` zones as the drivers filled them and the node filter cut them),
  present only while `SiteDisplaySettings.RenderUnassignedContent` is on; `SitePageBuilder` asks
  the resolver alone, so the setting and a tenant template decide alike. The setting has no
  admin screen yet (recipe `settings` step only); the per-part read rule that narrows "fields
  the viewer may see" further is decision 13's, in the node filter.
- [x] **11. Edit mode is an overlay, not a wrapper.** Ruled (2026-10-09). The designer's
  canvas is the real renderer and the real page, rendered **unmodified**; the content never
  carries designer markup. The root document has two layers: the **content** layer, and a
  **designer** layer above it (`z-index` far above anything content can reach, `pointer-events`
  only on its own handles) that draws hover and selection boundaries, snap guides and drop
  markers, so no content node is affected by being edited. Mechanics:
  - **Locating a node.** The renderer marks every node's root in the DOM with its instance id, as
    a `data-` attribute when the primitive forwards attributes to its root (the convention for
    Crest-shipped primitives) and otherwise as a comment marker the renderer emits before the
    node, the way Blazor marks its own components; the designer resolves markers to elements.
  - **Measuring.** Layout is the browser's, not WebAssembly's, so rectangles come from JS
    (`getBoundingClientRect`, kept fresh by `ResizeObserver` and `MutationObserver`) and are
    batched to the designer; the overlay is drawn from those rectangles. Only the hovered and
    the selected nodes are drawn by default; a "show all boundaries" mode draws every node,
    rarely.
  - **Snapping.** The overlay snaps a drag to the edges and centres of the measured rectangles
    of sibling nodes and zone bounds, draws the guides, and on drop turns the target into a
    template edit (the tag moves in the text, § 11), then the page re-evaluates.
  - **Reading and writing a node.** Reading properties never touches the DOM: the property pane
    reads the evaluated tree and the registry schema. Only inline text editing inserts a
    temporary editing surface over that one node, removed on blur.
- [x] **12. Caching the rendered tree.** Ruled as written (2026-10-09). Two trees exist: the
  **unfiltered** tree holds everything the template binds and never leaves the server; the
  **filtered** tree (after the permission filter, slots resolved) holds only what the requesting
  user may see and is as sensitive as the page they received. A per-user server cache scales by
  users × pages × cultures and is rejected on memory. Candidate shape:
  - **Client-held filtered trees**, validated by a composite version (template version, overrides
    version, item version, culture, design-system version, permission version: a hash of the
    user's roles plus a counter bumped on any policy change) sent as an ETag; the server compares
    versions without evaluating and answers unchanged or sends a fresh tree. Session storage,
    cleared on sign-out.
  - **A small server cache of unfiltered trees** per page, item version and culture, bounded by
    content size and not by users, filtered per request; it serves cold loads (the first request
    is prerendered on the server regardless of any client cache) and misses.
  - **Anonymous and role-shared pages** share one filtered tree per permission set, the one place
    a shared cache or CDN applies.
  - Drafts and previews are never cached.
  **What landed (2026-10-13).** The composite version is `DisplayTreeVersions`
  (`Crest.DisplayManagement/Components`): a stamp taken when the tenant starts, a content counter
  bumped on every publish, unpublish, update, import and removal (`DisplayTreeContentHandler`),
  the culture, the `IDisplayTreeVersionPart`s modules register (Templates: the published set and
  the overrides document; Layers: the layers document; Crest.Server: the site settings) and the
  caller's scope (the path, `CallerContext.ScopeSignature`, which carries the user, the roles and
  the permission version). `api/crest/site/page` and the published-item path of
  `api/crest/content-items/{id}/display` send it as the ETag and answer 304 on a match before
  building anything. The site shell's `ContentPage` keeps pages in session storage once
  interactive, revalidates with If-None-Match, and drops them when the scope changes (sign-out).
  Anonymous site pages, one permission set, share a filtered tree through `DisplayTreeCache` (a
  tenant singleton bounded by size). Not built: the cache of **unfiltered** trees per page for
  signed-in viewers. The tree builder still applies per-viewer decisions (component
  permissions, menus filtered by their driver, the drivers' own checks), so a built tree is not
  viewer-independent; that cache needs those decisions moved into the node filter first.
- [x] **13. Publish.** Ruled (§ 10, set 5): publish is its own permission, per page and per
  component, since components have drafts of their own and a preview needs them. The tenant
  setting in § 5a defaults to "edit for preview, a manager publishes". "Data" at run time stays the only data permission, and the per-part
  read rule from [permissions.md](permissions.md) is one of its implementations through the node
  filter (every node carries what admitted it; the coordinator asks the filter before adding a
  part or field shape).
- [x] **14. Design-system token taxonomy.** Ruled (2026-10-09), recorded in
  [design-systems.md](design-systems.md) › Token taxonomy: a reference palette feeding one flat
  semantic namespace (no component-token layer; primitives declare which semantic tokens their
  style parameters default to), fixed categories, numeric and t-shirt scales, tokens extensible
  by name through the registry with per-tenant and per-user overrides, presets with per-audience
  override permissions set at tenant level, modules never changing another module's defaults
  unless a build-time setting allows it, inheritance compiled at publish. The design system is
  the visual language and branding; the theme is structure.

### After the rulings

Rewrite this document around the fluid model (zones as nodes, generator as fallback, pages as
§ 6a, value types from `Crest.Data.Types`), fold page-regions.md into it, rewrite
blazordesigner.md's Phase 1 to consume the registry and the tree, record the theme ruling in
shells-and-themes.md, and then turn the Rework plan into tracked tasks.

## Out of scope here

The designer UI (canvas, palette, property pane, drag and drop, undo) is
[blazordesigner.md](blazordesigner.md); it edits the `Template.Tree` this document defines and
reads the registry's schema. The expression engine choice is [architecture.md](architecture.md) ›
Direction. Shell registry and per-shell hosts are [shells-and-themes.md](shells-and-themes.md).
