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
| `FlowPart` / `Container` (a page body composed of widgets) | `Container`/`Stack` in a template, `BagPart` for item-held children, and `DisplayTreePart` for a body authored per item (ruling below) | done |
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
| Visibility rules (`FormInputElementVisibilityPart`) | Show or hide an input by another input's value (is, is not, empty, contains, starts with, greater than…) | Every input's `Visible`, bound to a comparison (`visible: nodes.country.value == "US"`, ruling below); a hidden input is neither required nor submitted | done |
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

Left: the per-item page body (2, ruled 2026-10-13, below), visibility rules (ruled
2026-10-13, below), the captcha, Facebook, and a media picker editor for the `media` kind.

**Ruled 2026-10-13: comparisons in bindings.** A binding may compare: `visible:
nodes.country.value == "US"`. The left side is a binding source as today (a node output, a
page variable, the event); the operator is one of a fixed list (`==`, `!=`, `empty`, `not
empty`, `contains`, `starts_with`, `ends_with`, `>`, `<`); the right side is a literal or
another binding source; `and` / `or` join comparisons, `not` negates. Nothing else: no calls,
no filters, no arbitrary expressions - the scripting ruling stands, this is a predicate
grammar over bindings. The template parser stores it as data (`DisplayBinding` with
`Operator`, `Operand`, `Combinator`), `DisplayTreeValidator` checks the operator and the
sources, and `DisplayState` evaluates it in the browser when a source changes. Any
parameter may bind this way, not only `visible`. To build.

**Ruled 2026-10-13: the per-item page body is a `DisplayTreePart`.** The part stores the
item's own display tree - the same node format templates produce (components, properties,
zones, bindings) - as JSON on the item. Rendering goes through the existing tree builder: the
part's tree is mapped into the item's shape zone like any other part, so templates, layer
placements and item bodies are one system, and the node filter, validator and overrides apply
to it unchanged. Editing: the designer canvas scoped to one item once it exists; until then a
JSON editor with `DisplayTreeValidator` validation (components must be registered, properties
must fit the schema, no scripting). A template referenced by the tree runs with the same
limits. Replaces the old flow body; the Contact page is its first use. To build.

**What landed (2026-10-13), both rulings above.**

- **Comparisons in bindings.** `DisplayBinding` carries `Operator`, `Operand`, `Combinator` and
  `Terms` as data; `DisplayPredicates` holds the fixed lists and evaluates. The template parser
  takes a logical expression as a tag argument and `TemplateTreePredicates` reads it off the
  parsed expression without evaluating it: Liquid's `==`, `!=`, `contains`, `startswith`,
  `endswith`, `>`, `<`, `== empty` / `!= empty`, `and`, `or`, and a leading `not` (`>=` and `<=`
  are stored as `not <` and `not >`). A comparison with no binding in it is evaluated by Liquid
  as before. `DisplayState.Read` evaluates a predicate whenever the tree re-renders on a change;
  text that reads as a number compares as one against a number. `DisplayTreeValidator` checks the
  operator, the operand and every source, and the cycle check and the node filter's unlinking
  follow the nested sources. Every form input has `Visible`.
- **`DisplayTreePart`** (Templates module): `Tree` (the node JSON; a node without an id gets one
  from its position, `DisplayTreeJson`), attachable. Its shape binds through
  `DisplayTreePartBinding`, an `IDisplayTemplateBinding`, so the builder fills, filters and
  overrides it like a template's tree: ids and every reference to them are scoped under the
  part's instance id, markup leaves are sanitized, a node naming a published template is
  expanded as a composite under the template limits, and the node budget counts. Saving
  validates it (`DisplayTreePartHandler`: `DisplayTreeValidator.ValidateStored`, which adds the
  schema check of stored properties, zones and required parameters, plus the workflow action
  validators). The editor is `DisplayTreeEditor` (`Edits = ["DisplayTreePart"]`), JSON with the
  same validation as you type. The SiteTheme blog recipe's `Page` type has the part and the
  Contact page's body is rich text and a contact form. The form has no workflow to send to yet:
  the recipe ships no UI-action workflow, so its submit only checks the required inputs.

## Order

1. Rich text and the image caption: the sample blog pages lost their bodies and need them back.
2. A per-item page body: `DisplayTreePart` (ruled above), so an editor can compose one page without a template.
3. Forms: `Form`, `TextInput`, `TextArea`, `Select`, validation, then visibility rules and the captcha.
4. `SearchBox`.
5. Facebook.
