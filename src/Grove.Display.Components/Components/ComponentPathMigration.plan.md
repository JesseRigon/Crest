# Component path migration plan

Status: applied. This file is the path-change source of truth for the completed migration.

## Second-pass domain colocation

Applied: the initial pass used exact component-name matching. This follow-up moves coherent
component-family code that has a different filename into the component family that
owns its behavior, while leaving cross-cutting infrastructure in `Utilities/Grove`.

| Utility family | Destination component family |
|---|---|
| `DataGrid*.cs` | `Components/Primitives/GroveDataGrid/` |
| chart/axis/series/scale/legend/marker types | `Components/Primitives/GroveChart/` |
| `Scheduler*.cs`, `IScheduler*.cs`, appointment/date/month types | `Components/Primitives/GroveScheduler/` |
| `Gantt*.cs` | `Components/Primitives/GroveGantt/` |
| `HtmlEditor*.cs` | `Components/Primitives/GroveHtmlEditor/` |
| `Tree*.cs` | `Components/Primitives/GroveTree/` |
| `Upload*.cs`, preview-file types | `Components/Primitives/GroveUpload/` |
| form, validation, and dropdown-base types | `Components/Primitives/GroveTemplateForm/` |
| dialog, tooltip, notification, context-menu services | their corresponding component folders |
| AI chat, Google Map, pager, pick-list, tabs, and steps types | their corresponding component folders |
| component option enums, rendering modes, and event models | their corresponding component folders |

`GroveComponent`, `GroveComponentWithChildren`, component activation, generic
query/expression utilities, shared collection models, JS helpers, and dependency
registration remain in `Utilities/Grove` because they serve multiple component
families.

## Rules

1. For every `Components/**/<Component>.razor` (except `_Imports.razor`), create a sibling `<Component>/` folder.
2. Move that component's `.razor`, `.razor.cs`, and `.razor.css` companions into its folder. Components with code-behind but no Razor markup receive the same folder treatment.
3. Move sibling `Components/**/<Component>*.cs` support files into the same component folder, using the longest matching component name. This includes component-specific models, events, and bases.
4. Move `Utilities/Grove/<Component>*.cs` into the same component folder when a Grove component root exists. General-purpose shared utilities remain in `Utilities/Grove`.
5. Preserve namespaces. Because a Razor component folder is otherwise appended to its generated namespace, add an explicit `@namespace` directive to each moved primitive Razor component, set to its pre-move namespace. Place that directive after any leading Razor directives (`@inherits`, `@implements`, `@typeparam`, etc.) so their required ordering is retained. Existing explicit `@namespace` directives in Grove-authored components remain authoritative.
6. Do not move or overwrite the isolated duplicate implementations in `RadzenSource`.

## Path transformations

| Source | Destination | Scope |
|---|---|---|
| `Components/<Area>/<Component>.razor` | `Components/<Area>/<Component>/<Component>.razor` | every Razor component except imports |
| `Components/<Area>/<Component>.razor.cs` | `Components/<Area>/<Component>/<Component>.razor.cs` | companion code-behind where present |
| `Components/<Area>/<Component>.razor.css` | `Components/<Area>/<Component>/<Component>.razor.css` | scoped companion CSS where present |
| `Components/<Area>/<Component>*.cs` | `Components/<Area>/<Component>/<same filename>` | sibling, longest-prefix component support files |
| `Utilities/Grove/<Component>*.cs` | `Components/<Area>/<Component>/<same filename>` | only Grove-prefixed component support files listed below |
| `Components/Primitives/**/<Component>/<Component>.razor` | add explicit `@namespace` | preserves the namespace that existed before the folder was introduced |

## Matched utility files to colocate

```text
Utilities/Grove/GroveAccordionItem.cs -> Components/Primitives/GroveAccordion/GroveAccordionItem.cs
Utilities/Grove/GroveBarcodeEncoder.cs -> Components/Primitives/GroveBarcode/GroveBarcodeEncoder.cs
Utilities/Grove/GroveChartComponentBase.cs -> Components/Primitives/GroveChart/GroveChartComponentBase.cs
Utilities/Grove/GroveChartRangeNavigator.cs -> Components/Primitives/GroveChart/GroveChartRangeNavigator.cs
Utilities/Grove/GroveChartTooltipOptions.cs -> Components/Primitives/GroveChartTooltip/GroveChartTooltipOptions.cs
Utilities/Grove/GroveCheckBoxListItem.cs -> Components/Primitives/GroveCheckBoxList/GroveCheckBoxListItem.cs
Utilities/Grove/GroveColumnOptions.cs -> Components/Primitives/GroveColumn/GroveColumnOptions.cs
Utilities/Grove/GroveDataFilterProperty.cs -> Components/Primitives/GroveDataFilter/GroveDataFilterProperty.cs
Utilities/Grove/GroveDropZoneItemEventArgs.cs -> Components/Primitives/GroveDropZoneItem/GroveDropZoneItemEventArgs.cs
Utilities/Grove/GroveDropZoneItemRenderEventArgs.cs -> Components/Primitives/GroveDropZoneItem/GroveDropZoneItemRenderEventArgs.cs
Utilities/Grove/GroveGoogleMapMarker.cs -> Components/Primitives/GroveGoogleMap/GroveGoogleMapMarker.cs
Utilities/Grove/GroveHtmlEditorButtonBase.cs -> Components/Primitives/GroveHtmlEditor/GroveHtmlEditorButtonBase.cs
Utilities/Grove/GroveHtmlEditorColorBase.cs -> Components/Primitives/GroveHtmlEditorColor/GroveHtmlEditorColorBase.cs
Utilities/Grove/GroveHtmlEditorCommandState.cs -> Components/Primitives/GroveHtmlEditor/GroveHtmlEditorCommandState.cs
Utilities/Grove/GroveLayout.cs -> Components/Primitives/GroveLayout/GroveLayout.cs
Utilities/Grove/GroveNumericRangeValidator.cs -> Components/Primitives/GroveNumeric/GroveNumericRangeValidator.cs
Utilities/Grove/GroveRadioButtonListItem.cs -> Components/Primitives/GroveRadioButtonList/GroveRadioButtonListItem.cs
Utilities/Grove/GroveSSRSViewerParameter.cs -> Components/Primitives/GroveSSRSViewer/GroveSSRSViewerParameter.cs
Utilities/Grove/GroveSelectBarItem.cs -> Components/Primitives/GroveSelectBar/GroveSelectBarItem.cs
Utilities/Grove/GroveSplitterEventArgs.cs -> Components/Primitives/GroveSplitter/GroveSplitterEventArgs.cs
Utilities/Grove/GroveSplitterResizeEventArgs.cs -> Components/Primitives/GroveSplitter/GroveSplitterResizeEventArgs.cs
Utilities/Grove/GroveStepsItem.cs -> Components/Primitives/GroveSteps/GroveStepsItem.cs
Utilities/Grove/GroveTreeLevel.cs -> Components/Primitives/GroveTree/GroveTreeLevel.cs
```

## Explicit exclusions

- `Components/Primitives/_Imports.razor` remains at the primitive root.
- `Utilities/Grove` files with no component-root prefix match remain shared utilities.
- `RadzenSource/` remains the isolated duplicate/reference area.
