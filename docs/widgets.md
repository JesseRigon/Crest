# Widget replacements

Widgets were retired on 2026-10-12 (docs/blazor-display.md, rulings, second batch). This is the
list of what the deleted widgets did and what replaces each one in the new system: a registered
Blazor component (`[CrestBlazorComponent]`) that templates place with `{% component %}`, a
composite template built from components, or a layer placement. Keep it current: when a
replacement lands, set its status and name the component.

Status: **done** (a replacement exists and is registered), **partial** (it exists but misses
something the widget did), **missing** (nothing yet), **dropped** (not recreated, with the reason).

## How widgets map to the new system

| Widget mechanism | Replacement | Status |
| --- | --- | --- |
| Layer widgets (a widget content item in a layer's zone) | `LayerPlacement` (layer, zone, component, properties, position) via `/Layers` and the `Layers` recipe step | done |
| `WidgetsListPart` (zones of widgets on one content item) | A `{Type}` template (`Content__{Type}`) places components per type. Per-item placement has no equivalent | partial |
| `FlowPart` / `Container` (a page body composed of widgets) | `Container`/`Stack` in a template, `BagPart` for item-held children. A page whose body is authored per item, without a template, has no editor yet | missing |
| `BlazorComponent` content type (place one component with a properties JSON) | The `component` tag in a template, or a layer placement | done |
| `LiquidWidget` (a Liquid template as a widget) | A `Template` content item, as a composite when it takes inputs | done |
| `Widget.Wrapper` and zone/type alternates | Component variants and overrides (§ 5a) | done |
| Alias/Autoroute widget shape providers | Internal to the old shapes, nothing to recreate | dropped |

## Content widgets (SiteTheme and blog recipes)

| Widget | What it did | Replacement | Status |
| --- | --- | --- | --- |
| Paragraph | Rich text (`HtmlField`) | A placeable rich-text component that renders sanitized HTML. Today only the internal `Markup` leaf does this; it is not registered for templates to place | missing |
| Blockquote | A quote (`TextField`) | `CrestQuote` | done |
| Image (`ImageWidget`) | A media item and a caption | `Image` takes `Path` and `AlternateText`. It has no media-library picker in its schema and no caption; add a caption or a `Figure` composite (Image plus text) | partial |
| RawHtml | Unsanitized HTML | The rich-text component above. Unsanitized HTML and scripts are not recreated (scripting ruling) | dropped |
| Container | Groups widgets (`FlowPart`) | `Container`, `Stack`, `Card` | done |
| LinkMenuItem | A menu link | `MenuItem` inside `Menu` | done |

## Forms (`Crest.Forms`)

Every form element was a widget, and the form posted to a workflow (`FormPart.WorkflowTypeId`)
or to an action URL. The replacement posts to a workflow through the UI-action contract
(`UiActionTrigger` with declared inputs), so the inputs are checked by name and type when the
template is saved and when the form is submitted.

| Widget | What it did | Replacement | Status |
| --- | --- | --- | --- |
| Form | Container. Action, method, enctype, antiforgery, workflow to run, keeps the form location for redirects | A `Form` component: collects its inputs' outputs into one object and, on submit, runs a `run_workflow` action with them as arguments. Antiforgery comes from the action endpoint | missing |
| Label | A label tied to an input | A `Label` parameter on each input component | missing |
| Input | Text, email, number, date, checkbox… with default value and placeholder | `TextInput` (type, name, default, placeholder, required), output `Value` | missing |
| TextArea | Multi-line input | `TextArea` component, output `Value` | missing |
| Select | Options (text and value), default, dropdown or radio | `Select` component with `Options` and `Editor` (dropdown or radio), output `Value` | missing |
| Button | Submit button with text | `Button` with an `on click` → `submit` or `run_workflow` | done |
| Validation | Field error message | Inputs show the errors the run returns for their name (the workflow's Invalid outcome and the contract check) | missing |
| ValidationSummary | All errors of the form | `ValidationSummary` component reading the same errors | missing |
| Visibility rules (`FormInputElementVisibilityPart`) | Show or hide an input by another input's value (is, is not, empty, contains, starts with, greater than…) | A `visible` property bound to another node's output, with a comparison. Needs an expression form for the binding; plain `nodes.X.output` covers only the boolean case | missing |
| ReCaptcha (`Crest.ReCaptcha` Forms feature) | A captcha in a form | A `Captcha` component that adds its token to the form's arguments, checked by a workflow activity or the action endpoint. ReCaptcha's login and registration use is unaffected | missing |

## Other modules

| Widget | What it did | Replacement | Status |
| --- | --- | --- | --- |
| SearchForm (`Crest.Search`) | A search box posting to the search page | A `SearchBox` component that navigates to the search route with the query. `SearchFormPart` still exists in `Crest.Search.Abstractions` and is now unused | missing |
| Facebook Chat, Comments, Continue With, Like, Quote, Save, Share | Facebook SDK embeds, each rendered from a Liquid snippet in the part settings | One `FacebookPlugin` component in `Crest.Facebook` with a `Kind` parameter. It loads the SDK itself (module code, so the scripting ruling allows it). Low priority | missing |

## Not recreated

Test-only widget types (`TestWidgetA`, `TestWidgetB`, `EsModuleTestFieldsWidget`,
`EsModuleTestLiquidWidget`, `SeoMetaPartTestWidget`, `FormsFieldEditorTestWidget`) went with
their tests. The es-module and SEO fixtures keep their non-widget types.

## Order

1. Rich text and the image caption: the sample blog pages lost their bodies and need them back.
2. A per-item page body (the `FlowPart` gap), so an editor can compose one page without a template.
3. Forms: `Form`, `TextInput`, `TextArea`, `Select`, validation, then visibility rules and the captcha.
4. `SearchBox`.
5. Facebook.
