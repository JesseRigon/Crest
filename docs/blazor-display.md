# The display system for Blazor

This is the backend the Blazor designer ([blazordesigner.md](blazordesigner.md)) sits on: how
templates, layouts, pages and content items become Blazor components, as data a tenant can
edit, rendered the same way on the server, in WebAssembly and headless. It repurposes the
platform's display system (shapes, drivers, placement, zones, shape tables) rather than
replacing it, and retires the parts of it that are bound to Razor and Liquid HTML views.
Implementation status is in the [Rework plan](#rework-plan) and [backlog.md](backlog.md) ›
Display.

## The thesis

**The platform already builds a type-neutral component tree. Only the last step, turning it into
HTML, is Razor's.** A display driver returns a `ShapeResult`; placement puts it in a zone at a
position with alternates; the display manager assembles an `IShape` tree (type, display type,
properties, items, zones, alternates, wrappers). None of that mentions HTML. The HTML path then
resolves each shape through `DefaultHtmlDisplay` and the theme's shape table to a `.cshtml`, a
`.liquid` file or a DB-stored Liquid template.

So the design keeps the build side as it is, replaces the binding with a **component binding**,
and replaces the HTML renderer with a **tree renderer** that walks the shape tree and emits Blazor
components. Everything a tenant edits (templates, layouts, placement, pages) is data that feeds
that build side, and the same tree is the headless contract.

Not part of it: Razor runtime compilation (tenant C# is code execution; Roslyn in WASM is
unusable), and a second, Blazor-only display system beside the platform's.

## Vocabulary

| Term | Meaning here |
| --- | --- |
| **Shape** | The platform's `IShape`: a node in the display tree. Keeps its name. |
| **Display tree** | The serializable form of a shape tree: `{ type, displayType, alternates, properties, zones: { name: [nodes] }, items: [nodes], metadata }`. What the renderer consumes and what the headless API returns. Its node is `DisplayNode` in `Grove.Display.Management.Abstractions`; `GroveModel` is only the content-item wrapper. |
| **Component** | Anything in the component registry: a name, a parameter schema, zones, the shells it may appear in. One concept with facets, not kinds: a component may be a **primitive** (text, container, input, button, link), a **composite** (a header built from primitives, as code or as a saved template), an **editor** (the input component for a value type), and/or a **host** (it exposes open zones others fill). A **page is not a component**: pages are their own kind, § 6a. |
| **Binding** | The rule that maps a shape (type + alternates + display type, per theme) to a component, or to a template. Replaces `ShapeBinding.BindingAsync`. |
| **Template** | A tenant-editable component tree bound to a shape name, stored as content, authored in Liquid (§ 5). A template *is* a binding whose target is a tree instead of a compiled component. |
| **Layout** | The template bound to the `Layout` shape for a shell and theme: the page frame, placing the zones. |
| **Zone** | A named list of child nodes; a `RenderFragment` parameter on the component that renders it (the word is "zone", never "slot"). A zone is **closed** (only the template that declares it fills it) or **open**: a registered key (`Navigation`, `Content`, `Footer`, a module's `prefix:*` keys) that other modules fill without owning the component, through placement, code contributions or layer rules (decision 6). A `Zone` node may be placed anywhere, any number of times. Page regions ([page-regions.md](page-regions.md)) are zone contributions. |
| **Slot** | A data binding on a component: a parameter whose value comes from data (the item, a query, the context) rather than a literal. A picker's `source` is a slot; a heading bound to `TitlePart.Title` is a slot. Slots are what overrides may rebind (§ 5a). |
| **Page** | The unit that owns a URL: its route, shell bucket, route authorization and document concerns. Three kinds (module page, content page, designed page) share one route table; § 6a. Distinct from a component and from a template. |
| **Editor kind** | Not an enum: the editor components in the registry, each declaring the value types (CLR types, content field types) it edits. The designer's property pane, the content field editors and a template's inputs all draw from this one set; these are what blazordesigner.md calls custom data types. |
| **Expression** | A Liquid expression in a node property, evaluated on the server at build time by the one expression engine (§ 12). |
| **Liquid template** | The one authoring form of a template: Fluid (MIT) with the platform's tags emitting tree nodes, evaluated on the server into the display tree. The designer edits this text, Gutenberg-style. Data, never code; § 5 and § 11. |

## What stays, what goes

**Stays, unchanged** (the build side; all type-neutral):

- `IDisplayDriver<T>`, `DisplayDriver<T, TDisplayModel>`, `IDisplayManager<T>`, `ShapeResult`
  (`Location`, `Differentiator`, `OnGroup`, `RenderWhen`), `BuildDisplayContext`, display types (`Detail`, `Summary`, `SummaryAdmin`, `DetailAdmin`), editor
  groups.
- `IContentItemDisplayManager`, `ContentItemDisplayCoordinator`, the content, part and field
  display drivers, the alternates factories, `IContentDisplayHandler`.
- `IShape`, `ShapeMetadata`, `IShapeFactory`, alternates, wrappers, positions.
- `PlacementInfo`, `placement.json`, `IPlacementInfoResolver`, the Placements module's data, and the
  feature order that lets a theme's placement override a module's.
- `IZoneHolding`, `ILayoutAccessor`, zones as named, positioned lists.
- `ShapeTable`, `IShapeTableManager` (per theme, per tenant, invalidated on feature and theme
  change), `IShapeTableProvider`, `ShapeTableBuilder.Describe(...)` with `OnDisplaying`, `Placement`
  and the events.
- `IThemeManager`, `IThemeSelector`, `BaseTheme` layering.
- `IResourceManager`'s data (`GetRequiredResources`, links, metas, `ResourceDefinition`).
- The Layers module's rule evaluation (`ILayerService`).

**Replaced:**

| Platform piece | Replaced by |
| --- | --- |
| `ShapeBinding.BindingAsync : Func<DisplayContext, Task<IHtmlContent>>`, `ShapeAlterationBuilder.BoundAs(...)` | A component binding: `{ ComponentName }` or `{ Template }`, resolved with the same alternates-last-to-first order, same shape table, same theme scoping. |
| `IHtmlDisplay` / `DefaultHtmlDisplay`, `IDisplayHelper.ShapeExecuteAsync` | The **tree renderer**: shape tree → display tree → `RenderTreeBuilder` calls. One implementation, in a library both the server and the WASM client reference. |
| `IShapeTemplateViewEngine`, `RazorShapeTemplateViewEngine`, the Liquid shape engines, `ShapeTemplateBindingStrategy` (file harvesting of `.cshtml`/`.liquid`) | Component discovery through the registry; a module binds a shape to a component by attribute or by `Describe(...)`, never by a file name convention. |
| `TemplatesShapeBindingResolver` (DB Liquid → shape) | The template binding, returning a component tree from content. |
| `LayerFilter` (an MVC `IAsyncResultFilter`) | A layout build step that runs the same rule evaluation and adds layer placements to the layout's zones. |
| `IResourceManager.Render*(TextWriter)`, the resource tag helpers | A head/foot renderer from the resource registry data, emitted as `HeadContent` / markup nodes in the tree. |
| The site theme's `Layout.liquid`, `Content-Page.liquid`, `Widget-*.liquid`, `Menu*.liquid` | Layout, content and menu components (and templates for the tenant-editable ones). |
| The admin client's `DisplayManager`/`Shape`/`ShapeView` and `Grove.Display.Components/Shapes/Shape.cs` | Retired as a display system; the shell state left in `DisplayManager` is `AdminShellState`. |
| `[BlazorComponent(name)]`, the global component registry, the workbench registry, the route table's component scan | **One component registry**, § 1. |
| `BlazorComponentShapeBindingResolver` rendering a component to static HTML so Liquid can embed it | Gone with the HTML views; `HtmlRenderer` stays for headless HTML (email, static pages). |

## The pieces

### 1. The component registry

One registry, per shell. An entry is:

```text
ComponentDescriptor
  Name            the shape type it binds by default ("Content", "Content__Page", "Menu",
                  "Zone", "Heading", ...). Alternates are names too.
  Type            the Blazor component type (statically referenced, trim-safe)
  Parameters      the schema: name, CLR type, editor kind (text, number, bool, enum, picker,
                  color, token, markup, media), required, default, group
  Outputs         values the component publishes (§ 4a)
  Zones           the RenderFragment parameters: name, allowed component names, min/max
  Facets          Host (declares open zones), Editor (edits which value types); primitive
                  versus composite is only whether Type is code or a template
  Shells          admin | site | member (from the assembly's [GroveShell])
  Feature         the feature it belongs to; absent when the feature is disabled
  Permission      optional; the node is dropped from the tree for users without it
  InstancesPerPage single | multiple: several instances of some components break their logic
  Category, DisplayName, Icon   for the designer's palette
```

- **Discovery:** `[BlazorComponent]` on the component and a **source generator** that emits the
  descriptors and a `name → Type` map at build time (component prefixes stay `Grove`, service
  names are neutral). Reflection over `[Parameter]` properties is the dev-time fallback; in WASM
  the generated map is what keeps trimming from stripping parameters.
- **Scope:** the registry is built per tenant, shell and theme like the shape table and the
  route table (same cache, same invalidation). The catalogs also describe an assembly's module
  pages, which the route table reads; pages are never registry entries (§ 6a).
- **The schema is shared** between the server (validation, defaults), the client renderer and the
  designer's property pane. One source.
- **Schema vocabulary:** modelled on Plasmic's MIT `registerComponent` metadata. Parameter types:
  `string`, `number`, `boolean`, `choice` (static or computed options), `zone` (Plasmic's `slot`:
  `allowedComponents`, `defaultValue`), `object`, `array`, `dataSource` (a binding to a query or
  the context), `eventHandler` (an action: a workflow). Entries may declare `templates` (presets
  the palette offers), `defaultStyles`, `isAttachment` (a wrapper that takes one child),
  `isRepeatable`, and per-parameter `hidden`/`advanced`/`readOnly` rules computed from other
  parameters. Value types from `Grove.Data.Types` plug in as parameter types beside the base ones.

Implementation:

- **Contracts** (`Grove.Display.Management.Abstractions/Components`, browser-safe):
  `[BlazorComponent]` with palette and rule metadata, `[ComponentParameter]` (with `Choices`,
  static options that make a string parameter a `Choice`), `[ComponentZone]`,
  `[assembly: ComponentCatalog]`, `ComponentDescriptor`, `IComponentCatalog`,
  `IComponentRegistry`/`ComponentRegistry`, `ComponentSchema` (CLR type → kind and editor:
  string/char/Guid/Uri → text, numbers → number, bool, enum → `Choice` with the names, dates and
  times, `EventCallback` → `EventHandler`/action, collections → `Array`,
  `RenderFragment`/`RenderFragment<T>` → a zone, anything else `Object`),
  `ReflectionComponentCatalog` (the dev fallback, same output as the generator),
  `ComponentCatalogs.ForAssembly` (the generated catalog if the assembly names one, else
  reflection; read once per assembly) and `ComponentParameters` (schema validation and
  conversion, sharing its JSON options with `DisplayTreeRenderer`, so what validates renders;
  enums by name; a lenient converter binds stored `"true"` to `bool`).
- **Source generator** (`Grove.SourceGenerators/ComponentCatalogGenerator.cs`): per assembly, an
  internal `IComponentCatalog` with full descriptors (inherited `[Parameter]`s base first,
  capture-unmatched and cascading ones left out, `[EditorRequired]` → required), the shells from
  `[assembly: GroveShell]` (every shell without one), `Module` = the assembly name, and
  `[assembly: ComponentCatalog(typeof(...))]`. It reads C# only: Razor's own generator output is
  invisible to other generators, so a component is marked and its parameters declared in the
  code-behind. OCSG003/OCSG004 warn about a marker or `[Parameter]`s left in `.razor` markup;
  OCSG005 about a marked component it cannot catalog (generic, abstract, inaccessible).
- **Generator wiring:** `buildTransitive/Grove.Display.Management.Abstractions.targets`, shipped in
  the abstractions package with the generator in `buildTransitive/analyzers/`, so every package
  consumer gets it, directly or through `Grove.Display.Components`. It applies to Razor projects named
  `*.BlazorWasm` or `*.Client`, or any project setting `<ComponentCatalog>true</ComponentCatalog>`
  (`Grove.Display.Components`, `Grove.Display.Icons`), and enables the interceptor namespace the same assembly's
  shape generators need. In this repo `Directory.Build.targets` imports the same file for the
  non-platform projects with the generator as a project reference.
- **Server registry** (`Grove.Display.Management/Components`): `IComponentRegistryManager` builds
  one registry per shell and theme from the enabled features' assemblies plus
  `ComponentRegistryOptions` (Grove.Infrastructure.Server adds `Grove.Display.Components`, `Grove.Display.Icons`, the theme
  clients and the module client libraries, each library limited to its shell bucket). A
  component's feature is its type's (`ShellBlueprint.Dependencies`), else the module its library
  is named after (`X.BlazorWasm` → `X`); catalogs apply in feature order, a theme's only for that
  theme and its base themes. Cached in a tenant singleton like the shape table. The scoped
  `IComponentRegistry` is the request's shell (`IShellKindAccessor`).
- **Browser registry** (`Grove.Infrastructure.Shell/ShellComponentRegistry.cs`): the document's shell over the
  shared libraries, its theme client, and the shell's eager and lazily loaded module libraries
  (`LazyModules`), rebuilt when a lazy module loads; registered at WASM boot
  (`ClientWebAssemblyHost`).
- **Editor facet:** `[BlazorComponent(Edits = [...])]`, `IComponentRegistry.TryGetEditor`, `*` for
  the JSON fallback; `GET api/grove/components?shell=` lists components, composites, variants and
  editors.
- **Registered library components:** `GroveHeading`, `GroveQuote`, `Label`, `Badge`, `Alert`,
  `Row`, `Column`, `Panel`, `Fieldset`, `Image`, `Button`, `Link`, `Card`, `Stack`, `Icon`,
  `Markdown` (a wrapper rendering `GroveMarkdown` with HTML pass-through off; `GroveMarkdown`
  itself is not registered, since its tag and attribute allow-lists would be a template's to
  set), the layout hosts `AppLayout`, `AppHeader`, `AppBody`, `AppFooter`, `AppSidebar` (named
  apart from the themes' `Layout` and the zone keys), and the value editors `ColorEditor`
  (`color`) and `MediaPicker` (`media`). The form editors (`GroveTextBox`, `GroveTextArea`,
  `GroveNumeric`, `GroveCheckBox`, `GroveSwitch`, `GroveDatePicker`, `GroveDropDown`,
  `GroveHtmlEditor`) are not registered: the form primitives ([widgets.md](widgets.md)) and the
  field editors cover them. Generic components (`GroveDataGrid<TItem>`, charts) cannot be named
  in a tree; they need a closed wrapper.

### 2. Bindings: shape → component or template

The shape table keeps its per-theme descriptor list. Each binding is one of:

- **Code binding:** `builder.Describe("Content__BlogPost").BindTo<BlogPost>()` or the attribute
  on the component. Modules ship these.
- **Template binding:** the Templates module's resolver answers "is there a template for shape
  `X` with these alternates in this theme?" and returns a tree (decision 7).
- **Resolution order:** alternates last to first, then the type's segments; template bindings are
  asked before code bindings. A tenant template therefore overrides a module's component for that
  shape, and the theme's binding overrides the module's.
- A display variant (`X_Display__Mode`, `X_Summary`, `X_SummaryAdmin`, `X_Detail`,
  `X_DetailAdmin`) falls back to the shape's own component; an editor shape never does.

### 3. The tree renderer

A recursive walker over the display tree, in a library referenced by server and client:

- For each node: look up the component by the resolved binding; `OpenComponent(type)`;
  `AddComponentParameter` for each schema parameter present in the node's properties (converted
  by the schema's CLR type); each zone becomes a `RenderFragment` that renders the zone's nodes;
  `SetKey(node.Id)` for stable diffing; constant sequence numbers per call site, `OpenRegion` per
  node. A template node renders the template's tree with the node's properties as its inputs.
- **Wrappers** are components with one zone that take the wrapped node as their child.
- **Markup leaves** (a rich-text field, Liquid free text) render as sanitized `MarkupString`.
  Liquid is evaluated on the server at build time into a leaf; the client never runs it.
- **Composition is one tree, rendered once.** A sub-component is never rendered and fed into its
  parent; the whole tree is assembled at run time and rendered in one pass.
- **The same renderer** runs under static SSR, inside the interactive root, and under
  `HtmlRenderer` for headless HTML. Nothing in it touches `HttpContext`, JS or `IHtmlContent`.

The server side is `IDisplayTreeBuilder` / `DisplayTreeBuilder` (`Grove.Display.Management/
Components`), the shape-to-tree mapper. It runs the shape descriptor's and the shape's own
displaying and processing phases, then binds through `IComponentRegistry.TryResolve` in
`DefaultHtmlDisplay`'s order; checks properties against the schema (unknown or wrong-typed ones
dropped and diagnosed); maps named zones and a shape's items (`ChildContent`) with the zone's
allowed list and min/max; maps wrappers to wrapper components; gives deterministic instance ids
(item, part, field, shape type, position); drops a component whose permission the caller lacks
(`IAccessDecision`); and runs under `DisplayTreeOptions` (time, nodes, depth, size, template
steps and depth). An overrun becomes a `DisplayError` node and is never retried.

A shape with no component becomes a `DisplayError` node naming its shape type (diagnostic
`no-component`), counted for the coverage report (`GET api/grove/components/coverage`,
[MVC/shape-engine.md](MVC/shape-engine.md) › Component coverage). `ShapeBinding.ComponentName`
(`ShapeBinding.ForComponent`) binds a shape to a component in the shape table.

### 4. One render mode

**Every shell renders `InteractiveAuto` at its root, prerendered.** The page arrives as
server-rendered HTML, so it reads without JavaScript and never waits on the runtime, then
hydrates (Server circuit first, WebAssembly once cached). One mode everywhere: no per-node or
per-shell render modes to encode, no island boundaries, no casing per shell.

What that implies for the renderer:

- The display tree **crosses the boundary once, at the root**: the shell's root component takes
  the tree the server built (`[PersistentState]`), so the client renders the same tree without a
  second fetch. The tree is JSON; `RenderFragment`s are built on the client from the tree's
  zones, never serialized.
- The renderer is the same code on both sides; the component registry needs no `RenderMode`
  facet.
- Per-node islands (`AddComponentRenderMode`) are not used: the one interactive root is what lets
  components on a page share state (§ 4a), which separate islands could not.
- Data a component needs beyond the tree comes through the API, never a server service.

### 4a. Interaction: outputs, page state, client actions

Components declared in a Liquid template talk to each other through the tree, never through
code in the template. Liquid only writes the wiring down; the renderer connects it in the
browser.

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
- **Comparisons.** A binding may be a predicate: `visible: nodes.country.value == "US"`. The left
  side is a binding source (a node output, a page variable, the event); the operator is one of a
  fixed list (`==`, `!=`, `empty`, `not empty`, `contains`, `starts_with`, `ends_with`, `>`, `<`);
  the right side is a literal or another binding source; `and` / `or` join comparisons, `not`
  negates. Nothing else: no calls, no filters, no arbitrary expressions. Any parameter may bind
  this way.
- **Event payloads.** An event carries a payload (the selected row, the right-clicked row, the
  submitted values). Actions bound to the event may read it as `event` (`event.item.id`).
- **Client actions** join the server actions of decision 5 (run a workflow, navigate, submit):
  - **set** a page variable (from a literal, an output or the event payload);
  - **open**, **close** and **toggle** an overlay node (a modal, a drawer, a context menu, a
    popover) declared in the tree, positioned at the event when it has a position (a right-click);
  - an action is a list, run in order, so "set the row, open the menu" is one handler.
  Client actions only change what the viewer sees; anything that reads or writes data is a
  query or a workflow on the server, checked there.
- **Context menus** are overlay nodes whose items are actions; an item's workflow takes its
  parameters from the event payload or page state. Items the viewer may not run are dropped
  by the node filter (§ 10) when the tree is built.
- **Validation:** a binding to an unknown node or output, or of the wrong type, is a save error;
  a cycle of bindings (A's parameter from B's output from A's) is refused.

Implementation: contracts in `Grove.Display.Management.Abstractions/Components` (`[ComponentOutput]`,
`IsOverlay`, `ParameterKind.Output`, `DisplayNode.Bindings`/`Slots`/`Events`, `DisplayBinding`
with `Operator`, `Operand`, `Combinator` and `Terms`, `DisplayPredicates` (the fixed lists and
evaluation), `DisplaySlot`, `DisplayAction`, `DisplayState`, `DisplayActionRunner`,
`DisplayOverlay`, `IDisplayActionDispatcher`, `IDisplayDataSource`, `DisplayTreeValidator`). The
renderer owns a tree's state (or the page's, passed as `State`), feeds bound parameters and
slots, records outputs, runs event actions and renders overlays only while open; `DisplayState.Read`
evaluates a predicate whenever the tree re-renders on a change (text that reads as a number
compares as one against a number). On the server: `DisplayTreeFilter` with `IDisplayActionFilter`
(the workflows module answers for workflow actions), `POST api/grove/workflows/actions/{id}`, and
the query module's `IDisplayDataSource`. The shells' `HttpDisplayActionDispatcher` runs actions
and bound slots over the API (in a circuit too). Primitives: `ItemList` (selection output,
positioned context-menu event), `ContextMenu` and `Dialog` (overlays), `MenuAction`,
`PropertyList`, `Container`, `Fragment`.

### 5. Templates

A template is content (`Template` content type, `TemplatePart`, in the Templates module):

```text
Template
  Name          the shape name it binds ("Content__Page", "Layout", "Menu", "Header")
  Theme         optional: the theme (and so the shell) it applies to; absent = every theme
  DisplayType   optional (Detail, Summary, ...)
  Source        the Liquid text
  Inputs        for a composite: typed parameters with defaults and choices
  Outputs       for a composite: each one an inner node's output
  Version       content versioning: draft, published, history
```

- **One authoring form: Liquid.** A template is a Fluid template whose platform tags emit nodes
  into the tree rather than HTML; free text between tags becomes sanitized `Markup` leaves. It is
  evaluated on the server into the display tree, validated against the registry, and the client
  never runs Liquid. The display tree is the runtime and wire format only, never authored by
  hand. The designer edits the Liquid text (§ 11).
- **Tags** (`TemplateTreeParser`; tag arguments take plain expressions only, no filters or
  function calls):
  - `{% component "Name", id: "x", text: Model.Title %}`: a leaf; arguments are parameters, snake
    case for Pascal, checked against the schema.
  - `{% compose "Name" %}…{% endcompose %}` with children, `{% zone "Header" %}…{% endzone %}`
    inside a compose, `{% children "Content" %}` (the bound shape's own zone, filled by the tree
    builder in the same build), `{% shape "Menu", alias: "alias:main-menu" %}` (a platform shape
    built by its shape table and mapped in).
  - § 4a: `nodes.{id}.{output}` and `page.{variable}` are bindings, `event` inside
    `{% on "Event" %}…{% endon %}`; actions `set_variable`, `open`, `close`, `toggle`,
    `run_workflow "id", into: "var", arg: value`, `navigate`, `submit`; slots
    `{% slot "parameter", "Query", param: value, path: "items" %}` inside a compose. A Liquid
    logical expression as a tag argument is read as a predicate (`TemplateTreePredicates`)
    without evaluating it: `==`, `!=`, `contains`, `startswith`, `endswith`, `>`, `<`,
    `== empty` / `!= empty`, `and`, `or`, a leading `not` (`>=` and `<=` are stored as `not <` and
    `not >`). A comparison with no binding in it is evaluated by Liquid.
- **Ids** in a template are scoped under the instance id (`{instance}.{id}`).
- Node properties may be **literals**, **bindings** to the shape's properties
  (`{{ Model.ContentItem.DisplayText }}`, a field value, a picker's resolved label) or
  **expressions** (Liquid, the one engine).
- The schema of every component is known, so a template is validated on save and on publish
  (`TemplateValidator`): unknown component, unknown parameter, wrong type, a zone's allowed
  children, each `run_workflow` against the workflow's declared inputs.
- **Composites.** A template need not bind a driver-produced shape. A template named `Header`
  with no driver behind it is a composite: a reusable node any other template places by name.
  A composite template (`TemplatePart.IsComposite`) declares its inputs (name, type
  `string`/`number`/`boolean`/`choice`/`object`/`array`, required, default, choices), its outputs
  (name → an inner node's output) and its palette entry (display name, category, icon); it is
  validated like a coded component (save validation samples the inputs and checks each output
  names a real inner output), listed with them in the registry schema (`source: "template"`), and
  placed by name anywhere a component is. Placing one checks the arguments (unknown dropped,
  converted, defaults filled, required reported) and wraps it in a node whose `OutputAliases`
  re-publish the outputs under the instance id (`DisplayState.RegisterAlias`). A composite is
  parsed and evaluated on its own with only its arguments as `Model`, never concatenated into
  its parent's source; cycles and depth are capped; every evaluation shares the page's deadline
  and node budget and has a step budget (`DisplayTreeOptions.MaxTemplateSteps`,
  `MaxTemplateDepth`). Composites in code and as templates resolve through the same binding, so a
  tenant can replace a module's header component with a template of its own.
- **Shipped templates** are the last level of the chain: modules register an
  `IShippedTemplateProvider`, and `TemplateResolver` tries, per name, the tenant's templates and
  then the shipped ones (`DefaultContentTemplate`, `AccountEmailTemplates`).
- **Rendering to HTML:** `ITemplateHtmlRenderer` evaluates the template chain and renders it with
  `IDisplayHtmlRenderer`; email bodies (`UserConfirmEmail`, `UserLostPassword`) are templates
  this way.
- Admin templates and site templates are separate sets: an admin template is one whose theme is
  the admin theme, under `ManageAdminTemplates`; site templates are under `ManageTemplates`.
- `api/grove/templates` (list, get, save draft, publish, delete, validate, preview) and the admin
  Templates page (edit, validate, publish, preview a draft for a content item). The published set
  is cached and re-read on publish.
- HTML Liquid templates from the old Templates module are not migrated; dev tenants reset.

### 5a. Overrides are a basic tenet

A tenant never edits the base component or template everyone on the system shares; every edit
is an **override** (a fork) saved over the default and keyed to what it overrides.

- **What may be overridden:** a node's literal settings (visual settings: font, background,
  spacing, variant) and its **slots** (a picker rebinds from one data target to another, as
  long as the picker's declared value types accept the target). The override stores only the
  changed values, over the component's or template's defaults.
- **What may not:** values derived from data. A content-driven page title, an item's price, a
  query result are not editable in the designer; only their visual settings are. Data belongs to
  data.
- **Three ids and a map.** A **component id** is cross-tenant: the component a module declares
  at build time, editable by the default tenant only. Each tenant gets its own copy of the
  declared set, so every component has a per-tenant **node id** (the definition as that tenant
  sees and edits it); the map from component id to node id is per tenant. An **instance id** is
  one specific placement. None of these is scoped to a page: an instance can be referenced from
  any page.
- **One parent per override.** An override binds to exactly one parent: a node id **or** an
  instance id, never both. Binding to a node id makes a new node definition (a fork with its own
  node id); binding to an instance id makes a new instance derived from that instance. An
  override stores only the values it sets; every other value is inherited through its parent
  chain.
- **Resolution order**, from base to most specific: base node → node overrides → the instance
  of that node → instance overrides. A node override has only node ancestors; an instance
  override has an instance parent and then that instance's node chain.
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
  (the default) the user may edit it for preview but cannot publish it even with publish on the
  page they are working on, and a manager publishes on their behalf.

Implementation: `ComponentOverridesDocument` holds **variants** (node overrides: a name, a base
component, composite or variant, literal values; resolved down the chain base first, cycles
refused; placed by name, the tag's own arguments winning; listed as components) under
`ManageSharedComponents`, and **instance overrides** applied by the tree builder to every tree
(`IDisplayTreeOverrides`), never over a binding, a slot or an event.
`api/grove/templates/overrides` and a section on the admin Templates page edit them. Not built:
per-tenant node ids for declared components (variants are the node layer today), component
drafts, the setting for instances on inaccessible pages, and the designer UI.

### 6. Layouts, zones, pages

- **Layout** is the template bound to `Layout` for a shell and theme. It places `Zone` nodes by
  key; there is no fixed zone list, and a key may appear anywhere and any number of times. The
  keys are registry entries (decision 6), so Layers rules, the Placements editor and the designer
  target keys, not a layout's markup. `ILayoutAccessor` is how the layout shape is reached while a
  page is built.
- **Page content** is a content item (any type with `AutoroutePart`, or a type a module routes)
  built by `IContentItemDisplayManager` into the `Content` zone; layer placements into their
  zones; menus as `Menu` shapes.
- **Routing a content page:** one content route component in the site shell (`ContentPage`,
  `@page "/"` only; the shell middleware rewrites every site path no page claims to it and hands
  it the real path, [shells-and-themes.md › Shell dispatch](shells-and-themes.md#shell-dispatch))
  asks `api/grove/site/page?path=`, which `SitePageBuilder` (`Grove.Infrastructure.Server/Display`) answers: a
  designed page matching the path first, else the path through Autoroute (the home route for
  `/`, `Contents/ContentItems/{id}` for an item without a route), `ViewContent` for the caller,
  decision 10, then the `Layout` shape filled (the item into `Content`, the `alias:main-menu`
  menu into `Navigation`, the `ILayoutTreeContributor`s) and mapped and filtered as one tree. A
  miss is the layout with `NotFound` (404), a refusal with `Forbidden` (403). The page's query is
  forwarded (and part of the ETag), so list pages page. Module `@page` components work beside it
  and may build a tree in code (`IDisplayManager<T>`) or render directly.
- **The site shell** (`Grove.Themes.Site/wasm`): `ContentPage` (the real path from
  `IShellRequestPath` while prerendering and from the navigation manager once interactive; the
  page as `[PersistentState]`; the status set while prerendering through
  `IShellResponseStatus`), and the components `Layout` (zones `Navigation`, `Header`, `Messages`,
  `Content`, `Sidebar`, `Footer`), `Content` (zones `Header`, `Meta`, `Content`, `Tags`,
  `Actions`, `Footer`), `NotFound`, `Forbidden`; `GroveSite.css` on the tokens. `App.razor`'s
  site branch loads the design system (tokens, component styles, icons, scripts). The page's
  status is applied as the response starts and the status code pages are off for it, since it
  renders its own error page.
- **Two kinds of contribution, kept distinct.** Filling an *open zone* puts a component into a
  region (a layer placement into `Footer`, a module's panel into a page region). Contributing to
  a *model* puts an item into data a component renders (an admin menu item through the page
  registry, a site menu item through `INavigationProvider`, which the `Menu` component then
  renders). The admin menu and the site navigation are both: a model built by sources, rendered by a `Menu` component, placed in an
  open zone of the layout. `Menu`/`MenuItem` (`Grove.Display.Components/Interaction/LinkMenu.cs`) render
  the menu module's items, resolved in the processing phase.
- **Layer rules.** A layer rule (all blog posts, signed-in users) places a component instance
  into a zone key as a conditional fill applied while the tree is built: `LayerPlacement` (a
  component, its parameter values, a zone key, a position), added by `LayerTreeContributor`;
  `api/grove/layers` and the admin Layers page edit them. Widgets are not part of the system;
  [widgets.md](widgets.md) maps what each one did to its replacement.

### 6a. Pages stay a distinct thing

A page is **not** a component with a route facet. It is the unit that owns a URL, and everything
a URL implies, and that does not fold into the tree model.

What a page owns that a component or template never does:

- **A route**: the pattern, its parameters, the shell bucket (`admin`, `site`, `member`) it
  belongs to, and so the base path, the route gate and the client router that may resolve it
  ([blazor-web.md](blazor-web.md) › Route reachability, [shells-and-themes.md](shells-and-themes.md)
  › Shell dispatch). A shell never resolves another shell's page; that is a property of the
  page's assembly or data, never of a node in a tree.
- **Authorization at the URL**: the route permission, `[AllowAnonymous]`, the login redirect, and
  the auth-cookie render-mode exception. The tree filter (§ 10) runs *after* the page admitted
  the request.
- **Document concerns**: title, metas, canonical URL, culture and `dir`, head and foot
  resources, the response status (404 for an unresolved path, 301 for a moved alias).
- **A template choice**: which layout and which template render it, resolved per shell, theme
  and display type (§ 5, § 6).

Three kinds of page, one route table:

| Kind | Route comes from | Tree comes from |
| --- | --- | --- |
| **Module page** (`@page` component in a `blazor-wasm/`, `member-wasm/` or `site-wasm/` library) | the `[Route]` attribute, described by the assembly's catalog (`IComponentCatalog.DescribePages`, `PageDescriptor`) and read per bucket into the route table | code: the component renders itself, or builds a tree with `IDisplayManager<T>` and hands it to the renderer |
| **Content page** (a content item with `AutoroutePart`, or a type a module routes) | Autoroute, through the site's content route component | the item's template (decision 10) |
| **Designed page** (a tenant-authored tree with its own URL: a landing page, a dashboard, a per-type admin page) | data: a page record with route pattern + bucket (decision 9) | its stored tree |

The third kind is what blazordesigner.md's page editor and content-engine.md's per-type designed
pages need, and it is why the route table ([blazor-web.md](blazor-web.md)) accepts data-defined
entries beside scanned ones, with the same bucket gate, the same permission check and the same
per-tenant cache invalidation. A designed page in the admin bucket is reachable only through the
admin shell; in the site bucket only through the site shell; it never changes shell by being
edited.

A page is **its own kind**: it is never in the component registry, never a child of anything,
and carries the fields only a page has; components and composites are placed under it. A page is
not a template either: a template has no URL, and the same template renders many pages (every
`Content__BlogPost`). A page may be rendered by a template, and a designed page may embed its
tree inline, but the route, the bucket and the authorization are the page's.

### 7. Content items

- Rendering is driver-driven: a part's display driver emits a shape, placement puts it in a
  zone, a binding picks the component. A component is placed by a template, a page's tree, an
  item's own tree (`DisplayTreePart`, below) or a layer rule.
- **Field and part displays are components** bound by shape name: every content field (`TextField`
  and its `Header` display mode, `HtmlField`, `MarkdownField`, `NumericField`, `BooleanField`,
  `DateField`, `DateTimeField`, `TimeField`, `LinkField`, `OptionPickerField`,
  `LocalizationSetContentPickerField`, `UserPickerField` in both display modes, `MultiTextField`,
  `YoutubeField`, `MediaField`, `TaxonomyField`, `GeoPointField`), `TitlePart` (with its summary),
  `HtmlBodyPart`, `MarkdownBodyPart` (a sanitized markup parameter), `BagPart` and `ListPart`
  (their drivers add the contained items' displays while the shape is processed, so the tree maps
  them as children; the list adds its `Pager`/`PagerSlim` after its items), `TaxonomyPart` (the
  term tree, linked), `ListPartFeed` (a `HeadLink`: the list's RSS link in the page head). The
  site's culture switcher is `CulturePicker`, placed by a template or a layer. The drivers' view models expose what the components read (`Label`,
  `FieldClass`, resolved `Items` for pickers and taxonomies, the link text, the YouTube size, the
  title's link). Field labels are data-localized: `FieldDisplayLabels` sets each field shape's
  `Label` from `IDataLocalizer` under the field's data localization context.
- **An item's own body** is a `DisplayTreePart` (Templates module): it stores the item's display
  tree - the node format templates produce (components, properties, zones, bindings) - as JSON on
  the item (`Tree`; a node without an id gets one from its position, `DisplayTreeJson`).
  `DisplayTreePartBinding`, an `IDisplayTemplateBinding`, maps it into the item's shape zone like
  any other part, so the builder fills, filters and overrides it like a template's tree: ids and
  references are scoped under the part's instance id, markup leaves are sanitized, a node naming
  a published template is expanded as a composite under the template limits, and the node budget
  counts. Saving validates it (`DisplayTreePartHandler`: `DisplayTreeValidator.ValidateStored`,
  the schema check of stored properties, zones and required parameters, plus the workflow action
  validators). Its editor is `DisplayTreeEditor` (`Edits = ["DisplayTreePart"]`), JSON validated
  as you type, until the designer canvas scoped to one item replaces it.
- **Field editors** are components too: the editor tree resolves to editor components, and
  `UpdateEditorAsync` consumes the posted values. The generic content-item editor in the admin
  shell is a rendered editor tree, not a hardcoded form, which is what per-type designed pages
  ([content-engine.md](content-engine.md) › Still to build) need. Editors read and write through one
  `ContentEditorContext` (with its visibility conditions and picker cascades), cascaded by the
  `ContentEditor` root, which publishes the edited content as an output. The server builds the
  editor tree as shapes (`ContentEditorTreeBuilder`, `GET api/grove/content-items/{id}/editor`,
  `new-{type}` for a new item), so a `ContentEditor__{Type}` template lays a type's editor out.
  Editor components: text, number, yes/no, date, option picker, HTML, Markdown, link, title,
  permalink; the option picker field's editor (Grove.Contents.OptionPicker's client library; for content
  items the selection in order with edit links, a search by title, a create link per allowed
  creatable type); `BagPartEditor` (each contained item through its type's editor tree;
  add from the allowed types, remove, reorder), `UserPickerFieldEditor` (searching
  `api/grove/content-fields/users`); `TaxonomyPartEditor` (the term tree, each term through its
  type's editor tree) and `TaxonomyFieldEditor` (check boxes, radios or tags); `ListPartEditor`
  (the list's children, their order, a child created on the native create page with the list as
  its container, the feed values); `SeoMetaPartEditor`; `GeoPointFieldEditor` (a Leaflet/OpenStreetMap
  map, Google Maps when the field names a key);
  `LocalizationPartEditor` (the item's translations, opened or created); `IconPicker` (Grove.Display.Icons).
  An editor may declare a field's editor setting (`Edits = ["TextField-IconPicker"]`), which the
  editor tree prefers to the field type's own. The editors that act on the stored item (a list's
  children, translations) read its id from the context (`ContentEditorContext.ContentItemId`,
  none until the item is saved). `GroveFormManager` renders the same editors from the type's
  metadata.
- **Saving validates on the server.** The content items API runs the content manager's
  `ValidateAsync` (every part's and field's handler `ValidatingAsync`: required values, lengths,
  ranges, URLs, Liquid that must parse, permalink and alias uniqueness, the right to set the home
  page, the canonical address, the SEO schema, media extensions, a taxonomy's term type) before
  create, update and draft, and refuses with the error contract's map keyed as the editor tree
  keys its editors (`Part.Field`, `Part`; the empty key for the item). The editor page puts the
  map under the tree's `ContentEditor` node (`DisplayState.SetErrors`), which shows each error
  under its editor. Markup a type's settings ask to sanitize (HTML field and body, HTML menu item)
  is sanitized by the handlers on create, update, draft and import.
- **An item's page body is searchable**: `DisplayTreePartIndexHandler` indexes the text its tree
  holds (`DisplayTreeText`, a migrated Liquid body's composite source included), and the part
  answers the body aspect with it.
- **The editor's preview** is `ContentPreviewPane` beside the editor tree: the unsaved item
  rendered by Grove.Contents.Preview's `POST api/grove/content-preview` the way its display renders
  (drivers, placement, templates, the node filter), as HTML in a sandboxed frame; the latest
  version of a stored item, a draft included, is the display endpoint with `version=latest`.
- **An item's admin pages** are the native pages of the page registry: `ContentAdminPaths`
  (Grove.Contents.Management.Abstractions) names them for code outside the admin shell (the Liquid
  `{% a edit_for/admin_for/create_for %}` tags, admin menu nodes); `ContentItemMetadata` carries
  only the display route values.
- **Content editing is a stripped-down designer.** The marketing team's WYSIWYG is the designer
  in a reduced mode: the same component trees, a palette limited by role. There is one system for
  content and UI structure; "content" is a tree an editor fills, not a separate body format.
- The display tree carries **resolved** values (a picker's labels, a reference's display text), so
  the renderer and headless clients do not re-resolve. The raw `?parts=` read stays for editing.
- **Shortcodes are not part of the system.** Every shortcode becomes a component or a template;
  body text holding `[image]`/`[asset_url]` is migrated to media component nodes, keeping the id
  and path references ([shortcodes.md](shortcodes.md) tracks the migration).

### 8. Headless

- `GET api/grove/content-items/{id}/display?displayType=Detail` returns the display tree: the same
  JSON the client renderer consumes (ViewContent through the access decision; an editor may
  display the latest draft). A headless client gets structure, resolved values and component
  names; it renders them with its own components. That is the parity rule ("every capability a
  Blazor page has is reachable through the API") met by construction, since the Blazor page *is*
  that tree. The admin page `/Contents/ContentItems/{ContentItemId}/Display` renders the same
  tree.
- `?format=html` on the same endpoint and `IDisplayHtmlRenderer` render the tree to static HTML
  with `HtmlRenderer`, for email bodies, PDF sources and cached static pages.
- The API is on by default; pages are not (decision 10).

### 9. Design systems

- Tokens (`--grove-*`) are the styling contract. A component's style-typed parameters have the
  `token` editor kind, so a template picks a token, not a color.
- The design system ([design-systems.md](design-systems.md)) resolves per user, shared, tenant,
  default, and the result is loaded for the site shell as it is for admin.
- A template may carry **responsive variants**: per-breakpoint property overrides on a node. Data
  only; the renderer applies the variant for the current breakpoint. Not built.
- **Localizable by default; "Master" mode for full control.** The style schema exposes only what
  survives localization: logical properties (`inline-start`, not `left`), tokens,
  direction-aware layout, no fixed widths on text. The shell root sets `dir` from the culture, so
  every tree flips without a component knowing its side; captions stay localized data. A
  **Master** mode unlocks raw CSS and physical properties for people who want complete control,
  and every such edit carries a notice that it will break localization (right-to-left layout,
  text expansion) and is excluded from the localizable guarantee. Master edits are marked in the
  template so the designer and the headless API can tell them apart.

### 10. Security

- **The trust boundary is the installer, not the tenant.** Code enters the system only as modules
  the host's installer builds in and has vetted; a tenant can enable and disable those features
  and edit UIs as data (trees, Liquid templates, placement, design systems), never add code. A
  workflow sandbox for admin-granted code is a later, separate design
  ([architecture.md](architecture.md) › Direction); until then expressions are Liquid.
- **Tenant templates are data, never code.** The tree is validated against the registry; the
  renderer instantiates only registered types; `Type.GetType` on a stored name is never used.
  No tenant HTML or script reaches the page: unsanitized HTML is not offered, and behaviour is
  workflows only (decision 5).
- **Expressions** run on the server through the sandboxed engine (member allow-list), before the
  tree leaves the server, under the render limits (time, steps, recursion, depth, size).
  **Limits fall back, they do not retry**: evaluation is deterministic, so an overrun becomes an
  error node for the subtree.
- **Composition parses each template on its own** and links the parsed templates into one tree
  with a scope per child holding only its declared parameters; template sources are never
  concatenated, so a child's syntax and `{% assign %}` cannot reach the parent. A composite that
  places itself (directly or through others) is refused on save and capped by depth at render.
- **Permission sets**, assigned **per page** (or shell), never per component, which would be
  impossible to nest and track:
  1. **Design**: may edit this page or shell (its template, overrides, zones).
  2. **Schema**: may see the schema available for design work (types, fields, queries, components
     and their parameters) without seeing any data. A designer builds the marketplace page knowing
     the item schema and never an item.
  3. **Data**: may read the data, which applies only at run time (viewing, or testing a design
     with real data). No data permission is implied by a design one.
  4. **Shared components**: may edit a node definition (a base or a node override) that other
     instances and pages inherit from (§ 5a). Tenant-wide, admins only by default; the
     designer's base-edit mode is gated by it.
  5. **Publish**: may make a draft live. It applies **per page and per component** (a node
     definition or an instance), because a component has a draft state of its own: without it
     there is nothing to preview before a change reaches every page that inherits it. Editing
     produces drafts; publish is the separate act, on either object.

  Workflows care about schema and data only. Per designed page, `DesignedPagePermissions`
  generates the sets as permissions a role is granted: `DesignPage_{name}` (edit the draft,
  preview), `ViewPageSchema_{name}` (list the page and read `api/grove/components?page=`, implied
  by Design), `ViewPage_{name}` (Data: view it at run time, unless the page allows anonymous
  callers) and `PublishPage_{name}`; `ManageDesignedPages` creates pages and implies every page's
  sets (the shell-level assignment, administrators by default). Shared components are
  `ManageSharedComponents`. Not built: Publish per component (a node definition or an instance has
  no draft of its own yet), and per-template sets beyond `ManageTemplates` /
  `ManageAdminTemplates`.
- **Permissions are applied when the tree is built, on the server:** a node whose component or
  content the user may not see is not in the tree. The per-part read rule of
  [permissions.md](permissions.md) is applied through the node filter. The client never decides
  visibility. **The tree reaches the browser** as persisted state, so everything in it, resolved
  values on undisplayed nodes included, must be the viewer's to see.
- **Markup** is sanitized on save and before a tree leaves the server (`DisplayTreeFilter`); the
  renderer never feeds a browser-bound value into a markup parameter.
- **Actions only on interaction**: no tag starts a workflow during evaluation; a display action
  runs only a workflow with a UI action trigger, with antiforgery, its arguments checked against
  the trigger's declared inputs (decision 5).
- **Caches vary by viewer** (decision 12): a tree built with one caller's permissions is never
  served to another.
- A template's author needs a template permission; the designer is gated like any admin page.

### 11. The designer edits Liquid

With Liquid the only authoring form, the designer works the way Gutenberg works on its text
format: it parses the template (Fluid exposes the parsed statements), shows the tags it knows
(`component`, `compose`, `zone`, the primitives) as editable nodes with the registry's schema in
the property pane, and shows everything else (loops, conditions, raw markup) as opaque blocks it
preserves verbatim. A template a person hand-edits may therefore become partly uneditable in the
designer; that is the accepted trade for one canonical form.

- **Preview is evaluation, just in time.** On each edit the designer writes the tag back into the
  text and sends the template to the server, which parses it (cached by source), evaluates it with
  the real display context, applies the overrides and returns the display tree; the canvas
  renders that tree with the same renderer the live page uses. Liquid never runs in the browser:
  the tags call services, and preview enforces the data permission.
- **Overrides are not in the text.** The template text defines the base nodes; an override is a
  record bound to a node id or an instance id (§ 5a) and applied at evaluation. The designer
  therefore writes the **instance id into the tag** (`id: "…"`) when a node is inserted, and
  never regenerates it, so overrides survive edits around them.
- **Typed text is data.** In the stripped-down content designer, what an editor types (including
  `{{` or a code sample) is stored escaped as a markup value, never written into template source.

### 12. One expression engine

The workflow engine's expression model is the one contract for workflows **and** display. An
activity is described once from attributes into an `ActivityDescriptor` with
`InputDescriptor`s (type, default, UI hint, UI handler). An enum input is a dropdown
automatically; a dynamic picker is a `DropDownOptionsProviderBase` registered in DI and named on
the input; options can be fetched lazily per property; the studio renders every hint through
registered `IUIHintHandler`s. A value is an `Expression { Type, Value }` resolved by a descriptor
registry (Literal, Variable, Input, Liquid, JavaScript) in an `ExpressionExecutionContext`. That
is the component schema, the property pane and the binding model this design needs:

1. ☐ The engine's `Expression` model, descriptor registry, handlers and input metadata are the
   contract for display too. A component descriptor is the activity describer applied to Blazor
   parameters; a node property and a slot are `Expression`s; the designer's property pane reuses
   the studio's UI-hint handlers and option providers.
2. ☑ The platform's Liquid is the only Liquid. `PlatformLiquidTemplateManager`
   (`Grove.Workflows/Server/Expressions`) replaces the engine's template manager, so a workflow's
   Liquid expression is parsed and rendered by the platform's (cached parser, tags, filters,
   member allow-list) with `Variables`, `Input` and the workflow ids as properties; the workflow's
   variable types are admitted to the allow-list. One Fluid version (2.40). The engine's own
   Liquid filters (`base64`, `keys`) are not in the platform's set.
3. ☑ The platform-side workflow evaluators are gone: the stock activities use `Input<T>`
   expressions ([workflows.md](workflows.md)).
4. ☐ **`Grove.Scripting` is removed** and replaced by workflows entirely. A workflow can be
   written as a script or viewed and edited as a flow diagram; both are front ends for the same
   backend objects. Everything that calls `IScriptingManager` today (recipes' `[js: …]`, layer
   rules, the stock tasks) moves to the one engine; workflows.md sequences this in its Retire
   step, after the generated surfaces.
5. ☐ A display `ExpressionExecutionContext` (item, route, user, tenant, query) joins the workflow
   one.
6. ☐ Jint and Fluid limits (steps, time, recursion, memory) are configured in one place: the
   start of the sandbox that admin-granted JavaScript, C# or Python needs. Neither a C# nor a
   Python provider exists in the vendored engine.

How a template declares inputs, limits and the designer hints for zones are built on this model,
not beside it. The operations registry, the request path, the four pipelines and the one access
machinery are in [workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery).

## Decisions

The standards the pieces above rest on. Other docs refer to them by number.

### Decision 1: value types

One registry in `Grove.Data.Types`: the one place a value type is declared with its field type,
editor, display primitive, query column type and workflow type. A module that owns a type
(Money) stays its own module and registers its types there. This is where the type system meets
the query system, so it sits with data, not with the UI. Built: `ValueTypeDescriptor` (name,
query column type, workflow type, field types, editor and display components, CLR type, owning
module), the base types over the content fields, `IValueTypeRegistry` and `AddValueType`
(a platform library referenced by `Grove.Data`, registered by Grove.Infrastructure.Server; `Grove.Data.Types.Money`
registers `Money`). Not wired: the query system and the display schema reading it.

### Decision 2: primitives

Modules supply primitives through the component registry: a module that registers a value type
registers the custom-coded primitives that edit and display it, so the type system and the UI
system connect through the registry. A base set (`Text`, `Heading`, `Markup`, `Image`, `Link`,
`Button`, `Container`, `Stack`/`Grid`, `Input` per value type, `Zone`, `Repeater`, `Form`) carries
semantics and accessibility (heading level, landmark role, label) in its schema. Headless parity
is per module: a headless client renders the base set plus the primitives of the modules it
chooses to support, and the display tree names the owning module on every node so it can tell.
Trust sits with the installer who builds the module in.

### Decision 3: style schema

Localizable by default (logical, token-bound, direction-aware); Master mode unlocks raw CSS with
break-localization notices; Master edits are marked in the template (§ 9).

### Decision 4: context and bindings

**Everything a template reads comes through the query system.** The context is `Route` (the page
record's typed parameters, the one non-query input) plus named queries. `Item`, `User` and
`Tenant` are built-in **system queries** (the item by its route parameter, the current
principal, the site settings) served by an in-process source with no SQL behind it and memoised
per request; they are display facts, never permissions. A slot binds `{ query, parameters, path }`;
a parameter is itself a binding (to a route parameter, to another query's result, or to a
component's own input, so a list's search box re-runs its query), which is how **filtering**
works; build-time filters stay in the saved query. Permissions arrive by construction: the query
system injects the caller's scope into every query ([queries.md](queries.md)) and the node filter
(§ 10) drops what remains. A template declares the queries it uses, so bindings validate against
the builder's typed column cache and the designer's slot picker is the query picker.

**Two systems, one direction.** Queries are the read system and run through connections they
consume from the data layer and the protocol modules (a write request through a connection is a
query too, per queries.md); workflows are the action system and
call queries, write queries included. Queries never call workflows, and a slot never binds a
write query; a write is reachable from a workflow action only. A template exported to a tenant
lacking a workflow it names has a button that does nothing, reported by validation.

### Decision 5: actions

Behaviour is workflows only, through a typed contract: a workflow opts in to being run from the
UI with a **UI action trigger** (`UiActionTrigger`) that declares its `name: type` inputs. A
button or form runs such a workflow, navigates, or submits the current item; nothing else is
code. The run endpoint refuses a workflow without the trigger (404) and arguments that do not
match (400, with `errors` by input, `UiActionContract.CheckByInput`); the tree filter leaves out
actions on such workflows; template validation checks each `run_workflow` through
`IDisplayActionValidator`.

### Decision 6: zone keys

Keys are registry entries with a schema. Grove ships the well-known keys (`Header`, `Navigation`,
`Content`, `Toolbar`, `Status`, `Sidebar`, `Footer`); a module adds keys it owns under its own
prefix (`{module}:{page}:{region}`). A key declares what may fill it (allowed component names,
single or multiple) and whether it is required on a layout. A module may fill a well-known key
without declaring it; any other key only its owner declares. **Ordering is by priority**: a
contribution ships a default priority, priorities are settable, and the tenant reorders a zone's
contributions through the override system, on the zone (for every page) or on a page's zone. The
designer's `Zone` node offers a key picker, an unknown key is a validation error on save, and a
layout missing a required key gets a warning.

**Zones are components** and carry the full override model of § 5a. Every component declares
whether more than one instance per page is allowed; the designer refuses a second placement
where one is declared.

Built: `ZoneKeyDescriptor` (key, owning module, allowed components, single or multiple, required
on a layout), the well-known keys in `ZoneKeys.WellKnown` (`Content` required), module keys
declared with `[assembly: ZoneKey("prefix:*")]` (a trailing `*` declares a family under a prefix;
a module cannot redeclare a well-known key). The catalogs collect a library's
`IPageRegionContributor`s as `ZoneContribution`s (key, component, default priority); the registry
serves `ZoneKeys`, `TryGetZoneKey` and `Contributions(key)` on the server (feature filtered) and in
the browser (rebuilt on a lazy load), and `GrovePageRegion` renders from it. A layer placement
into an unknown key, or of a component the key does not admit, is a save error; a layout without
a required key and a contribution to an undeclared key are logged warnings;
`GET api/grove/components/zone-keys?shell=` lists the keys; `DisplayTreeValidator` refuses a
second instance of a component declared single. Grove.Parties declares `Parties.Pane:*` and
`Parties.Detail:*`. Not built: priority reordering through overrides and a `Zone` component node;
both come with the designer's zone settings.

### Decision 7: template hierarchy

The platform's alternates chain, as data, with qualifiers. A template binds a shape name plus
optional **display type** and **theme**; resolution (`TemplateResolver`) is most specific first
(names most specific first, then theme and display type, theme, display type, none), down to the
module's shipped default, and below that to the **error pages** (404, 403, 500). Theme is the
only placement qualifier: a shell and its theme are one thing for this purpose (the admin, site
and member shells each have an active theme), so a `Content` fallback can differ on the admin
theme and the member theme without a separate shell qualifier. Composites are named templates
placed by name; a pattern is a node override with no further link. Versioning (draft, published,
history) comes from § 5a and § 10. Recipe export carries a template with its overrides as one
unit and references components by component id, so another tenant's map resolves them to its own
node ids.

### Decision 8: theme as data

Both kinds exist. Most shipped themes are compiled modules (manifest, bucket, `grove-blazor`,
base-theme chain, code components) that ship their set as a recipe; a data theme is that set
alone, created in a tenant or imported. A **public marketplace carries data themes only**,
because a public marketplace for code is risky; plugins (code) have a separate marketplace, and
a plugin that includes a theme registers it through the same mechanism a data theme uses, so
every theme imports and exports between systems the same way.

**A theme is page structures plus branding.** The theme system (`Grove.Themes`) separates the
**structure** (page and layout templates, composites, zone settings) from the **design system**
on top of it (branding: fonts, heading settings, corner radius, tokens;
[design-systems.md](design-systems.md)). A theme can then be swapped while the branding stays, or
a single page imported and made to conform to the tenant's branding. The admin, site and member
themes stop being swapped wholesale; only the selections inside them change, which needs the
theme registry and selection system reworked without losing any shell injection mechanism
(permissions, required types, contracts). That rework is its own plan, after the display system
([shells-and-themes.md](shells-and-themes.md) › Theme compatibility). The compatibility contract
(`grove-blazor`, buckets, the two guards) holds for the compiled part of a theme.

### Decision 9: designed pages

Data-defined route-table entries. A page record (route pattern with typed parameters from
`Grove.Data.Types`, bucket, permissions, what it renders) is served by a route-table provider that
reads content, cached per tenant and invalidated on publish; the entry exists once the page is
published and the draft is reachable only through preview. The same bucket gate, route
authorization and cache apply as to a module `@page`. A page is its own kind (§ 6a): its fields are
route and parameters, Autoroute and alias, bucket, title and metas, the permission sets; its
children are components or composites. **Pages have no overrides**: an override of a page would
change routing, so a page record is edited directly, with drafts and publish (§ 10) but no node
or instance override chain; the components under it keep the full § 5a model. The top of a
designed page's tree is the page record itself, rendered through the layout decision 7 resolves
for it.

Built: the `DesignedPage` content type (`DesignedPagePart`: name, route pattern, bucket, title,
meta description, anonymous access, Liquid source) in the Templates module, read through
`DesignedPageStore` (the published set cached and re-read on publish). Its tree is the shipped
template `DesignedPage__{Name}` (`DesignedPageTemplates`), evaluated with the route's values as
`Model.Route`. `DesignedPageSource` (a dynamic page registry source) adds them to the route table
per request after the declared pages, registering the site bucket's pages on the site's content
route component, and `SitePageBuilder` answers a matching
path ahead of Autoroute, inside the site layout. `api/grove/pages` (list, get, save draft,
publish, delete, preview a draft at a path) validates the name, the bucket, the route (pattern and
uniqueness in the bucket) and the tree; the `DesignedPages` recipe step creates and publishes
them. Not built: admin and member pages are stored and validated but not routed (neither shell
has a component that renders a data-defined page); route parameters are strings until
`Grove.Data.Types` gives them types; the designer's page editor. Designed pages are the
**defined** layer of the page registry ([server.md](server.md), [admin-routes.md](admin-routes.md)).

### Decision 10: one format, and no generated pages

The display tree is one typed node format. **The shape system stays as the backend**: drivers
and placement build `IShape` trees, and the tree builder converts them once (§ 3); nothing
downstream knows `IShape`.

**A content type without an assigned template does not get a page.** The alternates chain
(decision 7) ends at the error pages, not at a driver-built page. The shipped
`DefaultContentTemplate` (`Grove.Infrastructure.Server/Display`) is the default `Content` template for the
Detail display type (the title, then the item's `Header`, `Meta`, `Content`, `Tags` and `Footer`
zones as the drivers filled them and the node filter cut them); it exists only while the
per-tenant setting `SiteDisplaySettings.RenderUnassignedContent` is on, because a tenant may not
want unassigned types to render. `SitePageBuilder` asks the resolver alone, so the setting and a
tenant template decide alike. Drivers and placement remain the generator for the admin editor
trees (§ 7) and the field list the default template binds to.

**The API is on by default; pages are not.** A content type's items are served by the content
API (REST, GraphQL, § 8) under the data permission as soon as the type exists, while a page and a
URL exist only when a template is assigned (or the default template is enabled). Unlike the
upstream platform, an Autoroute part alone does not give an item a rendered page.

☐ The setting has no admin screen (recipe `settings` step only).

### Decision 11: edit mode is an overlay

The designer's canvas is the real renderer and the real page, rendered **unmodified**; the
content never carries designer markup. The root document has two layers: the **content** layer,
and a **designer** layer above it (`z-index` far above anything content can reach,
`pointer-events` only on its own handles) that draws hover and selection boundaries, snap guides
and drop markers.

- **Locating a node.** The renderer marks every node's root in the DOM with its instance id, as
  a `data-` attribute when the primitive forwards attributes to its root (the convention for
  Grove-shipped primitives) and otherwise as a comment marker the renderer emits before the
  node; the designer resolves markers to elements.
- **Measuring.** Layout is the browser's, so rectangles come from JS (`getBoundingClientRect`,
  kept fresh by `ResizeObserver` and `MutationObserver`) and are batched to the designer; the
  overlay is drawn from those rectangles. Only the hovered and the selected nodes are drawn by
  default; a "show all boundaries" mode draws every node.
- **Snapping.** The overlay snaps a drag to the edges and centres of the measured rectangles of
  sibling nodes and zone bounds, draws the guides, and on drop turns the target into a template
  edit (the tag moves in the text, § 11), then the page re-evaluates.
- **Reading and writing a node.** The property pane reads the evaluated tree and the registry
  schema, never the DOM. Only inline text editing inserts a temporary editing surface over that
  one node, removed on blur.

### Decision 12: caching the rendered tree

Two trees exist: the **unfiltered** tree holds everything the template binds and never leaves the
server; the **filtered** tree (after the permission filter, slots resolved) holds only what the
requesting user may see and is as sensitive as the page they received. There is no per-user
server cache (it scales by users × pages × cultures).

- **Client-held filtered trees**, validated by a composite version sent as an ETag; the server
  compares versions without evaluating and answers unchanged or sends a fresh tree. Session
  storage, cleared on sign-out.
- **A small server cache of unfiltered trees** per page, item version and culture, bounded by
  content size and not by users, filtered per request, serving cold loads and misses.
- **Anonymous and role-shared pages** share one filtered tree per permission set, the one place a
  shared cache or CDN applies.
- Drafts and previews are never cached.

Built: the composite version is `DisplayTreeVersions` (`Grove.Display.Management/Components`): a
stamp taken when the tenant starts, a content counter bumped on every publish, unpublish, update,
import and removal (`DisplayTreeContentHandler`), the culture, the `IDisplayTreeVersionPart`s
modules register (Templates: the published set and the overrides document; Layers: the layers
document; Grove.Infrastructure.Server: the site settings) and the caller's scope (the path,
`CallerContext.ScopeSignature`: user, roles, permission version). `api/grove/site/page` and the
published-item path of `api/grove/content-items/{id}/display` send it as the ETag and answer 304
on a match before building anything. The site shell's `ContentPage` keeps pages in session
storage once interactive, revalidates with If-None-Match, and drops them when the scope changes.
Anonymous site pages share a filtered tree through `DisplayTreeCache` (a tenant singleton bounded
by size).

☐ The unfiltered per-page cache for signed-in viewers. The tree builder still applies per-viewer
decisions (component permissions, menus filtered by their driver, the drivers' own checks), so a
built tree is not viewer-independent; the cache needs those decisions moved into the node filter
first. It is an optimization and comes last, once the display system's shape is final.

### Decision 13: publish

Publish is its own permission, per page and per component, since components have drafts of their
own and a preview needs them (§ 10, set 5). The tenant setting in § 5a defaults to "edit for
preview, a manager publishes". Data at run time is the only data permission, and the per-part
read rule from [permissions.md](permissions.md) is one of its implementations through the node
filter (every node carries what admitted it; the coordinator asks the filter before adding a part
or field shape).

### Decision 14: design-system tokens

A reference palette feeding one flat semantic namespace (no component-token layer; primitives
declare which semantic tokens their style parameters default to), fixed categories, numeric and
t-shirt scales, tokens extensible by name through the registry with per-tenant and per-user
overrides, presets with per-audience override permissions set at tenant level, modules never
changing another module's defaults unless a build-time setting allows it, inheritance compiled at
publish. The design system is the visual language and branding; the theme is structure. Detail in
[design-systems.md](design-systems.md) › Token taxonomy.

## Module homes

- **`Grove.Display.Management.Abstractions`** holds the display tree node, the component registry
  contract, the descriptors and the tree renderer: a small neutral library below
  `Grove.Display.Components`, referenced by the server and every theme client, with no server or UI
  framework in it, so it loads in the browser. The server-only pieces (the shape-to-tree mapper,
  the component and template bindings, the Blazor `IDisplay`) are in `Grove.Display.Management`.
  The component source generator is in `Grove.SourceGenerators`. Work stays within the
  platform's display management, reworked in place; no parallel modules.
- **`Grove.Data.Types`**: the value type registry (decision 1).
- **`Grove.Data.Global`**: the standardized global data set imports and loading
  ([globals.md](globals.md)) plus the global data registry, so no module reimplements either.
- **`Grove.Display.Components.Primitives`** (the primitive components; modules register theirs there) and
  **`Grove.Display.Components.Templates`** (the higher-level template trees modules ship: composites,
  default templates; the Templates module's tenant data layers over them). Not created; see Open
  questions.
- **The Templates module** is reworked in place, keeping its feature id.
- **Shape names are the registry keys** (`Content__Page`, `Zone`), so placement, alternates and
  templates all bind by the same names.
- **Editor kinds are editor components in the registry**, not a vocabulary in code; modules add
  one by registering a component that declares what it edits.
- **Custom data types are not a prerequisite** for rendering: a tenant content type has parts and
  fields with drivers and builds a tree like any other.

## Data model

| Entity | Where | Notes |
| --- | --- | --- |
| Component descriptor | Code, generated at build; cached per tenant/theme at run time | The registry. Not content. |
| Binding (code) | Shape table, from attributes and `Describe(...)` | Per theme, per feature. |
| Template (and Layout, composite) | Content item, versioned | The tenant's bindings. |
| Overrides | `ComponentOverridesDocument` | Variants and instance overrides (§ 5a). |
| Placement | `placement.json` per feature + the Placements module's document | `api/grove/placements` and the admin Placements page edit it. |
| Page | `@page` component, content item (Autoroute) or `DesignedPage` | § 6a. |
| Item body | `DisplayTreePart` on the item | § 7. |
| Layer rules | Layers module documents | A rule places a component instance into a zone key while the tree is built. |
| Design system | [design-systems.md](design-systems.md) | |
| Display tree | Not stored; built per request, cached per decision 12 | The wire format. |

## Rework plan

Each step a module reworked in place; each compiles and runs on its own, and the HTML path is
removed per page as its Blazor replacement exists, not all at once.

1. ☑ **Registry and schema** (§ 1): the source generator, the descriptor, the attribute, one
   registry replacing the three; the route table reads the catalogs. ☐ A theme scope is applied
   only when a caller passes the theme id; the scoped registry passes none and admits every
   enabled theme serving the shell.
2. ☑ **Display tree and renderer** (§ 3): `DisplayNode`, the renderer library, the component
   binding in `ShapeBinding`, the headless display endpoint and the admin display page.
3. ☑ **Site shell on the tree** (§ 6): `SitePageBuilder`, the site client, the layout and content
   components, menus. The MVC site routes are out of the request path: Autoroute's and the home
   route's dynamic routes are gone (their address schemes still generate URLs), and
   `Contents/ContentItems/{id}` only generates (`GenerationOnlyRouteConstraint`). The site shell
   middleware resolves site paths through the route table and forwards a path a non-Blazor
   endpoint owns to it (`ILegacyRouteOwnership`). BagPart stays in Flows (with the content card views and the
   shapes the BagPart editor uses).
4. ☑ **Templates module rework** (§ 5), with composites, the UI action contract (decision 5) and
   overrides (§ 5a).
5. ☑ **Editors as trees** (§ 7).
6. ☑ **Headless HTML, layers as a build step, Placements editor** (§ 6, § 8). The layer result
   filter, `LayerMetadata` and the widget admin are gone; the Placements page replaces the MVC
   editor and its content-type shortcuts.
7. ◐ **Retire** the HTML path. ☑ `IHtmlDisplay`, the Razor and Liquid shape engines, the tag
   helpers, `BlazorComponentShapeBindingResolver` and the markup fallback leaf are deleted with
   MVC; the admin and site shells are the Blazor themes. Every shape on the content path binds a
   registered component (`ContentPart` for a part with fields and no driver, `TermPart`,
   `PublishLaterPart`, `ArchiveLaterPart`; `ListPartFeed` is `HeadLink`; `LiquidPart` is retired,
   its bodies migrated to `DisplayTreePart`; [MVC/shape-engine.md](MVC/shape-engine.md) ›
   Component coverage). What remains:
   - ☐ Browser checks for the native pages that replaced the MVC settings and definition screens.
   - ◐ **Razor → Blazor component parity audit.** Every Razor view and shape template and every
     widget ([widgets.md](widgets.md)) has a Blazor component equivalent. Component boundaries
     need not match the Razor ones, and the existing components in `Grove.Display.Components` are reused
     wherever possible rather than duplicated. The inventories are
     [MVC/views.md](MVC/views.md) › The inventory (views) and
     [MVC/shape-engine.md](MVC/shape-engine.md) › Component coverage (shape templates). Liquid
     templates are not part of the audit. Tracker: [MVC/parity.md](MVC/parity.md). No `.cshtml`
     file remains.
   - ☑ **Liquid templates on display trees.** A template emits a display tree through the
     template tags (§ 5) and reads its data through bindings and slots (§ 4a, decision 4); the
     shipped templates (the site recipe's content templates) are tree templates. The shared
     Liquid has no view tags and no request values ([MVC/shape-engine.md](MVC/shape-engine.md) ›
     Liquid).

## Open questions

- **`Grove.Display.Components.Primitives` / `.Templates`.** The registered primitives derive from the
  Radzen-derived base classes in `Grove.Display.Components`, so a separate primitives project means
  moving that whole library or leaving those primitives behind.
- **Template security hardening** (to settle once the display system's shape is final):
  1. The member allow-list is the sandbox. A template is evaluated as the viewer, so an editor's
     template opened by an admin reads with admin rights (confused deputy) and can exfiltrate
     through markup. Tenant templates get no `Request` (cookies, headers, form, query) and no
     `HttpContext` ([MVC/shape-engine.md](MVC/shape-engine.md) L3). Proposed: remove as well
     `encrypt`/`decrypt`, the `liquid` filter, `users_by_id`/`users_by_name`/`user_email`,
     `Site.SuperUser`, `shape_render`/`shape_stringify`, `console_log`, with data reads through the
     query pipeline only. The counter-position is that no sandbox is needed beyond the installer
     boundary.
  2. Preview of another user's draft runs with the reviewer's rights: evaluate under the author's
     data rights, or block outbound requests on preview with a strict CSP.
  3. A Content-Security-Policy on rendered pages (`img-src`, `connect-src`, `form-action`) as a
     backstop against exfiltration through injected URLs.
  4. Typed parameters: URLs refuse `javascript:`/`data:`; raw CSS (Master mode) allows `url()`
     exfiltration and overlays, so it takes its own permission.
  5. The palette is enforced on the server on every path (designer, API, recipes, deployment),
     not only in the designer UI.
  6. On a timeout, serve the last good published tree (cached) instead of an error node, record
     it, and trip a per-template breaker.
- **Sandboxed custom elements** (parked, a separately permissioned design): tenant HTML/JS in an
  isolated iframe (own origin, no cookies, strict CSP) talking to the tree over a narrow message
  bridge that can only read bound values, publish outputs and request actions. Tenant C#/Razor
  compiled at run time is not part of it; real code enters as vetted plugins.

## Reference systems for the backend

Permissively licensed systems whose *data model* this design borrows from (verify licences
before porting code; WordPress is GPL and is consulted for vocabulary only):

- **Oqtane** (MIT, Blazor, copyright the .NET Foundation; every repository in the `oqtane` org
  is MIT). **The primary reference**: the only permissive, Blazor-native CMS backend. Cloned for
  local reference at `/workspaces/oqtane.framework` (with `oqtane.docs`,
  `oqtane.theme.bootswatch`, `oqtane.blogs`). What to study and port from: the data model (Site,
  Page, PageModule, ModuleDefinition, Theme, Container, Permission, Setting, Folder/File), how
  pages and module instances are stored and permissioned, the module/theme packaging and install
  model, the render-mode handling, and the module definition metadata. Its fixed *panes* per
  theme are not borrowed: this design's zones are nodes in the tree.
- **Umbraco CMS** (MIT, .NET): the Block Grid and Block List editors. Element types as block
  schemas, blocks nested through *areas* (fluid nesting, no fixed zones), content versus
  settings per block, allowed-block rules per area, the Delivery API as the headless tree. The
  closest .NET backend to this design.
- **Payload CMS** (MIT): the `blocks` field (discriminated `{blockType, fields}`), per-field
  access control, versions and drafts, localization of block content.
- **Strapi** (MIT, community edition): reusable *components* and *dynamic zones* (a list of
  components of allowed kinds), which is the template-part and slot model.
- **Plasmic**: split licence. Everything outside `platform/` (the SDKs, loaders, `plasmicpkgs`,
  the `registerComponent` API and its docs) is **MIT**; the Studio and its backend under
  `platform/` are **AGPL-3.0**, so they are never ported from or consulted for design. This
  design takes the MIT component-registration model (§ 1).

## Out of scope here

The designer UI (canvas, palette, property pane, drag and drop, undo) is
[blazordesigner.md](blazordesigner.md); it edits the templates this document defines and reads
the registry's schema. The expression engine's sandbox is [architecture.md](architecture.md) ›
Direction. Shell registry and per-shell hosts are [shells-and-themes.md](shells-and-themes.md).
