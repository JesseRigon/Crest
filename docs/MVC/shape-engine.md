# The shape engine after MVC

Workstream (b) of the MVC retirement, done together with moving the APIs out of Grove.Infrastructure.Server
([server.md](../server.md)). Siblings: [views.md](views.md) (views → Blazor components, workstream
a) and [api.md](api.md) (the API layer, workstream c). The display system's own design is
[blazor-display.md](../blazor-display.md); its step 7 is where this plan picks up.

## The answer: what the Blazor display relies on

**The shape *model*, not the shape *templates*, and Liquid only as a language.** The three
things people call "the shape engine" are separate:

1. **The shape model: shapes, display drivers, placement, alternates, the shape table.** Kept,
   and used by the Blazor display. Every content page, every `/display` and every editor tree
   starts as shapes: drivers return `ShapeResult`s (`Grove.Display.Management/Views/ShapeResult.cs`),
   placement puts them in zones, the shape table adds alternates and the displaying/processing
   events, and `DisplayTreeBuilder` (`Grove.Display.Management/Components/DisplayTreeBuilder.cs`)
   maps the resulting `IShape` tree once to `DisplayNode`s bound to registered components
   (`Grove.Display.Management.Abstractions/Components/ComponentRegistry.cs`). No MVC type is in
   the engine: no `ViewContext`, `IHtmlHelper`, `ActionContext`, `IUrlHelper`, `TagBuilder`,
   `ModelStateDictionary`, `IRazorPage`, and no reference to an MVC library.
2. **There are no shape template renderers.** A shape the registry cannot resolve becomes an
   error node naming its shape type (§ Component coverage). The Razor and Liquid-through-Razor
   view engines, the C# `[Shape]` bindings, the shape tag helpers, `IHtmlDisplay` and the MVC
   model updaters are gone.
3. **Liquid as a language.** Tree templates (`TemplateTreeEvaluator`,
   `Grove.Display.Templates/Services/TemplateTreeEvaluator.cs`, with its own `TemplateTreeParser`),
   designed pages, workflow expressions (`PlatformLiquidTemplateManager`), Autoroute and title
   patterns, HTML and Markdown bodies. None of these renders a view; § Liquid.

| Part | Where | Notes |
| --- | --- | --- |
| Shape model, drivers' display halves, placement, alternates, shape table, zones, layout accessor | Grove.Display.Management | The tree builder's input |
| Component registry, tree builder, filter, HTML renderer, versions, cache, coverage | Grove.Display.Management(.Abstractions) | The display system |
| Validation sink | Grove.Display.Management.Abstractions (`Grove.Validation`) | § The validation sink |
| Head contribution seam | Grove.Display.Management (`Document/`) | views.md › Themes, layouts and layout filters |
| Liquid language | Grove.Display.Liquid(.Abstractions) | § Liquid |

## How the MVC retirement bears on the shape engine

