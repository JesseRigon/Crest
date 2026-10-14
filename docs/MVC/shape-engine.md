# The shape engine after MVC

Workstream (b) of the MVC retirement, done together with moving the APIs out of Crest.Server
([server.md](../server.md)). Siblings: [views.md](views.md) (views → Blazor components, workstream
a) and [api.md](api.md) (the API layer, workstream c). The display system's own design is
[blazor-display.md](../blazor-display.md); its step 7 is where this plan picks up.

Status: plan. Nothing here is built yet; every tracked item has a checkbox, an owner
module, its dependencies, a size (S/M/L/XL) and a wave.

## The answer first: what the Blazor display still relies on

**Yes on the shape *model*, no on the shape *templates*, and Liquid only as a language.** The
three things people call "the shape engine" are separate, and only one of them is MVC:

1. **The shape model: shapes, display drivers, placement, alternates, the shape table.** Kept,
   and used by the Blazor display. Every content page, every `/display` and every editor tree
   starts as shapes: drivers return `ShapeResult`s (`Crest.DisplayManagement/Views/ShapeResult.cs`),
   placement puts them in zones, the shape table adds alternates and the displaying/processing
   events, and `DisplayTreeBuilder` (`Crest.DisplayManagement/Components/DisplayTreeBuilder.cs`)
   maps the resulting `IShape` tree once to `DisplayNode`s bound to registered components
   (`Crest.DisplayManagement.Abstractions/Components/ComponentRegistry.cs`). This is the "build
   side" blazor-display.md keeps on purpose. It is **lightly** MVC-coupled, through four seams
   only (below): the model-binding interface on the build context, `IHtmlContent` on the shape
   metadata, a `TagBuilder` helper, and the project references.
2. **The shape template renderers: Razor and Liquid-through-Razor.** Not needed by the Blazor
   display except as a **fallback**: `DisplayTreeBuilder.FallbackAsync`
   (`DisplayTreeBuilder.cs:549`, called at `:201`) renders a shape that has no component through
   `IHtmlDisplay` (`Implementation/DefaultHtmlDisplay.cs`) into a sanitized markup leaf with a
   `markup-fallback` diagnostic. That path runs `RazorShapeTemplateViewEngine`
   (`Razor/RazorShapeTemplateViewEngine.cs`), the `.liquid` view engine (`LiquidPage`,
   `Crest.DisplayManagement.Liquid/LiquidPage.cs:3`, a `RazorPage<dynamic>`), the C# `[Shape]`
   bindings (`Shapes/CoreShapes.cs`, `Zones/ZoneShapes.cs`, …) and the shape tag helpers
   (`TagHelpers/*`). **All of this is MVC** (Razor views, `ViewContext`, `IHtmlHelper`,
   `ActionContext`), and all of it goes once every rendered shape has a component.
3. **Liquid as a language.** Kept, not MVC by nature: tree templates (`TemplateTreeEvaluator`,
   `Crest.Templates/Services/TemplateTreeEvaluator.cs:220`, with its own `TemplateTreeParser`
   at `TemplateTreeTags.cs:30`), designed pages, workflow expressions (through
   the platform's manager, `Crest.Workflows/Server/Expressions/PlatformLiquidTemplateManager.cs`),
   Autoroute patterns (`Crest.Autoroute/Handlers/AutoroutePartHandler.cs:44`), title patterns,
   HTML and Markdown bodies with Liquid on. None of these renders a view. Two places still drag
   MVC in: `LiquidTemplateContext.ViewContext` (`Crest.Liquid.Abstractions/LiquidTemplateContext.cs:17`)
   and the `Request`/`HttpContext` scope values plus the shape/`IHtmlContent` converters in
   `Crest.DisplayManagement.Liquid/TemplateOptionsConfigurations.cs:57-59`.

| Part | Used by the Blazor display | MVC-coupled | Fate |
| --- | --- | --- | --- |
| Shape model, drivers' display halves, placement, alternates, shape table, zones, layout accessor | Yes (the tree builder's input) | Lightly: `IUpdateModel` on `BuildShapeContext` (`Handlers/BuildShapeContext.cs:27`), `IHtmlContent ChildContent` (`Shapes/ShapeMetadata.cs:87`), `TagBuilder` (`IShape.cs:180`), csproj refs to `Crest.Mvc` and `Crest.Mvc.Abstractions` (`Crest.DisplayManagement.csproj:22,28`) | Stays, made MVC-free |
| Component registry, tree builder, filter, renderer, versions | Yes (the display system) | No | Stays |
| Drivers' editor halves (`Edit`, `UpdateAsync`) | No: the Blazor editors are trees built from type metadata (`Crest.Server/Display/ContentEditorTreeBuilder.cs`) | Yes: `TryUpdateModelAsync` on MVC model binding | Die with the MVC editors (§ Editor halves) |
| Razor and Liquid shape template renderers, `[Shape]` C# bindings, tag helpers, `IHtmlDisplay` | Only as the markup fallback | Yes | Deleted when coverage is complete |
| Liquid as a language | Yes (templates, designed pages, workflows, patterns) | Two seams (`ViewContext`, request values) | Stays, its view half deleted |

