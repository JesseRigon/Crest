# Widget replacements

Widgets were retired on 2026-10-12 (docs/blazor-display.md, rulings, second batch). This is the
list of what the deleted widgets did and what replaces each one in the new system: a registered
Blazor component (`[BlazorComponent]`) that templates place with `{% component %}`, a
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
| Paragraph | Rich text (`HtmlField`) | `RichText` (`Html`, a `markup` parameter): the tree filter sanitizes markup parameters before a tree leaves the server, and the renderer never feeds a browser-bound value into one | done |
| Blockquote | A quote (`TextField`) | `CrestQuote` | done |
| Image (`ImageWidget`) | A media item and a caption | `Figure` (`Path`, `AlternateText`, `Caption`); its `Path` declares the `media` editor kind. No media picker editor component exists yet to serve that kind | partial |
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
| Form | Container. Action, method, enctype, antiforgery, workflow to run, keeps the form location for redirects | `Form`: its inputs register through a cascaded `FormContext`; on submit it checks required inputs and publishes the values object as its `Submitted` output, which a template's `{% on "submitted" %}{% run_workflow "id", email: event.email %}{% endon %}` sends. Antiforgery comes from the action endpoint | done |
| Label | A label tied to an input | The `Label` parameter of every input (`FormInputBase`) | done |
| Input | Text, email, number, date, checkbox… with default value and placeholder | `TextInput` (type, name, default, placeholder, required), output `Value`; a number submits as a number, a checkbox as a boolean | done |
| TextArea | Multi-line input | `TextArea`, output `Value` | done |
| Select | Options (text and value), default, dropdown or radio | `Select` with `Options` (`{ text, value }` objects or strings) and `Editor` (`Dropdown` or `Radio`), output `Value` | done |
| Button | Submit button with text | `Button` with an `on click` → `submit` or `run_workflow`; inside a `Form`, `button_type: "Submit"` submits the form | done |
| Validation | Field error message | Inputs show the errors the run returns for their name: the run endpoint answers a refused contract check with `errors` by input (`UiActionContract.CheckByInput`), the dispatcher raises `DisplayActionException`, and the runner keeps them in `DisplayState` under the form's node (`InstanceId`, filled by the renderer) | done |
| ValidationSummary | All errors of the form | `ValidationSummary`, reading the same errors | done |
| Visibility rules (`FormInputElementVisibilityPart`) | Show or hide an input by another input's value (is, is not, empty, contains, starts with, greater than…) | A `visible` property bound to another node's output, with a comparison. Needs an expression form for the binding; plain `nodes.X.output` covers only the boolean case | missing |
| ReCaptcha (`Crest.ReCaptcha` Forms feature) | A captcha in a form | A `Captcha` component that adds its token to the form's arguments, checked by a workflow activity or the action endpoint. ReCaptcha's login and registration use is unaffected | missing |

## Other modules

| Widget | What it did | Replacement | Status |
| --- | --- | --- | --- |
| SearchForm (`Crest.Search`) | A search box posting to the search page | `SearchBox`: a GET form to `search/{index}?Terms=`, so it works before the page is interactive. `SearchFormPart` still exists in `Crest.Search.Abstractions` and is now unused | done |
| Facebook Chat, Comments, Continue With, Like, Quote, Save, Share | Facebook SDK embeds, each rendered from a Liquid snippet in the part settings | One `FacebookPlugin` component in `Crest.Facebook` with a `Kind` parameter. It loads the SDK itself (module code, so the scripting ruling allows it). Low priority | missing |

## Not recreated

Test-only widget types (`TestWidgetA`, `TestWidgetB`, `EsModuleTestFieldsWidget`,
`EsModuleTestLiquidWidget`, `SeoMetaPartTestWidget`, `FormsFieldEditorTestWidget`) went with
their tests. The es-module and SEO fixtures keep their non-widget types.

## What landed (2026-10-13)

`RichText`, `Figure`, `Form`, `TextInput`, `TextArea`, `Select`, `ValidationSummary` and
`SearchBox` in `Crest.Components` (`Components/Content`, `Components/Forms`), styled in
`CrestComponents.css`; the `markup` and `media` editor kinds (`ComponentSchema.Editors`); markup
sanitized by `DisplayTreeFilter`; refused arguments returned by input name. The SiteTheme blog
recipe now ships `Content__Article`, `Content__BlogPost` and `Content__Blog` templates, so the
sample pages render again under decision 10. The Contact page is still empty: its body needs the
per-item page body (2 below) and a contact workflow with a UI action trigger.

Left: the per-item page body (2: how an item stores its own tree is not ruled; the stripped
designer and security audit item 2 bear on it), visibility rules (they need an expression form
for the binding), the captcha, Facebook, and a media picker editor for the `media` kind.

## Order

1. Rich text and the image caption: the sample blog pages lost their bodies and need them back.
2. A per-item page body (the `FlowPart` gap), so an editor can compose one page without a template.
3. Forms: `Form`, `TextInput`, `TextArea`, `Select`, validation, then visibility rules and the captcha.
4. `SearchBox`.
5. Facebook.
