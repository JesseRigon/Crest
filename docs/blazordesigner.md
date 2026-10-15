# Blazor designer (component workbench and WYSIWYG editor)

**Status: not started.** Nothing in this document is implemented.

The designer is the UI layer over the display system in [blazor-display.md](blazor-display.md):
a canvas, a component palette, a property pane, undo and a verification engine. It does not
define a component model, a registry or a node format of its own:

- **The component registry** (blazor-display.md § 1) is the palette's source and the property
  pane's schema. Components are marked with `[BlazorComponent]` and `[ComponentParameter]`;
  there is no workbench registry, workbench attribute or reflection scan of its own.
- **The node is the display tree's** (`DisplayNode`). Layout is a container primitive's
  parameters (stack, grid, flow), never fixed grid coordinates on every node.
- **What it edits is Liquid template text** (blazor-display.md § 11): known tags become
  editable nodes, everything else is preserved verbatim as opaque blocks; preview is a server
  evaluation of the edited text, rendered by the live renderer.
- **The canvas is the real page in an overlay** (blazor-display.md › decision 11): the content
  is rendered unmodified; a designer layer above it draws selection, hover, snap guides and drop
  markers from measured rectangles, and a drop becomes a template edit.
- **Overrides** (blazor-display.md § 5a): the default mode edits an instance; a separate mode
  edits a base node under the shared-component permission. Instance ids are written into the
  tags and never regenerated.
- **Three objects, edited differently.** A *template* (a tree bound to a shape name, no URL), a
  *designed page* (a page record with its route, bucket and permissions plus its tree; no
  overrides, blazor-display.md § 6a and decision 9) and an *item body* (an item's own
  `DisplayTreePart`, blazor-display.md § 7). The content editor is the designer in a reduced
  mode: the same trees, a palette limited by role.
- **Permissions** are the per-page sets of blazor-display.md § 10 (Design, Schema, Data, Shared
  components, Publish). A designer sees the schema without the data unless it has the data
  permission.

Custom data types are the registry's editor components (blazor-display.md › Vocabulary, Editor
kind) over the value types of `Grove.Data.Types`. Types a tenant creates without code reach the
designer through them; tenant-made party roles are the first such case and need their own design
before the designer is done ([parties.md](parties.md) › Tenant-defined party roles).

## Reference systems

Verify each licence before porting anything.

- **[Appsmith](https://github.com/appsmithorg/appsmith)** (Apache-2.0, cloned at
  `/workspaces/appsmith`): the reference architecture for the canvas, palette, property pane and
  IDE shell, ported to Blazor's component model. A `NOTICE` entry credits it once any of these
  pieces exist, even though no literal source is copied.
- **[Blazing Story](https://github.com/jsakamoto/BlazingStory)** (MIT, Blazor): the Storybook
  equivalent, merged in as the **component catalog**: every registry entry gets stories
  (parameter presets) that the palette previews, the property pane uses as examples and the
  verification engine snapshots. Its story model and catalog UI are ported rather than built
  from scratch. They map onto the registry's `templates` (presets).
- **Craft.js and Puck** (MIT, React): the editor's node/resolver/drag-drop architecture and typed
  field schemas; design references only.
- Backend references for the data model are in [blazor-display.md](blazor-display.md).

## What Appsmith gives to port

Paths are under `/workspaces/appsmith/app/client/src/` unless noted.

| Appsmith concept | Location | Blazor target |
|---|---|---|
| Canvas rendering, drop targets, resize handles | `layoutSystems/common/canvasViewer/`, `layoutSystems/common/dropTarget/`, `layoutSystems/common/resizer/` | The overlay layer over the live renderer (decision 11): JS-interop drag-over detection, ghost preview and handle hit-testing with the same behaviour contract; nothing to port but behaviour |
| Selection/highlight overlays | `layoutSystems/common/widgetComponent/`, `sagas/WidgetSelectionSagas.ts` | Overlay boundaries for the hovered and selected instance ids, from designer state (plain Blazor cascading state, no Redux) |
| Property/style pane | widget-specific property-pane config files | The property pane generated from the registry schema (parameter kinds, groups, `hidden`/`advanced`/`readOnly` rules) and the editor components of each kind |
| IDE shell (page list, top bar, canvas, property pane) | `IDE/Components/`, `IDE/Structure/` | The designer shell: palette left, canvas centre, property pane right, modelled on `IDE/Structure`'s regions |
| Widget catalog/toolbox | `pages/Editor` widget sidebar | The palette: registry entries and registered overrides first, grouped by category, any placed component reachable through search, favorites per user; its own layout is a tree overridable per tenant and user |
| Undo/redo | Redux action history in `sagas/` | A command-pattern history of template text edits; new code with the same behaviour contract |

Not ported: Appsmith's query-datasource system (slots bind the query system instead), its
Git-backed app versioning (templates have drafts, publish and history), and the Anvil/auto-layout
reflow engines (layout is container primitives).

## Phase 0: study pass

- [ ] **Read the widget contract.** `widgets/BaseWidget.tsx`, `WidgetProvider/`, and one simple
  widget end to end (`widgets/ButtonWidget/`), to map Appsmith's prop contract onto the registry
  schema.
- [ ] **Scope drag/drop and resize.** Read `layoutSystems/common/dropTarget/` and
  `layoutSystems/common/resizer/` to scope what the overlay's JS interop replicates (drag-over
  detection, ghost preview, resize handle hit-testing) versus pure React state with no DOM
  analog.

