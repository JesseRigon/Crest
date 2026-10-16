# Widget replacements

Widgets are not part of the display system ([blazor-display.md](blazor-display.md) § 6): a
widget was a content item placed into a zone, and components placed into zone keys do that now.
This is the list of what the deleted widgets did and what replaces each one: a registered Blazor
component (`[BlazorComponent]`) that templates place with `{% component %}`, a composite template
built from components, an item's own tree (`DisplayTreeFieldSet`), or a layer placement. Keep it
current: when a replacement exists, set its status and name the component. The content and form
components live in `Grove.Display.Components` (`Components/Content`, `Components/Forms`), styled in
`GroveComponents.css`.

Status: **done** (a replacement exists and is registered), **partial** (it exists but misses
something the widget did), **missing** (nothing yet), **dropped** (not recreated, with the reason).

## How widgets map to the new system

| Widget mechanism | Replacement | Status |
| --- | --- | --- |
| Layer widgets (a widget content item in a layer's zone) | `LayerPlacement` (layer, zone, component, properties, position) via `/Layers` and the `Layers` recipe step | done |
| `WidgetsListPart` (zones of widgets on one content item) | A `{Type}` template (`Content__{Type}`) places components per type. Per-item placement has no equivalent | partial |
| `FlowPart` / `Container` (a page body composed of widgets) | `Container`/`Stack` in a template, an embedding for item-held children, and `DisplayTreeFieldSet` for a body authored per item ([blazor-display.md](blazor-display.md) § 7) | done |
| `BlazorComponent` content type (place one component with a properties JSON) | The `component` tag in a template, or a layer placement | done |
| `LiquidWidget` (a Liquid template as a widget) | A `Template` content item, as a composite when it takes inputs | done |
| `Widget.Wrapper` and zone/type alternates | Component variants and overrides (§ 5a) | done |
| Alias/Autoroute widget shape providers | Internal to the old shapes, nothing to recreate | dropped |

## Content widgets (SiteTheme and blog recipes)

| Widget | What it did | Replacement | Status |
| --- | --- | --- | --- |
| Paragraph | Rich text (`HtmlField`) | `RichText` (`Html`, a `markup` parameter): the tree filter sanitizes markup parameters before a tree leaves the server, and the renderer never feeds a browser-bound value into one | done |
| Blockquote | A quote (`TextField`) | `GroveQuote` | done |
| Image (`ImageWidget`) | A media item and a caption | `Figure` (`Path`, `AlternateText`, `Caption`); its `Path` is a `media` property, edited by `MediaPicker` (a file from any drive, stored as its placement id) and rendered as `files/{id}` | done |
| RawHtml | Unsanitized HTML | The rich-text component above. Unsanitized HTML and scripts are not recreated: templates are data, never code (blazor-display.md § 10) | dropped |
| Container | Groups widgets (`FlowPart`) | `Container`, `Stack`, `Card` | done |
| LinkMenuItem | A menu link | `MenuItem` inside `Menu` | done |

## Forms (`Grove.Forms`)

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
| Visibility rules (`FormInputElementVisibilityPart`) | Show or hide an input by another input's value (is, is not, empty, contains, starts with, greater than…) | Every input's `Visible`, bound to a comparison (`visible: nodes.country.value == "US"`, blazor-display.md § 4a); a hidden input is neither required nor submitted | done |
| ReCaptcha (`Grove.Security.ReCaptcha` Forms feature) | A captcha in a form | `Captcha` (a form input, required, named `captcha` by default) renders the widget with the site key from `api/grove/recaptcha` and submits its token; the workflow checks it with the `Verify captcha` activity, through the same `ReCaptchaService` login and registration use | done |

## Other modules

| Widget | What it did | Replacement | Status |
| --- | --- | --- | --- |
| SearchForm (`Grove.Search`) | A search box posting to the search page | `SearchBox`: a GET form to `search/{index}?Terms=`, so it works before the page is interactive. `SearchFormPart` still exists in `Grove.Search.Abstractions`, unused | done |
| Facebook Chat, Comments, Continue With, Like, Quote, Save, Share | Facebook SDK embeds, each rendered from a Liquid snippet in the part settings | `FacebookPlugin` (`Kind`, `Href`, `PageId`, `Layout`, `Size`, `Width`, `NumberOfPosts`) in the module's site library `Grove.Integrations.Facebook.BlazorWasm`; its script loads the SDK through the module's own `sdk/init.js` and `sdk/sdk.{culture}.js` endpoints and parses the element | done |

## Not recreated

Test-only widget types (`TestWidgetA`, `TestWidgetB`, `EsModuleTestFieldsWidget`,
`EsModuleTestLiquidWidget`, `SeoMetaFieldSetTestWidget`, `FormsFieldEditorTestWidget`) went with
their tests. The es-module and SEO fixtures keep their non-widget types.

## Left

- ☐ The Razor → Blazor component parity audit covers every widget: each has a Blazor component
  equivalent, reusing `Grove.Display.Components` ([blazor-display.md](blazor-display.md) › Rework plan,
  step 7).
- ☐ The SiteTheme blog recipe's Contact page has a body (rich text and a contact form) but no
  UI-action workflow to send to, so its submit only checks the required inputs.
- ☐ Per-item placement of components into zones (what `WidgetsListPart` did) has no equivalent;
  an item composes its own body with `DisplayTreeFieldSet` instead.