## How the MVC retirement bears on the shape engine

- **The shape engine is platform, not server**: it stays in
  `Crest.DisplayManagement(.Abstractions)`. Crest.Server only hosts: the display APIs
  (`api/crest/site/page`, `…/display`, `…/editor`, templates, pages, components) are declared by
  the modules that own them and registered in Crest.Server's API registry
  ([architecture.md › Modules declare, Crest.Server registers and serves](../architecture.md#modules-declare-crestserver-registers-and-serves);
  [api.md](api.md) § Placement). This workstream only guarantees the engine they call has no MVC
  types in its signatures. `BlazorComponentShapeBindingResolver`
  (`Crest.Server/Services/BlazorComponentShapeBindingResolver.cs:19`) goes with the renderers it
  plugs into.
- **Validation is the data engine's** (`Crest.Data*` is the validation engine): what lands in
  `ModelState` through `IUpdateModel` today moves to the data engine's validation results
  (§ Editor halves); the shape engine stops carrying it.
- **Shared screens (open, [views.md](views.md) § Shared screens).** Shared screens are
  components; where their *defaults* live only changes **which component the registry resolves
  for a shape name**: (a) the theme client registers `NotFound`, `Forbidden`, error components
  per shell (today `Crest.SiteTheme/wasm/Components/SiteLayout.cs` does), tenants change them
  with a `Content__`-style template or a § 5a variant; (b) a default component ships with Crest,
  themes override by registering the same name later in the catalog order
  (`ComponentRegistryManager` applies catalogs in feature order, themes last). Both work with
  the registry as it is; (a) keeps a theme self-contained, (b) gives every shell one default and
  needs no theme to be complete.
- **Setup (open, [views.md](views.md) § Setup).** Setup renders no shapes through drivers;
  whichever way it goes, the setup screen does not need the shape template renderers. If it
  stays MVC it keeps only plain Razor views (no `IHtmlDisplay`, no shape tag helpers): deleting
  the renderers must not break it. Tracked as E7.

## Target

`Crest.DisplayManagement.Abstractions` (browser-safe, no server framework) and
`Crest.DisplayManagement` (server) hold the engine, with **no MVC type** in either: no
`ViewContext`, `IHtmlHelper`, `ActionContext`, `IUrlHelper`, `TagBuilder`, `ModelStateDictionary`,
`IRazorPage`, no reference to `Crest.Mvc` or `Crest.Mvc.Abstractions`. The template renderers,
the Liquid view engine and the tag helpers are deleted once every rendered shape has a component;
until then they are isolated in one project that nothing else references, so the delete is a
project removal.

### Type map

Destination legend: **DM.A** = `Crest.DisplayManagement.Abstractions`; **DM** =
`Crest.DisplayManagement`; **Renderers** = a temporary `Crest.DisplayManagement.Razor` project
holding what dies, deleted in wave 4; **Liquid** = `Crest.Liquid.Abstractions` / `Crest.Liquid`
(§ Liquid).

| Type (folder) | Today | Decision | Destination | MVC seam to cut |
| --- | --- | --- | --- | --- |
| `IShape`, `Shape`, `Composite`, `AlternatesCollection`, `ShapeMetadata`, `ShapeSerializer` (`IShape.cs`, `Shapes/`) | DM | Keep | DM.A for `IShape`, `ShapeMetadata`, `AlternatesCollection` (the contract the builder and drivers share); DM for the rest | `IShape.GetTagBuilder` (`IShape.cs:180`) → Renderers; `ShapeMetadata.ChildContent` (`IHtmlContent`, `:87`) removed once the fallback is gone (blazor-display.md: "`ShapeMetadata` (minus `ChildContent`)") |
| `IShapeFactory`, `DefaultShapeFactory`, `IShapeFactoryEvents`, `Arguments`, `ShapeFactoryExtensions` | DM | Keep | DM | none |
| `IDisplayDriver`, `DisplayDriver`, `DisplayDriverBase`, `IDisplayResult`, `ShapeResult`, `CombinedResult`, `ShapeViewModel` (`Handlers/`, `Views/`) | DM | Keep (display halves) | DM | `BuildShapeContext.Updater` becomes the new validation sink (§ Editor halves) |
| `BuildDisplayContext`, `BuildEditorContext`, `UpdateEditorContext`, `FindPlacementDelegate` | DM | Keep display; editor contexts shrink with the editor halves | DM | `IUpdateModel` |
| `IDisplayManager`, `DisplayManager`, `BaseDisplayManager` | DM | Keep | DM | `UpdateEditorAsync` path goes with the editor halves |
| Placement (`Descriptors/PlacementInfo*`, `ShapePlacementStrategy/*`, `IPlacementInfoResolver`, `ShapeTablePlacementProvider`) | DM | Keep | DM | `PathPlacementNodeFilterProvider` reads `HttpContext` (`using Microsoft.AspNetCore.Http`): acceptable (not MVC), keep |
| Shape table (`ShapeTable`, `ShapeTableBuilder`, `ShapeAlteration(Builder)`, `DefaultShapeTableManager`, `IShapeTableProvider`, `ShapeTableProvider`, `FeatureShapeDescriptor`) | DM | Keep (descriptors, alternates, displaying/processing events) | DM | `ShapeBinding.BindingAsync` (`Func<DisplayContext, Task<IHtmlContent>>`) → only `ShapeBinding.ComponentName` stays; HTML bindings → Renderers |
| `ShapeAttributeStrategy/*` (C# `[Shape]` methods), `ShapeTemplateStrategy/*` (file harvesting of `.cshtml`/`.liquid`) | DM | Delete | Renderers | the whole strategy |
| `Implementation/IHtmlDisplay`, `DefaultHtmlDisplay`, `DisplayHelper`, `IDisplayHelper`, `PlatformDisplayHelper`, `IShapeDisplayEvents` | DM | Delete (keep `GetShapeDescriptor` as a shape-table helper) | Renderers; the descriptor lookup → `ShapeTable` | the renderer |
| `DisplayContext`, `ShapeDisplayContext` | DM | Keep `ShapeDisplayContext` (the displaying/processing events take it); `DisplayContext` loses `DisplayHelper`/`HtmlFieldPrefix` | DM | `IDisplayHelper` |
| Zones (`IZoneHolding`, `ZoneHolding`, `ZoneOnDemand`, `Zones`, `FlatPositionComparer`, `ZoneAlternatesFactory`) | DM | Keep | DM | none |
| `Zones/ZoneShapes.cs`, `Shapes/CoreShapes.cs`, `DateTimeShapes.cs`, `GroupShapes.cs`, `PageTitleShapes.cs`, `ShapeDebugView.cs` | DM | Delete (HTML bindings); a component where the shape is still rendered (`Zone` is the tree's own) | Renderers | `IHtmlContent`, `TagBuilder` |
| Layout (`ILayoutAccessor`, `LayoutAccessor`, `ILayoutTreeContributor`) | DM | Keep | DM | none |
| Theming (`IThemeManager`, `ThemeManager`, `IThemeSelector`, `ThemeSelectorResult`) | DM | Keep (theme scope of the registry and templates) | DM | none |
| Theming (`ThemeLayout`, `ThemeViewStart`, `ThemingViewsFeatureProvider`), `LocationExpander/*` | DM | Delete | Renderers | Razor view engine |
| `Razor/*`, `RazorPages/Page.cs`, `IAsyncViewActionFilter`, `ViewContextAccessor` | DM | Delete | Renderers | Razor |
| `TagHelpers/*` (19) | DM | Delete | Renderers | tag helpers |
| `Html/*` (`HtmlContentString`, builder extensions, debug info) | DM | Delete | Renderers | `IHtmlContent` |
| `ModelBinding/*` (`ControllerModelUpdater`, `ModelStateWrapperUpdater`, `PageModelUpdater`, `PrefixedModelUpdater`, `NullModelUpdater`, `LocalModelBinderAccessor`, `ModelBinderAccessorFilter`) | DM | Replace by the validation sink; delete the MVC updaters | DM (sink), Renderers (MVC updaters) | `ModelStateDictionary` |
| `Notify/*` (`INotifier`, `Notifier`, `NotifyFilter`) | DM | Move: notifications are a UI concern of the MVC frame (TempData-backed `NotifyFilter`); the Blazor admin shows API results | api.md decides the API-side replacement; `INotifier` callers in drivers go with the editor halves | `NotifyFilter` (MVC result filter) |
| `Entities/SectionDisplayDriver*`, `SiteDisplayDriver` | DM | Keep while settings drivers exist; settings editors are JSON APIs declared by their modules and registered in Crest.Server (api.md) | DM | editor halves |
| `Title/IPageTitleBuilder` | DM | Keep the builder; title rendering is the site page's `PageTitle` | DM | none |
| `Components/*` (registry manager, tree builder, filter, versions, cache, HTML renderer, template binding seam) | DM | Keep | DM | `ComponentShapeRenderer` (renders a component *inside* a Razor fallback) → Renderers |
| `Components/*` in DM.A (descriptors, registry, `DisplayNode`, renderer, state, predicates, validator, zone keys) | DM.A | Keep | DM.A | none |
| `Crest.Mvc.Abstractions/ModelBinding/IUpdateModel`, `IUpdateModelAccessor` | Mvc.A | Replaced by the sink (§ Editor halves); the interface dies with MVC | — | — |
| `Crest.Server/Services/BlazorComponentShapeBindingResolver.cs` | Server | Delete with the renderers (it renders components to HTML for Razor) | — | — |

Today's MVC-coupled files in `Crest.DisplayManagement` (grep for `Microsoft.AspNetCore.Mvc`,
`ViewContext`, `IHtmlHelper`, `ActionContext`, `IUrlHelper`, `RazorPage`, `TagHelper`): 50,
all in the rows marked Delete or Renderers above, plus `IShape.cs` (the `TagBuilder` helper),
`Extensions/HttpContextExtensions.cs`, `Extensions/ServiceCollectionExtensions.cs` and
`PlatformBuilderExtensions.cs` (the registrations of the renderers).

### Tracker: the extraction

- [ ] **E1. Isolate the renderers.** Move every Delete/Renderers row into a new
  `Crest.DisplayManagement.Razor` project referenced only by `Crest.Mvc`-hosting code and by the
  fallback (through `IShapeHtmlFallback`, a one-method interface in DM the builder calls). DM
  drops its `Crest.Mvc` and `Crest.Mvc.Abstractions` references except `IUpdateModel` (E3).
  Owner: Crest.DisplayManagement. Depends on: nothing. Size: L. Wave 1.
- [ ] **E2. `IShape` and `ShapeMetadata` MVC-free.** `GetTagBuilder` to Renderers; `ChildContent`
  kept behind the fallback until E6, then removed. Owner: Crest.DisplayManagement. Depends on
  E1. Size: S. Wave 1.
- [ ] **E3. The validation sink.** § Editor halves. Owner: Crest.DisplayManagement with
  Crest.Data (the validation engine). Depends on api.md's editor-API cut-over. Size: M. Wave 3.
- [ ] **E4. Move the shared contract to DM.A.** `IShape`, `ShapeMetadata`, `AlternatesCollection`
  (by hand, per file: the memory rule on Abstractions moves). Owner: Crest.DisplayManagement.
  Depends on E2. Size: S. Wave 3.
- [ ] **E5. Shape table without HTML bindings.** `ShapeBinding` keeps `ComponentName` only;
  the attribute and template strategies move to Renderers. Owner: Crest.DisplayManagement.
  Depends on E1. Size: M. Wave 1.
- [ ] **E6. Delete the renderers.** Remove `Crest.DisplayManagement.Razor`,
  `Crest.DisplayManagement.Liquid`'s view half (§ Liquid), `BlazorComponentShapeBindingResolver`,
  the fallback seam and `ShapeMetadata.ChildContent`. Owner: Crest.DisplayManagement. Depends on
  the coverage tracker reaching zero on every live path, views.md's MVC screens gone, views.md
  § Setup answered. Size: L. Wave 4.
- [ ] **E7. Setup check.** Whichever way views.md § Setup goes, setup renders without the shape renderers
  (plain Razor or a pre-database page). Owner: Crest.Setup. Depends on views.md § Setup. Size: S. Wave 4.

## Component coverage

Every shape a live path renders must resolve to a component; a shape without one falls back to
the renderers today. "Live paths" are the site (`api/crest/site/page`), the admin `/display`
and headless display (`api/crest/content-items/{id}/display`), the editor trees
(`…/editor`, built from metadata, so they never fall back), designed pages and the audit
version view (`api/crest/audit/content/{id}`). Starting list: blazor-display.md step 7's
fallback list, re-checked by scanning every content part, field and content display
driver's `Display` shapes against the registered component names (94 registered; the registry's
display-variant rule counted). The runtime list comes from the `markup-fallback` diagnostics
(`DisplayTreeBuilder.cs:573`), which the flag below makes measurable.

| Shape | Owner module | Live path | Size | Wave | Status |
| --- | --- | --- | --- | --- | --- |
| `TermPart` (a term's page: the items tagged) | Crest.Taxonomies | site | M (items + pager, like `ListPart`) | 1 | - [ ] |
| `ListPartFeed` (RSS link in the head) | Crest.Lists | site | S (a head-link component or a document-concern on the site page) | 1 | - [ ] |
| `LiquidPart`, `LiquidPart_Summary` | Crest.Liquid | site | — | — | - [ ] decide: by design tenant Liquid rendered as HTML (blazor-display.md step 7). Options: keep a *server-side* markup leaf rendered by the Liquid manager (no Razor, no `IHtmlDisplay`), or migrate `LiquidPart` bodies to `DisplayTreePart` and retire the part. The first keeps working pages; the second has one body format. Not decided here. |
| `SearchFormPart` | Crest.Search | none (unused, widgets.md) | S (delete the part and driver) | 1 | - [ ] |
| `PublishLaterPart`, `ArchiveLaterPart` (scheduled-date notes) | Crest.PublishLater, Crest.ArchiveLater | `/display` (Detail shows them to editors) | S each | 2 | - [ ] |
| `DashboardPart`, `Dashboard_*Button`, `Dashboard_DragHandle`, `DashboardWidget_DetailAdmin__ContentsTags` | Crest.AdminDashboard | MVC dashboard only | — | 4 (with views.md's dashboard port) | - [ ] |
| `*_SummaryAdmin` (`LocalizationPart`, `ListPart`, `AuditTrailContentsAction`), `*_Thumbnail`, `*_Admin` (menu item parts), `ListPartHeaderAdmin`, `ListPartNavigationAdmin`, `ListPartDetailAdminSearchPanel` | Crest.ContentLocalization, Crest.Lists, Crest.Contents, Crest.Menu | MVC admin lists only | — | 4 (go with TheAdmin, views.md) | - [ ] |
| `AddToDeploymentPlan_Modal__ActionDeploymentPlan`, `ExportContentToDeploymentTarget_Modal__ActionDeploymentTarget` | Crest.Contents | MVC admin lists only | — | 4 | - [ ] |
| `ListPart_RemotePublishing` | Crest.Lists | MetaWeblog discovery (head) | S | 2 | - [ ] |
| `TestContentPartA`, `LowerDoll` | Crest.Demo | Demo module | — | 4 (Demo goes or ports) | - [ ] |
| Non-driver shapes rendered into the site layout (menus, pager, layout zones) | Crest.Menu, Crest.Navigation, theme clients | site | — | — | ☑ covered (`Menu`, `MenuItem`, `Pager`, `PagerSlim`, `Layout`, `Content`, `NotFound`, `Forbidden`) |
| Field, part and content displays (text, HTML, Markdown, numbers, dates, links, pickers, users, media, taxonomy fields, YouTube, geo point, title, bag, list, HTML and Markdown bodies, taxonomy pages, `DisplayTreePart`) | ContentFields, Media, Markdown, Taxonomies, Spatial, Title, Flows, Lists, Html, Templates | site, `/display` | — | — | ☑ covered (blazor-display.md step 7) |

### Tracker: the fallback as an error

- [ ] **C1. `DisplayTreeOptions.Fallback`** = `Render` (today), `Diagnose` (render, and log
  each `markup-fallback` with the shape type and the path, counted per type), `Refuse` (the
  node becomes a `DisplayError` node with the shape type; no `IHtmlDisplay` call). Default
  `Render` in production, `Diagnose` in Development, `Refuse` in the functional test host so a
  new driver without a component fails a test. Owner: Crest.DisplayManagement. Depends on
  nothing. Size: S. Wave 1.
- [ ] **C2. Coverage report.** `GET api/crest/components/coverage` (admin): every shape type the
  enabled drivers can produce (from the drivers' declared shape names and the shape table)
  against the registry, so the table above is generated, not hand-kept. Owner: the display
  system (declared with DM, registered in Crest.Server's API registry). Depends on C1. Size: M. Wave 2.
- [ ] **C3. Flip the default to `Refuse`** once C2 shows zero uncovered shapes on the live
  paths. Owner: Crest.DisplayManagement. Depends on the coverage rows of waves 1-2. Size: S.
  Wave 3.
- [ ] **C4. Component parity audit.** Every shape template (the 597 driver-shape templates and
  the 261 `Views/Items/*`, [views.md](views.md) › The inventory) has a Blazor component
  equivalent, not only the shapes on live paths; the audit's rules are in
  [blazor-display.md](../blazor-display.md) › Rework plan, step 7; the tracker is
  [parity.md](parity.md). Owner: the module of each
  shape. Depends on C2. Size: L. Wave 3.

## Editor halves vs the JSON APIs

255 display drivers; 162 override `Display`, 206 `Edit`, 168 `UpdateAsync` (scan of `src/`).
The Blazor editors do not call them: `ContentEditorTreeBuilder` builds the editor tree from the
type definition and the registry's editor components, and the content API saves the JSON. The
callers of `UpdateEditorAsync` are all MVC controllers and Razor Pages: Contents, AdminMenu,
AuditTrail options, ContentPreview, Deployment steps, Queries, RateLimits, Settings, Sitemaps,
Taxonomies, UrlRewriting, Users (account, registration, reset password), Demo.

What dies with the MVC editors (each with the MVC screen views.md retires, in the same change):

- every driver's `Edit` and `UpdateAsync` whose only caller is an MVC editor, with its edit
  views (`*.Edit.cshtml`), its edit view models and its `INotifier` messages;
- `BuildEditorContext`/`UpdateEditorContext`'s HTML prefix plumbing and the MVC model updaters;
- the classic pickers' search controllers (the user picker's is already replaced by
  `api/crest/content-fields/users`).

What survives in a driver's editor half: **validation**. Today some rules live only in
`UpdateAsync` (required checks, length, regex, picker cardinality) and land in `ModelState`.
They move to where the JSON API already validates: the part and field handlers'
`ValidatingAsync` (`ContentPartHandler`), which the content manager runs on every save, and which
the Blazor editor's server round-trip reports. Coordinate the field list with [api.md](api.md)
(the content and settings APIs own the request/response shape).

**What `IUpdateModel` becomes.** A ModelState-only sink, then nothing:

1. `IValidationSink` in `Crest.Data.Abstractions` (Crest.Data is the validation engine): `AddError(string key, string message)`, `Errors`, `IsValid`. No model binding.
2. `DefaultContentManager` (`Crest.ContentManagement/DefaultContentManager.cs:408, 412, 480, 484,
   640, 644, 955, 959`) reports cancelled publish/unpublish/create/delete into the sink instead of
   `ModelUpdater.ModelState`; the content API returns the sink's errors as its problem details.
3. `BuildShapeContext.Updater` (`Handlers/BuildShapeContext.cs:27`) is removed from display
   contexts (a display never binds); editor contexts keep the sink until their halves are gone.
4. `IUpdateModel`/`IUpdateModelAccessor` (`Crest.Mvc.Abstractions/ModelBinding/IUpdateModel.cs`)
   are deleted with `Crest.Mvc.Abstractions`.

- [ ] **U1. `IValidationSink`** and its request-scoped implementation; the MVC
  `IUpdateModelAccessor` adapts to it while both exist. Owner: Crest.Data.Abstractions. Depends
  on api.md's error contract. Size: S. Wave 2.
- [ ] **U2. Content manager on the sink.** Owner: Crest.ContentManagement. Depends on U1.
  Size: S. Wave 2.
- [ ] **U3. Port editor-only validation to handlers.** Per field and part, the rules in
  `UpdateAsync` that `ValidatingAsync` lacks. Owner: each module (ContentFields first: Text,
  Numeric, Link, pickers). Depends on U1. Size: L. Wave 3.
- [ ] **U4. Display contexts without an updater.** Owner: Crest.DisplayManagement. Depends on
  U2. Size: M (every `BuildDisplayAsync` caller passes one today). Wave 3.
- [ ] **U5. Delete editor halves** module by module, in the change that deletes the module's MVC
  editor (views.md). Owner: each module. Depends on views.md. Size: XL overall. Waves 3-4.

## Liquid without `LiquidPage`

`Crest.DisplayManagement.Liquid` is two things in one project: the **view engine** (dies) and the
**language configuration** the templates, workflows and patterns use (lives).

| Part | Files | Fate |
| --- | --- | --- |
| View engine: `.liquid` views as Razor pages | `LiquidPage.cs`, `LiquidViewTemplate.cs`, `LiquidViewParser.cs`, `LiquidViewOptions*.cs`, `LiquidViewsFeatureProvider.cs`, `LiquidViewExtensionProvider.cs`, `LiquidShapeTemplateOptionsSetup.cs`, `ModuleProjectLiquidFileProvider.cs`, `TemplateOptionsFileProviderSetup.cs`, `ViewBufferTextWriterContent.cs` | Delete (wave 4, with E6) |
| View tags | `Tags/*` (`shape`, `zone`, `layout`, `render_body`, `render_section`, alternates/classes/wrappers/attributes, `antiforgerytoken`, `a`, `cache*`, `page_title`, `httpcontext_*`) | Delete; the tree templates have their own tags (`TemplateTreeParser`) |
| Tag-helper bridge | `TagHelpers/*`, `Tags/FluidTagHelper.cs` | Delete |
| View filters | `shape_new`, `shape_render`, `shape_stringify` (`Filters/NewShapeFilter.cs`, `ShapeRenderFilter.cs`, `ShapeStringifyFilter.cs`), `LiquidViewFilters.cs`, `append_version`, `resource_url` | Delete (security audit item 3 already lists `shape_render`/`shape_stringify` as not for tenant templates) |
| Language configuration | `TemplateOptionsConfigurations.cs` (steps and recursion bounds, member access, value converters), `Filters/SanitizeHtmlFilter.cs`, `Filters/SupportedCulturesFilter.cs`, `Values/CultureValue.cs` | Keep, move to `Crest.Liquid` (it registers the rest of the filters already, `Crest.Liquid/Startup.cs:72-82`) |
| Request values | `Values/HttpRequestValue.cs`, `HttpContextValue.cs`, `HeaderDictionaryWrapper.cs`, `CookieCollectionWrapper.cs`, `RouteValueDictionaryWrapper.cs`, `HttpContextItemsWrapper.cs`, `StringValuesValue.cs`, `TrackingConsentValue.cs`, `HostingEnvironmentValue.cs` | Decide with the security audit (item 3: `Request` is not for tenant templates). Proposal: drop `Request`/`HttpContext` from the shared scope; a template reads the route through the `Route` context of decision 4 |
| Shape converters | the `Shape`/`IHtmlContent` value converters and `ShapeAccessor` | Delete with the renderers; tree templates see shapes as `Model` through the member access they register |

And in the other Liquid projects:

- `Crest.Liquid.Abstractions`: keep `ILiquidTemplateManager`, `ILiquidFilter`,
  `LiquidFilterDelegateResolver`, `LiquidContentAccessor`, `LiquidPropertyAccessor`,
  `FluidOptionsParser`; `LiquidTemplateContext.ViewContext` (`:17`) is removed (only the view
  engine reads it).
- `Crest.Liquid`: keeps `LiquidTemplateManager` (the one Liquid every caller uses: Autoroute,
  titles, bodies, workflows through `PlatformLiquidTemplateManager`, search queries) and the
  content filters (`slugify`, `json`, `local`/`utc`, `href`, `absolute_url`, `liquid`,
  `encrypt`/`decrypt` pending the audit); `Razor/LiquidRazorHelperExtensions.cs` goes with Razor;
  `LiquidPart` per the coverage table's open row.
- `Crest.Templates`: keeps its own parser and evaluator over the shared `TemplateOptions`; no
  change beyond the scope values above.

- [ ] **L1. Split `Crest.DisplayManagement.Liquid`**: language configuration to `Crest.Liquid`,
  the rest into the Renderers project. Owner: Crest.Liquid. Depends on E1. Size: M. Wave 2.
- [ ] **L2. `LiquidTemplateContext` without `ViewContext`.** Owner: Crest.Liquid.Abstractions.
  Depends on L1 and E6 (the view engine is its only reader). Size: S. Wave 4.
- [ ] **L3. Request values out of the shared scope** (with the security audit's ruling on item
  3). Owner: Crest.Liquid. Depends on that ruling. Size: S. Wave 3.

## Waves

| Wave | Contents | Gate |
| --- | --- | --- |
| 1 | E1, E2, E5, C1; coverage rows `TermPart`, `ListPartFeed`, `SearchFormPart` | compiles; fallback measurable |
| 2 | U1, U2, L1, C2; rows `PublishLaterPart`, `ArchiveLaterPart`, `ListPart_RemotePublishing` | coverage report shows only MVC-admin shapes uncovered |
| 3 | E3, E4, U3, U4, L3, C3, C4 | `Refuse` is the default; no display context carries an updater |
| 4 | E6, E7, L2, U5 (rest); admin-only rows go with views.md's screens | `Crest.Mvc*` references gone from DM and Liquid; renderers deleted |

Cross-workstream dependencies: wave 4 needs [views.md](views.md)'s MVC screens retired (the
admin lists, the dashboard, the Contents MVC create/edit actions, the Demo module) and
[api.md](api.md)'s error contract (U1) and content/settings editor APIs (U3).