## Phase 1: designer state over the template text

- [ ] **Designer state** (scoped service, one per editor session): the template being edited (its
  Liquid text and the parsed statements), the evaluated tree from the last preview, the selected
  instance id, and the undo history.
- [ ] **Parse and write back.** Map the known tags to editable nodes and write a node's changed
  arguments back into the tag; keep unknown statements as opaque blocks.
- [ ] **Preview round trip** through the templates preview endpoint (`api/grove/templates`), the
  canvas rendering the returned tree with `DisplayTreeRenderer`.

## Phase 2: the design studio

- [ ] **The shell**: palette, canvas, property pane.
- [ ] **The palette** from `api/grove/components?shell=` (components, composites, variants).
- [ ] **The overlay**: instance-id markers, measuring, hover and selection boundaries, snapping,
  drop into a zone as a tag move.
- [ ] **The property pane**: one input per parameter kind through the registry's editor
  components, writing back into the tag; slots offer the query picker; outputs offer bindings.
- [ ] **Override modes**: instance (default) and base node, with the permission gate.
- [ ] **Designed page editor** over `api/grove/pages`; **item body editor** replacing the JSON
  `DisplayTreeEditor`.
- [ ] **The Appsmith `NOTICE` entry.**

## Phase 3: headless verification engine

Verification without external web hooks, in the same spirit as the Playwright screenshot-diff
harness under `modules/Grove/tests/playwright/harness/`.

| Dependency | Purpose | License | Tier |
|---|---|---|---|
| AngleSharp | Normalizes structural HTML trees for DOM validation | MIT | Core |
| Puppeteer Sharp | Chromium viewports for pixel snapshotting | MIT | Optional |
| System.Text.Json | Serializes the evaluated tree to clear text | MIT | Core |

```csharp
public class DesignSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string StateTreeJson { get; set; } = string.Empty;
    public string StructuralHtmlHash { get; set; } = string.Empty;
    public byte[]? CapturedScreenshotPng { get; set; }
}
```

- [ ] **Normalize structural HTML with AngleSharp**: strip runtime tokens (Blazor's `_bl_*`
  marker attributes, generated element ids) so the same tree yields the same text signature.
- [ ] **Record snapshots with Puppeteer Sharp** in an isolated background task, with the same
  base/new/`UPDATE_BASE=1` promotion convention as
  `modules/Grove/tests/playwright/harness/screenshot-diff.js`.
- [ ] **Image difference**: reuse `Verify.XunitV3`'s image comparison if it covers the need,
  otherwise a deterministic colour-delta loop (`dE = sqrt((r1-r2)^2 + (g1-g2)^2 + (b1-b2)^2)`).

## Phase 4: pilot

- [ ] **One real component end to end**: a registered component from `Grove.Components` through
  palette → canvas → property pane → snapshot, no synthetic test-only component.

## Verification matrix

- [ ] **State mutation stability.** Changing one parameter in the property pane changes only that
  node's tag; child nodes, instance ids and overrides are untouched.
- [ ] **Structural snapshot precision.** A component with shifting `_bl_*`/GUID attributes yields
  the same normalized HTML across renders.
- [ ] **Pixel contrast.** A visual delta scan between two baselines with a 3px colour boundary
  offset flags the item as failed with a precise error ratio.