- **The shape engine is platform, not server**: it is `Grove.Display.Management(.Abstractions)`.
  Grove.Infrastructure.Server only hosts: the display APIs (`api/grove/site/page`, `…/display`,
  `…/editor`, templates, pages, components) are declared by the modules that own them and
  registered in Grove.Infrastructure.Server's API registry
  ([architecture.md › Modules declare, Grove.Infrastructure.Server registers and serves](../architecture.md#modules-declare-groveserver-registers-and-serves);
  [api.md](api.md) § Placement).
- **Validation is raised into the request's sink**, and the API answers it as the error
  contract's `errors` map (§ The validation sink).
- **Shared screens are required pages ([views.md](views.md) § Shared screens).** The behaviour
  is one component per screen; the registry resolves it as it resolves any component.
- **Setup ([views.md](views.md) § Setup)** is static-SSR Blazor pages in Grove.Infrastructure.Setup;
  it renders no shapes and uses no MVC type.

### Type map

DM.A = `Grove.Display.Management.Abstractions`; DM = `Grove.Display.Management`.

| Type (folder) | Where |
| --- | --- |
| `IShape`, `Shape`, `Composite`, `AlternatesCollection`, `ShapeMetadata`, `ShapeSerializer` (`IShape.cs`, `Shapes/`) | DM. Server-only: the browser's contract is the display tree (`DisplayNode`, DM.A). `ShapeMetadata` has no rendered content; its displaying/processing callbacks take a `ShapeDisplayContext` (the shape and the services) |
| `IShapeFactory`, `DefaultShapeFactory`, `IShapeFactoryEvents`, `Arguments`, `ShapeFactoryExtensions` | DM |
| `IDisplayDriver<TModel>`, `DisplayDriver<TModel>`, `DisplayDriverBase`, `IDisplayResult`, `ShapeResult`, `CombinedResult`, `ShapeViewModel` | DM; display only (`BuildDisplayAsync`) |
| `BuildShapeContext`, `BuildDisplayContext`, `FindPlacementDelegate` | DM; no model binding, no updater |
| `IDisplayManager<TModel>` (`BuildDisplayAsync(model, displayType, groupId)`), `DisplayManager`, `BaseDisplayManager` | DM |
| Placement (`Descriptors/PlacementInfo*`, `ShapePlacementStrategy/*`, `IPlacementInfoResolver`, `ShapeTablePlacementProvider`) | DM |
| Shape table (`ShapeTable` with `GetShapeDescriptor`, `ShapeTableBuilder`, `ShapeAlteration(Builder)`, `DefaultShapeTableManager`, `IShapeTableProvider`, `FeatureShapeDescriptor`) | DM. `ShapeBinding` is a binding name, a source and a `ComponentName`; `ShapeAlterationBuilder.Bind(ShapeBinding)` |
| Zones (`IZoneHolding`, `ZoneHolding`, `ZoneOnDemand`, `Zones`, `FlatPositionComparer`, `ZoneAlternatesFactory`, `ZoneShapeAlternates`) | DM |
| Layout (`ILayoutAccessor`, `LayoutAccessor`, `ILayoutTreeContributor`) | DM |
| Theming (`IThemeManager`, `ThemeManager`, `IThemeSelector`, `ThemeSelectorResult`) | DM (theme scope of the registry and templates) |
| `Title/IPageTitleBuilder` | DM |
| `Components/*` (registry manager, tree builder, filter, versions, cache, HTML renderer, template binding seam, coverage) | DM |
| `Components/*` (descriptors, registry, `DisplayNode`, renderer, state, predicates, validator, zone keys) | DM.A |
| `Validation/*` (`IValidationSink`, `ValidationSink`, `ValidationErrors`, the rules) | DM.A, namespace `Grove.Validation` |
| `Document/*` (`IDocumentContributor`, `DocumentContext`, `DocumentSlot`) | DM |

### Tracker: the extraction

- [x] **E1. Renderers out of the engine.** The engine holds no renderer; the Razor and Liquid view
  engines, the C# bindings, the tag helpers, `IHtmlDisplay`/`DisplayHelper`, the MVC model
  updaters and `INotifier` are deleted (E6).
- [x] **E2. `IShape` and `ShapeMetadata` MVC-free.** No `TagBuilder` helper; no `ChildContent`.
- [x] **E3. The validation sink** (§ The validation sink).
- [ ] **E4. The shape contract in DM.A.** The shape types stay in DM: `ShapeMetadata` carries
  the displaying callbacks and the cache context (server types), and nothing in the browser
  takes a shape (it takes `DisplayNode`). Open: whether the item is dropped.
- [x] **E5. Shape table without HTML bindings.** `ShapeBinding` names a component; the
  attribute and template strategies are deleted.
- [x] **E6. Renderers deleted**, with `BlazorComponentShapeBindingResolver`, the fallback seam
  and `ShapeMetadata.ChildContent`.
- [x] **E7. Setup** renders without shapes and without MVC.

## Component coverage

Every shape a live path renders resolves to a component; one that does not becomes an error node
(`DisplayNode.Error`, diagnostic `no-component`, naming the shape type), and is counted in
`DisplayFallbackLog` with the request path. "Live paths" are the site (`api/grove/site/page`),
the admin `/display` and headless display (`api/grove/content-items/{id}/display`), the editor
trees (`…/editor`, built from metadata), designed pages and the audit version view
(`api/grove/audit/content/{id}`).

**The coverage report**, `GET api/grove/components/coverage?shell=&theme=` (admin;
`ComponentCoverage` in DM, served by Grove.Display.Templates' `ComponentsApi`): every shape type
of the shape table (descriptors and bindings without a component) and every type a display tree
met without a component, against the shell's registry; uncovered types first, with their
bindings, their count and the last path. The table below is the hand-kept record of what was
resolved; the report is the live answer.

| Shape | Owner module | Live path | Status |
| --- | --- | --- | --- |
| `TermPart` (a term's page: the items tagged) | Grove.Contents.Taxonomies | site | - [x] `TermPart` (`ListPart`'s display, bound as `TermPart`) |
| `ListPartFeed` (RSS link in the head) | Grove.Contents.Lists | site | - [x] `HeadLink`, bound as `ListPartFeed`: the feed driver puts the link's values on the shape |
| `LiquidPart`, `LiquidPart_Summary` | Grove.Display.Liquid | site | - [x] retired: one body format. Grove.Display.Templates' migration (`Migrations.UpdateFrom3Async`, `LiquidBodies`) gives every type with the part `DisplayTreePart` in its place, converts every version's body (a body rendering one shape by name becomes that component's node; a body without Liquid a `RichText` node; a body with Liquid a published composite template of its own, `LiquidBody_{id}`, placed by name with the item's id as its `ContentItemId` input, `Model.ContentItem` read through the `Content` accessor) and deletes the part's definition |
| `SearchFormPart` | Grove.Search | none | - [x] deleted with its driver (views.md › Search) |
| `PublishLaterPart`, `ArchiveLaterPart` (scheduled-date notes) | Grove.Contents.PublishLater, Grove.Contents.ArchiveLater | `/display` | - [x] `PublishLaterPart`, `ArchiveLaterPart` (the scheduled date as a badge); their `_SummaryAdmin` shapes resolve through the registry's display-variant rule |
| `DashboardPart` and its shapes | Grove.AdminDashboard | — | - [x] deleted with the module |
| `*_SummaryAdmin`, `*_Thumbnail`, `*_Admin` of the MVC admin lists; the deployment modals | Grove.Contents, Grove.Contents.SiteMenu | — | - [x] deleted with the Contents MVC screens and Grove.Themes.TheAdmin |
| `ListPart_RemotePublishing` | Grove.Contents.Lists | — | ☑ deleted with Grove.XmlRpc |
| `TestContentPartA`, `LowerDoll` | Grove.Demo | — | ☑ deleted with Grove.Demo |
| Non-driver shapes rendered into the site layout (menus, pager, layout zones) | Grove.Contents.SiteMenu, Grove.Display.Navigation, theme clients | site | ☑ covered (`Menu`, `MenuItem`, `Pager`, `PagerSlim`, `Layout`, `Content`, `NotFound`, `Forbidden`) |
| `ContentPart`, `ContentPart_Summary` (a part with fields and no driver) | Grove.Contents | site, `/display` | - [x] `ContentPart` (`Grove.Display.Components/Components/Content/ContentPartDisplay.cs`, its `Content` zone) |
| Field, part and content displays (text, HTML, Markdown, numbers, dates, links, pickers, users, media, taxonomy fields, YouTube, geo point, title, bag, list, HTML and Markdown bodies, taxonomy pages, `DisplayTreePart`) | ContentFields, Media, Markdown, Taxonomies, Spatial, Title, Flows, Lists, Html, Templates | site, `/display` | ☑ covered (blazor-display.md step 7) |

### Tracker: no fallback

- [x] **C1. A shape without a component is an error node** naming the shape type, logged in
  `DisplayFallbackLog`; nothing renders it to markup.
- [x] **C2. Coverage report** (`GET api/grove/components/coverage`).
- [x] **C3. No fallback by default**: there is no renderer to fall back to.
- [ ] **C4. Component parity audit.** Every shape template the platform shipped has a Blazor
  component equivalent, not only the shapes on live paths; the audit's rules are in
  [blazor-display.md](../blazor-display.md) › Rework plan, step 7; the tracker is
  [parity.md](parity.md).

## Display drivers and the JSON APIs

A display driver has a display half only: `DisplayDriver<TModel>.Display`/`DisplayAsync`,
`ContentPartDisplayDriver<TPart>.Display`, `ContentFieldDisplayDriver<TField>.Display`. Editors
are trees built from type metadata (`ContentEditorTreeBuilder`) and the registry's editor
components, saved as JSON by the content API. Content definitions are edited through the
definitions API (`ContentDefinitionsApi`); there are no definition editor drivers. A display
driver that reads the request (a pager's page) reads the query string
(`PagerParameters.FromQuery`, `PagerSlimParameters.FromQuery`).

Validation is where every save runs it: the part and field handlers' `ValidatingAsync`
(`ContentPartHandler`, `ContentFieldHandler`), which the content manager runs on every save and
the Blazor editor's server round-trip reports: Text, Numeric, Date, Time, DateTime, Link,
MultiText, Html, YouTube, the user, localization-set and option pickers, Media, GeoPoint,
Markdown (Liquid syntax), Title (required), Taxonomy field (required) and part (term type),
Seo (canonical characters, JSON schema), Alias, Autoroute, HTML body, menu items, contained
items and `DisplayTreePart`.

### The validation sink

`IValidationSink` (`Grove.Display.Management.Abstractions/Validation/IValidationSink.cs`,
namespace `Grove.Validation`): `AddError(string key, string message)`, `ValidationErrors Errors`,
`bool IsValid`. One per request (scoped; `ValidationSink` is the implementation, registered by
the display engine and by Grove.Contents.Management). Keys are field paths (`Part.Field`),
setting names or `""` for the object, kept as the error contract's wire keys (`ValidationErrors`).

- `DefaultContentManager` reports a cancelled publish, unpublish, create or delete into the
  sink.
- The API answers a non-empty sink with `ApiProblems.Validation(sink)` or through the
  `.AnswersValidationSink()` convention (`ApiValidationSinkEndpointFilter`), the error contract's
  400 with `errors` ([api.md](api.md) › The error contract).

- [x] **U1. `IValidationSink`** and its request-scoped implementation.
- [x] **U2. Content manager on the sink.**
- [x] **U3. Editor-only validation in the handlers** (the list above).
- [x] **U4. Display contexts without an updater**: `BuildDisplayAsync(model, displayType, groupId)`
  on `IDisplayManager<T>`, `IContentItemDisplayManager` and `IDisplayTreeBuilder.BuildDisplayAsync<T>`.
- [x] **U5. Editor halves deleted**: `BuildEditorAsync`/`UpdateEditorAsync`, the editor
  contexts, `IUpdateModel`/`IUpdateModelAccessor`, the content definition display manager and
  its drivers.

## Liquid

Liquid is a language, not a view engine.

- **Grove.Display.Liquid.Abstractions**: `ILiquidTemplateManager`, `ILiquidFilter`,
  `LiquidFilterDelegateResolver`, `LiquidContentAccessor`, `LiquidPropertyAccessor`,
  `FluidOptionsParser`, `LiquidTemplateContext` (the services and whether it is initialized; no
  view context) and `LiquidTemplateContextExtensions.InitializeAsync` (the tenant's time zone,
  local now and the current UI culture).
- **Grove.Display.Liquid**: `LiquidParser` (Fluid's tags with the tenant's `Grove_Liquid` parser
  options, no view tags), `LiquidTemplateManager` (the one Liquid every caller uses: Autoroute,
  titles, bodies, workflows through `PlatformLiquidTemplateManager`, search queries),
  `LiquidTemplateOptionsSetup` (the sandbox's step and recursion bounds, the `Culture` value) and
  the filters: `t` (the platform's string localizer), `html_class`, `json`, `jsonparse`, `local`,
  `utc`, `slugify`, `liquid`, `href` and `absolute_url` (over `IShellLinkGenerator`),
  `sanitize_html`, `supported_cultures`, `encrypt`/`decrypt`, `shortcode`.
- **Grove.Display.Templates** has its own parser over the shared `TemplateOptions`, and
  registers the member access a tree template reads shapes with (`Shape`, `ZoneHolding`,
  `ShapeMetadata`, `ShapeAccessor`).
- **No request values**: tenant Liquid has no `Request`, `HttpContext`, `Environment` or
  `TrackingConsent` value (an attack surface); a template reads the route through the
  `Route` context. Filters that need the request (`switch_culture_url`, `display_url`,
  `absolute_url`) read it themselves and expose only their result.

- [x] **L1. One Liquid language project**: the view half (`LiquidPage`, the view parser and its
  tags, the tag-helper bridge, the view filters `shape_new`/`shape_render`/`shape_stringify`/
  `shape_properties`/`append_version`/`resource_url`, the resource and `contentitem` and `a`
  tags) is deleted with `Grove.Display.Management.Liquid`.
- [x] **L2. `LiquidTemplateContext` without `ViewContext`.**
- [x] **L3. Request values out of the shared scope.**
