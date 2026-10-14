# MVC retirement › Component parity: every Razor template to a Blazor component, or gone

The parity audit of [blazor-display.md](../blazor-display.md) › Rework plan, step 7, and
[shape-engine.md](shape-engine.md) C4: every Razor view and shape template, and every widget
([widgets.md](../widgets.md)), has a Blazor component equivalent or a reason it needs none.
Siblings: [views.md](views.md) (the view surfaces and their ports), [shape-engine.md](shape-engine.md)
(the renderers this audit lets go), [api.md](api.md) (the APIs the pages call).

Rules:

- **Boundaries may differ.** One component may cover several shapes (`BagPartEditor` covers the
  bag editor, its content cards and its type picker), and a shape may decompose into primitives
  or disappear into the tree builder (item and part wrappers become zones).
- **Reuse first.** The equivalent is a component already in `Crest.Components`, a component
  registered through the component registry (`IComponentRegistry`, bound by
  `DisplayTreeBuilder`), or a native page. A row is **new** only when nothing existing covers it;
  a mode or a binding name added to an existing component is **existing+**, never a second
  component.
- **Liquid templates are not part of the audit**: they stay and are updated to display trees
  separately (blazor-display.md step 7). `LiquidPart`'s Razor templates are in scope (§ Open).

Inventory: the 1,072 `.cshtml` files under `src/` ([views.md](views.md) › The inventory: the
597 driver-shape templates, the 261 `Views/Items/*`, controller views, layouts, Razor Pages and
`_ViewImports`/`_ViewStart`). Fruitful has no `.cshtml`. The 34 files under
`test/Crest.Tests.Pages` are Razor Pages fixtures that test modular Razor Pages; they go with
`ModularPageMvcCoreBuilderExtensions` (views.md) and are not counted.

## Summary

| Fate | Templates | |
| --- | ---: | --- |
| Covered by existing components | 195 | 121 as they are (**existing**), 74 once the existing component or page gains a mode, a binding name or a section (**existing+**) |
| Needs a new component or page | 155 | **new**; § New components and § New pages |
| Not needed | 716 | **not needed**: 300 replaced by a native page or the Blazor shell (or with no caller left), 77 editor/display-mode option entries of the MVC definition editor, 157 deployment step shapes (steps are JSON), 46 admin menu icon shapes, 82 `_ViewImports`/`_ViewStart`, 54 in modules proposed for deletion (the user decides) |
| Open | 6 | **open**: `LiquidPart` (3), Setup (2), GraphiQL (1); § Open |
| **Total** | **1072** | every `.cshtml` under `src/` |

Widgets ([widgets.md](../widgets.md)): 26 rows, 22 covered by existing components, 4 not needed, none new (§ Widgets).

## New components

Each lives in its owner's client library and is registered (`[BlazorComponent]`) where a
template or the editor tree places it: the editors (through `Edits`), `HeadLink`,
`CulturePicker`, `ContentDiff`. Each builds on what exists; the column says what. Ordered by the
templates each covers.

| ☐ | Component | What it is | Builds on | Templates | Owner |
| --- | --- | --- | --- | ---: | --- |
| ☐ | `SettingsForm` | A settings group's form, generated from the group's settings schema (fields, sections, write-only secrets, the "saving reloads the tenant" notice that `Settings-Reload.Wrapper` gave), loaded and saved through the group's settings API; served at `/Settings/{groupId}` for every group without a page of its own. The bespoke settings pages stay where the form has behaviour (test send, certificate pickers). | `Form`, `TextInput`, `TextArea`, `Select` (`Crest.Components/Components/Forms`), `CrestFieldset`, `CrestSwitch`, `CrestAlert` | 31 | Crest.Settings (component in `Crest.Components` beside `ContentEditor`); schemas declared by each settings module (api.md) |
| ☐ | `TwoFactorSetup` | The signed-in user's two-factor page: enabled methods (authenticator app, email, SMS), authenticator enrollment with a QR code and a verification code, phone number confirmation, recovery codes (generate, show once), reset and disable. One component placed by each shell's account page. | `CrestQRCode`, `CrestSecurityCode`, `CrestSteps`, `Dialog` | 18 | Crest.Users |
| ☐ | `ListPartEditor` | A list's contained items in the editor: the children of the item (search, status, paging), reorder when the list is ordered, create a child of an allowed type (the child's editor gets its container, what `ListPart.ContainerId` carried). Only if Crest.Lists is kept (views.md › Module deletions). | `CrestDataGrid`, `Pager`, `ContentEditor` | 8 | Crest.Lists |
| ☐ | `TwoFactorChallenge` | The second sign-in step: the code for the user's method (send again for email and SMS), or a recovery code; part of the login pages of every shell. | `CrestSecurityCode`, `CrestLogin` | 6 | Crest.Users |
| ☐ | `TaxonomyPartEditor` | A taxonomy's term tree: add, rename, nest and reorder terms, each term edited with its own content editor. | `CrestTree`, `Dialog`, `ContentEditor` | 5 | Crest.Taxonomies |
| ☐ | `CodeEditor` | A code text editor with a language mode (HTML, Liquid, JavaScript, CSS, JSON), line numbers and syntax highlighting; an editor component (`Edits`) chosen by the field's editor setting. `JsonEditor`, the Templates page and the Shortcodes page use it too. | `JsonEditor`'s value handling (`FieldEditorBase`) | 4 | Crest.Components |
| ☐ | `SeoMetaPartEditor` | The SEO part: meta title, description, keywords, canonical URL, robots, Open Graph, Twitter card, Google schema JSON, the social image. | `TextInput`, `TextArea`, `MediaPicker`, `CrestTabs`, `CodeEditor` | 4 | Crest.Seo |
| ☐ | `ContentPreviewPane` | The editor's live preview: the unsaved item rendered through the display endpoint beside the editor tree, refreshed as the item changes. | `ContentEditor`'s context, the display tree renderer | 3 | Crest.ContentPreview |
| ☐ | `TaxonomyFieldEditor` | A taxonomy field's terms: a tree of check boxes or radios, or a tags input with creation of new terms when the settings allow. | `CrestTree`, `CrestAutoComplete`, `CrestChipList` | 2 | Crest.Taxonomies |
| ☐ | `GeoPointFieldEditor` | Latitude and longitude, set by typing or by placing a marker on a map. | `CrestNumeric`, `CrestGoogleMap` | 2 | Crest.Spatial |
| ☐ | `HeadLink` | A `<link>` contributed to the page head (`rel`, `type`, `href`, `title`): a list's RSS feed, the RSD discovery link. Renders nothing in the body. | `HeadContent` | 2 | Crest.Components |
| ☐ | `IconPicker` | Pick an icon from the tenant's icon sets, with search; stores the icon key. | the icon catalog behind `Icons.razor`, `CrestIcon`, `CrestDropDown` | 1 | Crest.Icons |
| ☐ | `CulturePicker` | The site's culture switcher: the current item's translations (its localization set), or the site cultures. | `Select`, `Menu` | 1 | Crest.ContentLocalization |
| ☐ | `LocalizationPartEditor` | An item's culture and its translations: the set's other cultures, open or create a translation. | `ContentEditor`'s context, `Badge`, `MenuAction` | 1 | Crest.ContentLocalization |
| ☐ | `ContentDiff` | Two versions of an item side by side, changed values marked; on the audit record and the editor's versions list. | `PropertyList`, the content display API | 1 | Crest.Contents |

## New pages

Ports in [views.md](views.md); each page is a component built from existing primitives
(`CrestDataGrid`, the form inputs, `SettingsForm`, `CodeEditor`). The ones marked "if kept"
exist only if views.md › Module deletions keeps the module.

| ☐ | Page | Module | Templates | Condition |
| --- | --- | --- | ---: | --- |
| ☐ | Sitemaps (sitemaps, sitemap indexes, sources, cache) | Crest.Sitemaps (with `ContentTypesSitemapSource` from Crest.Contents) | 20 | if kept |
| ☐ | Rate-limit policies (policies, limiters per kind) | Crest.RateLimits | 20 | if kept |
| ☐ | URL rewrite rules (rewrite and redirect rules) | Crest.UrlRewriting | 13 | if kept |
| ☐ | Shortcodes (templates list and editor) | Crest.Shortcodes | 6 | enabled in Venti |
| ☐ | Feature profiles (`/TenantFeatureProfiles`) | Crest.Tenants | 5 | — |
| ☐ | CORS policies | Crest.Cors | 1 | if kept |
| ☐ | SMS test (`/Sms/Test`, the email test pattern) | Crest.Sms | 1 | — |

## Open

| ☐ | Templates | Question | Where it is decided |
| --- | --- | --- | --- |
| ☐ | `LiquidPart`, `LiquidPart.Summary`, `LiquidPart.Edit` | A server-side markup leaf rendered by the Liquid manager, or the bodies migrated to `DisplayTreePart` and the part retired. If the part stays, its editor is `CodeEditor` and its display a markup leaf: no new component either way. | [shape-engine.md](shape-engine.md) › Component coverage |
| ☐ | `Crest.Setup` `Setup/Index`, `_Layout` | Setup on the keep list, or a pre-database page (static HTML or static SSR) | [views.md](views.md) › Setup |
| ☐ | `Crest.Apis.GraphQL` `Admin/Index` (GraphiQL) | Delete, or a static page | [views.md](views.md) › Ports |

Fates that hold only on a pending decision: the **not needed** rows of Crest.Demo, Crest.AdminDashboard
and the MVC samples assume their deletion; the "if kept" pages and `ListPartEditor`,
`TaxonomyFieldEditor`, `TaxonomyPartEditor`, `SeoMetaPartEditor` assume their modules stay
([views.md](views.md) › Module deletions).

## Widgets

Every widget of [widgets.md](../widgets.md), with its equivalent.

| | Widget | Fate | Blazor equivalent |
| --- | --- | --- | --- |
| ☑ | Layer widgets | existing | `LayerPlacement` on `Crest.AdminTheme/wasm/Pages/Layers.razor` and the `Layers` recipe step |
| ☑ | `WidgetsListPart` | not needed | an item composes its own body with `DisplayTreePart` (`DisplayTreeEditor`, `Crest.Components/Components/Editors/DisplayTreeEditor.cs`); per-type placement is a `Content__{Type}` template |
| ☑ | `FlowPart` / Container | existing | `Container` (`Crest.Components/Components/Interaction/Fragment.cs`), `Stack` (`CrestStack`), `BagPart`, `DisplayTreePart` |
| ☑ | `BlazorComponent` content type | existing | the `component` template tag, or a layer placement |
| ☑ | `LiquidWidget` | existing | a `Template` content item (a composite when it takes inputs) |
| ☑ | `Widget.Wrapper`, zone and type alternates | existing | component variants and overrides (blazor-display.md § 5a) |
| ☑ | Alias/Autoroute widget shape providers | not needed | internal to the old shapes |
| ☑ | Paragraph | existing | `RichText` (`Crest.Components/Components/Content/RichText.cs`) |
| ☑ | Blockquote | existing | `CrestQuote` (`Crest.Components/Components/CrestQuote`) |
| ☑ | Image (`ImageWidget`) | existing | `Figure` (`Crest.Components/Components/Content/RichText.cs`) with `MediaPicker` |
| ☑ | RawHtml | not needed | templates are data, never code (blazor-display.md § 10); `RichText` for markup |
| ☑ | Container | existing | `Container`, `Stack`, `Card` (`CrestCard`) |
| ☑ | LinkMenuItem | existing | `MenuItem` in `Menu` (`Crest.Components/Components/Interaction/LinkMenu.cs`) |
| ☑ | Form | existing | `Form` (`Crest.Components/Components/Forms/Form.cs`) |
| ☑ | Label | existing | the `Label` parameter of every input (`FormInputBase`) |
| ☑ | Input | existing | `TextInput` (`Crest.Components/Components/Forms/Inputs.cs`) |
| ☑ | TextArea | existing | `TextArea` (`Inputs.cs`) |
| ☑ | Select | existing | `Select` (`Inputs.cs`) |
| ☑ | Button | existing | `Button` (`CrestButton`) |
| ☑ | Validation | existing | each input shows the run's errors for its name (`DisplayState`) |
| ☑ | ValidationSummary | existing | `ValidationSummary` (`Inputs.cs`) |
| ☑ | Visibility rules | existing | every input's `Visible`, bound to a comparison |
| ☑ | ReCaptcha | existing | `Captcha` (`Crest.Components/Components/Forms/Captcha.cs`) |
| ☑ | SearchForm | existing | `SearchBox` (`Crest.Components/Components/Forms/SearchBox.cs`) |
| ☑ | Facebook Chat, Comments, Continue With, Like, Quote, Save, Share | existing | `FacebookPlugin` (`Crest.Facebook/site-wasm/FacebookPlugin.cs`) |
| ☑ | Test-only widget types | not needed | removed with their tests |

## Tracker: shapes every module has

| | Razor templates | n | Fate | Modules |
| --- | --- | ---: | --- | --- |
| ☑ | `_ViewImports`, `_ViewStart` | 82 | not needed: Razor plumbing; each goes with the last view of its module | every module with views |
| ☑ | `NavigationItemText-*.Id` (admin menu icons) | 46 | not needed: the admin navigation is native (`Crest.AdminTheme/wasm/Components/Admin/PrimaryNavMenu.razor`); icons come with the menu API | Admin, AdminDashboard, AuditTrail, AzureAI, ContentTypes, Contents, Cors, Demo, Elasticsearch, Email, Facebook, GitHub, Google, Indexing, Localization, Lucene, Media, Microsoft.Authentication, OpenId, Queries, ReverseProxy, Roles, Search, Settings, Sms, Taxonomies, Tenants, Themes, Twitter, UrlRewriting, Users |
| ☑ | `Items/*DeploymentStep.{Edit,Summary,Thumbnail}` | 157 | not needed: a step's settings are edited as the JSON the plan stores on `Pages/DeploymentPlan.razor` (admin-routes.md); a step type a module adds needs no component | AdminMenu, AzureAI, ContentTypes, Contents, CustomSettings, DataLocalization, Deployment, Elasticsearch, Facebook, Features, Google, Indexing, Layers, Lucene, Media, Microsoft.Authentication, OpenId, Placements, Queries, RateLimits, Roles, Search, Settings, Shortcodes, Sitemaps, Templates, Tenants, Themes, Users |

## Tracker by module

Fates: **existing** (the component or page named covers it now), **existing+** (that component or page covers it after a small addition, named in the row), **new** (§ New components, § New pages), **not needed** (the reason), **open** (§ Open). ☑ = nothing to build; ☐ = work left. `n` counts `.cshtml` files. Paths are under `src/`; `Pages/` is `Crest.AdminTheme/wasm/Pages/`. Shapes every module has are in the table above.

### Content display and editing

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Crest.Contents | `Content.Edit`, `ContentPart.Edit`, `Content.{Publish,SaveDraft,Unpublish,Delete}Button`, `ContentPart-BagPart.Edit` | 7 | existing | `ContentEditor`, `EditorSection` (`Crest.Components/Components/Editors/ContentEditor.cs`), `BagPartEditor`; the actions are `Crest.AdminTheme/wasm/Components/Admin/ContentItemVersionActions.razor` |
| ☑ | Crest.Contents | `ContentItem`, `ContentPart`, `ContentPart.Summary` (item and part wrappers) | 3 | existing | the tree builder's zone mapping and `Container` (`Crest.Components/Components/Interaction/Fragment.cs`) |
| ☑ | Crest.Contents | `Content`, `Content.Summary` | 2 | existing | `Content` (`Crest.SiteTheme/wasm/Components/SiteLayout.cs`); `_Summary` resolves to it by the registry's display-variant rule |
| ☐ | Crest.Contents | `AuditTrailPart.Edit` (a comment saved with the version) | 1 | existing+ | `ContentEditor`: a save-comment input next to the save actions |
| ☐ | Crest.Contents | `CommonPart-{Date,Owner}.Edit` | 2 | existing+ | `DateFieldEditor` and `UserPickerFieldEditor` (`Crest.Components/Components/Editors/FieldEditors.cs`) bound to `CommonPart`'s created date and owner |
| ☐ | Crest.Contents | `Items/AddToDeploymentPlan-*`, `Items/ExportContentToDeploymentTarget-*` (buttons, bulk actions, modals), `Items/Download-Button-Actions.SummaryAdmin` | 9 | existing+ | `Pages/ContentItems.razor`: row and bulk actions opening `Dialog` (`Crest.Components/Components/Interaction/Dialog.razor.cs`) with the plan or target picker |
| ☐ | Crest.Contents | `Stereotype`, `Stereotype.Summary` (retypes a stereotyped item to `Content`) | 2 | existing+ | `Content`, also bound under the name `Stereotype` |
| ☐ | Crest.Contents | `ContentVersionPruningSettings_Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.Contents | `Items/AuditTrailContentEventDiff.DetailAdmin` | 1 | new | `ContentDiff` (§ New components) |
| ☐ | Crest.Contents | `Items/ContentTypesSitemapSource.*` | 3 | new | If Sitemaps is kept: the Sitemaps page (see Crest.Sitemaps) |
| ☐ | Crest.Contents | `Items/ExportContentToDeploymentTargetSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☑ | Crest.Contents | `Admin/{Create,Display,Edit,List}` | 4 | not needed | native `Pages/ContentItems.razor`, `Pages/CreateContentItem.razor`, `Pages/EditContentItem.razor`, `Pages/DisplayContentItem.razor` (views.md wave 1) |
| ☑ | Crest.Contents | `AdminDashboardContents` | 1 | not needed | native `Pages/Dashboard.razor` |
| ☑ | Crest.Contents | `AuditTrailPart.Option` | 1 | not needed | MVC definition editor option; the native definition editor edits settings as JSON (`Pages/ContentTypes.razor`) |
| ☑ | Crest.Contents | `BodyAspect` | 1 | not needed | no driver or code produces the shape; bodies display through `HtmlBodyPart`/`MarkdownBodyPart` |
| ☑ | Crest.Contents | `ContentPart-{FlowPart,WidgetsListPart}.Edit` | 2 | not needed | widgets retired (widgets.md); an item's own body is `DisplayTreeEditor` |
| ☑ | Crest.Contents | `ContentsMetadata` (sets the page title) | 1 | not needed | the site page sets its title (`SitePageBuilder`, `PageTitle`) |
| ☑ | Crest.Contents | `Item/Preview`, `Download/Display` | 2 | not needed | the display endpoint (`?format=html`) and a download API (views.md, api.md) |
| ☑ | Crest.Contents | `Items/AuditTrailContentEvent*`, `Items/AuditTrailContentsAction_SummaryAdmin`, `Items/ContentAuditTrailSettings.Edit` | 6 | not needed | native `Crest.AuditTrail/blazor-wasm/Pages/AuditRecord.razor`, `AuditTrail.razor`, `AuditSettings.razor` |
| ☑ | Crest.Contents | `Items/ContentTypesAdminNode.Fields.Tree*` | 3 | not needed | native `Pages/AdminMenus.razor` |
| ☑ | Crest.Contents | admin list and detail shapes: `*.SummaryAdmin`, `*.DetailAdmin`, `*.Thumbnail`, `ContentsAdminList*`, `ContentsAdminFilters-*`, `Contents*_SummaryAdmin` | 28 | not needed | native `Pages/ContentItems.razor`, `Pages/DisplayContentItem.razor` |
| ☑ | Crest.Contents | view components `DisplayContentItem`, `SelectContentTypes` | 2 | not needed | used only inside MVC views; deleted with their last caller (views.md) |
| ☑ | Crest.ContentFields | `Media-HtmlField.Wrapper` (media picker wiring) | 1 | existing | `HtmlEditor`'s image tool with `MediaPicker` (`Crest.Components/Components/Files/MediaPicker.cs`) |
| ☑ | Crest.ContentFields | displays: `BooleanField`, `ContentPickerField`, `DateField`, `DateTimeField`, `HtmlField`, `LinkField`, `LocalizationSetContentPickerField`, `MultiTextField`, `NumericField`, `TextField`, `TextField-Header.Display`, `TimeField`, `UserPickerField`, `UserPickerField-UserNames.Display`, `YoutubeField` | 15 | existing | the field components of the same names in `Crest.Components/Components/Content/FieldDisplays.cs` (`TextField_Display__Header` for the heading mode) |
| ☑ | Crest.ContentFields | editors: `TextField.Edit`, `BooleanField.Edit`, `ContentPickerField.Edit`, `DateField.Edit`, `DateTimeField.Edit`, `LinkField.Edit`, `NumericField.Edit`, `UserPickerField.Edit`, `HtmlField{,-Wysiwyg,-Trumbowyg}.Edit` | 11 | existing | `TextFieldEditor`, `BooleanFieldEditor`, `ContentPickerFieldEditor`, `DateFieldEditor`, `LinkFieldEditor`, `NumericFieldEditor`, `HtmlEditor` (`Crest.Components/Components/Editors/FieldEditors.cs`), `UserPickerFieldEditor` (`Crest.Components/Components/Editors/UserPickerFieldEditor.cs`) |
| ☐ | Crest.ContentFields | `BooleanField-Switch.Edit` | 1 | existing+ | `BooleanFieldEditor` with a switch mode (`CrestSwitch`) |
| ☐ | Crest.ContentFields | `DateField-Localized.Edit` | 1 | existing+ | `DateFieldEditor` with the culture's date format |
| ☐ | Crest.ContentFields | `HtmlField-Multiline.Edit` | 1 | existing+ | `HtmlEditor` with a plain multi-line mode (`CrestTextArea`) |
| ☐ | Crest.ContentFields | `LocalizationSetContentPickerField.Edit` | 1 | existing+ | `ContentPickerFieldEditor` storing localization set ids |
| ☐ | Crest.ContentFields | `MultiTextField{,-CheckboxList,-Picker}.Edit` | 3 | existing+ | `OptionPickerFieldEditor` also `Edits` `MultiTextField` (check-box list or picker over `CrestCheckBoxList`/`CrestDropDown`) |
| ☐ | Crest.ContentFields | `NumericField-{Range,Select,Slider,Spinner}.Edit` | 4 | existing+ | `NumericFieldEditor` modes over `CrestSlider`, `CrestDropDown`, `CrestNumeric` |
| ☐ | Crest.ContentFields | `TextField-Color.Edit` | 1 | existing+ | `ColorEditor` (`Crest.Components/Components/Editors/ColorEditor.cs`) chosen by the field's editor setting |
| ☐ | Crest.ContentFields | `TextField-{Email,Tel,Url,TextArea,PredefinedList}.Edit` | 5 | existing+ | `TextFieldEditor` follows the field's editor setting: input type, multi-line, or `Select` (`Crest.Components/Components/Forms/Inputs.cs`) over the predefined options |
| ☐ | Crest.ContentFields | `TimeField.Edit` | 1 | existing+ | `DateFieldEditor` also `Edits` `TimeField` (time only, `CrestTimeSpanPicker`) |
| ☐ | Crest.ContentFields | `YoutubeField.Edit` | 1 | existing+ | `TextFieldEditor` on the raw address; the field handler derives the embed address |
| ☐ | Crest.ContentFields | `TextField-IconPicker.Edit` | 1 | new | `IconPicker` (§ New components) |
| ☐ | Crest.ContentFields | `TextField-{Monaco,CodeMirror}.Edit`, `HtmlField-Monaco.Edit` | 3 | new | `CodeEditor` (§ New components) |
| ☑ | Crest.ContentFields | `*.Option`, `*.DisplayOption` (editor and display-mode entries of the MVC field settings) | 47 | not needed | the native definition editor edits field settings as JSON (`Pages/ContentParts.razor`); the editor/display choice reads the registry |
| ☑ | Crest.Flows | `BagPart`, `BagPart.Empty` | 2 | existing | `BagPart`, `BagPart_Empty` (`Crest.Components/Components/Content/FieldDisplays.cs`) |
| ☑ | Crest.Flows | `BagPart{,.Fields,-Blocks}.Edit`, `ContentCard*`, `ContentTypePicker` | 12 | existing | `BagPartEditor` (`Crest.Components/Components/Editors/BagPartEditor.cs`): cards, type picker, reorder, blocks |
| ☑ | Crest.Flows | `BagPart.Option`, `BagPart-Blocks.Option` | 2 | not needed | native definition editor (settings as JSON) |
| ☑ | Crest.Html | `HtmlBodyPart`, `HtmlBodyPart.Summary` | 2 | existing | `HtmlBodyPart` (`Crest.Components/Components/Content/RichText.cs`) |
| ☑ | Crest.Html | `HtmlBodyPart{,-Wysiwyg,-Trumbowyg}.Edit` | 3 | existing | `HtmlEditor` (`Crest.Components/Components/Editors/FieldEditors.cs`) |
| ☑ | Crest.Html | `Media-HtmlBodyPart.Wrapper` | 1 | existing | `HtmlEditor`'s image tool with `MediaPicker` |
| ☐ | Crest.Html | `HtmlBodyPart-Monaco.Edit` | 1 | new | `CodeEditor` |
| ☑ | Crest.Html | `HtmlBodyPart*.Option`, `HtmlBodyPart.DisplayOption` | 5 | not needed | native definition editor |
| ☑ | Crest.Markdown | `MarkdownBodyPart`, `MarkdownBodyPart.Summary`, `MarkdownField` | 3 | existing | `MarkdownBodyPart` (`Crest.Components/Components/Content/RichText.cs`), `MarkdownField` (`Crest.Components/Components/Content/FieldDisplays.cs`) |
| ☑ | Crest.Markdown | `MarkdownBodyPart{,-Wysiwyg}.Edit`, `MarkdownField{,-Wysiwyg}.Edit`, `Media-MarkdownBodyPart.Wrapper` | 5 | existing | `MarkdownEditor` (`Crest.Components/Components/Editors/FieldEditors.cs`) with `MediaPicker` |
| ☑ | Crest.Markdown | `*.Option`, `*.DisplayOption` | 6 | not needed | native definition editor |
| ☑ | Crest.Title | `TitlePart.Edit`, `ContentPart-TitlePart.Edit` | 2 | existing | `TitlePartEditor` (`Crest.Components/Components/Editors/FieldEditors.cs`) |
| ☑ | Crest.Title | `TitlePart`, `TitlePart.Summary` | 2 | existing | `TitlePart`, `TitlePart_Summary` (`Crest.Components/Components/Content/FieldDisplays.cs`) |
| ☑ | Crest.Title | `TitlePart.Option`, `TitlePart.DisplayOption` | 2 | not needed | native definition editor |
| ☑ | Crest.Autoroute | `AutoroutePart.Edit` | 1 | existing | `AutoroutePartEditor` (`Crest.Components/Components/Editors/FieldEditors.cs`) |
| ☐ | Crest.Alias | `AliasPart.Edit` | 1 | existing+ | `TextFieldEditor` also `Edits` `AliasPart` (its `Alias` value) |
| ☑ | Crest.Alias | `AliasPart.Option` | 1 | not needed | native definition editor |
| ☑ | Crest.Media | `MediaField` | 1 | existing | `MediaField` (`Crest.Components/Components/Content/FieldDisplays.cs`) |
| ☑ | Crest.Media | `MediaField{,-Attached,-Gallery}.Edit`, `Shared/MediaFieldEdit{AnchorModal,Localization,MediaTextModal}` | 6 | existing | `MediaFieldEditor` (`Crest.Components/Components/Files/MediaFieldEditor.cs`, `CrestMediaFieldInput.razor`: paths, texts, anchors) |
| ☐ | Crest.Media | `MediaCache/Index` | 1 | existing+ | `Pages/MediaOptions.razor`: the cache section (views.md port) |
| ☐ | Crest.Media | `MediaApiSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☑ | Crest.Media | `Admin/{Index,Options}`, `MediaProfiles/*`, `Shared/MediaStoreEntries` | 6 | not needed | native `Pages/MediaLibrary.razor`, `Pages/MediaOptions.razor`, `Pages/MediaProfiles.razor` |
| ☑ | Crest.Media | `MediaField*.Option`, `MediaField.DisplayOption` | 4 | not needed | native definition editor (settings as JSON) |
| ☐ | Crest.Media.Azure, Crest.Media.AmazonS3 | `Admin/Options` ×2 | 2 | existing+ | `Pages/MediaOptions.razor`: the provider's read-only section |
| ☑ | Crest.Lists | `ListPart` | 1 | existing | `ListPart` (`Crest.Components/Components/Content/FieldDisplays.cs`) with `Pager` |
| ☐ | Crest.Lists | `ListPartFeed.Edit` | 1 | existing+ | the part's values on the editor tree (the `JsonEditor` fallback today), or `TextFieldEditor`/`NumericFieldEditor` bound to them |
| ☐ | Crest.Lists | `ListPart.{ContainerId,Sortable}`, `ListPartDetailAdmin`, `ListPartDetailAdminSearchPanel`, `ListPartHeaderAdmin`, `ListPartNavigationAdmin`, `ListPartSummaryAdmin`, `ContentsAdminList-ListPartFilter` | 8 | new | New (if Lists is kept): `ListPartEditor` (§ New components) |
| ☐ | Crest.Lists | `ListPartFeed` (RSS link), `ListPart.RemotePublishing` (RSD link) | 2 | new | `HeadLink` (§ New components) |
| ☑ | Crest.Lists | `Items/ListsAdminNode.Fields.Tree*` | 3 | not needed | native `Pages/AdminMenus.razor` |
| ☑ | Crest.Lists | `ListPart.DisplayOption` | 1 | not needed | native definition editor |
| ☑ | Crest.Taxonomies | `TaxonomyField`, `TaxonomyPart`, `TaxonomyPart.Empty`, `Term`, `TermItem`, `TermContentItem` | 6 | existing | `TaxonomyField`, `TaxonomyPart`, `TaxonomyPart_Empty` (`Crest.Components/Components/Content/FieldDisplays.cs`) |
| ☐ | Crest.Taxonomies | `ContentsAdminListTaxonomyFilter`, `TaxonomyContentsAdminListSettings.Edit` | 2 | existing+ | `Pages/ContentItems.razor`: a taxonomy filter (the settings name the taxonomies) |
| ☐ | Crest.Taxonomies | `TaxonomyField-Tags.Display` | 1 | existing+ | `TaxonomyField` with a tags mode (`Badge`) |
| ☐ | Crest.Taxonomies | `TermPart` (a term's page: the tagged items) | 1 | existing+ | `ListPart` (`Crest.Components/Components/Content/FieldDisplays.cs`) also bound to `TermPart`, with `Pager` |
| ☐ | Crest.Taxonomies | `TaxonomyField{,-Tags}.Edit` | 2 | new | `TaxonomyFieldEditor` (§ New components) |
| ☐ | Crest.Taxonomies | `TaxonomyPart.Edit`, `Admin/{Create,Edit}`, `Content.TermAdmin`, `ContentPart.TermAdmin` | 5 | new | `TaxonomyPartEditor` (§ New components); a term is edited in `Pages/EditContentItem.razor` |
| ☑ | Crest.Taxonomies | `TaxonomyField*.Option`, `*.DisplayOption` | 4 | not needed | native definition editor |
| ☑ | Crest.Spatial | `GeoPointField` | 1 | existing | `GeoPointField` (`Crest.Components/Components/Content/FieldDisplays.cs`) |
| ☐ | Crest.Spatial | `GeoPointField{,-Leaflet}.Edit` | 2 | new | `GeoPointFieldEditor` (§ New components) |
| ☑ | Crest.Spatial | `GeoPointField*.Option` | 2 | not needed | native definition editor |
| ☐ | Crest.Seo | `RobotsSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.Seo | `SeoMetaPart{,GoogleSchema,OpenGraph,Twitter}.Edit` | 4 | new | `SeoMetaPartEditor` (§ New components) |
| ☐ | Crest.PublishLater | `PublishLaterPart.Edit` | 1 | existing+ | `DateFieldEditor` (`Crest.Components/Components/Editors/FieldEditors.cs`) also `Edits` `PublishLaterPart` (its scheduled UTC date) |
| ☐ | Crest.PublishLater | `PublishLaterPart.SummaryAdmin` (the scheduled-date note) | 1 | existing+ | `Badge` (`Crest.Components/Components/CrestBadge`) bound to `PublishLaterPart` in `/display` and the content list |
| ☑ | Crest.PublishLater | `PublishLaterPart.Option` | 1 | not needed | native definition editor |
| ☐ | Crest.ArchiveLater | `ArchiveLaterPart.Edit` | 1 | existing+ | `DateFieldEditor` (`Crest.Components/Components/Editors/FieldEditors.cs`) also `Edits` `ArchiveLaterPart` (its scheduled UTC date) |
| ☐ | Crest.ArchiveLater | `ArchiveLaterPart.SummaryAdmin` (the scheduled-date note) | 1 | existing+ | `Badge` (`Crest.Components/Components/CrestBadge`) bound to `ArchiveLaterPart` in `/display` and the content list |
| ☑ | Crest.ArchiveLater | `ArchiveLaterPart.Option` | 1 | not needed | native definition editor |
| ☐ | Crest.Liquid | `LiquidPart`, `LiquidPart.Summary`, `LiquidPart.Edit` | 3 | open | open in shape-engine.md › Component coverage: a server-side markup leaf, or migrate the bodies to `DisplayTreePart`; if kept, the editor is `CodeEditor` |
| ☐ | Crest.ContentLocalization | `LocalizationPart.SummaryAdmin{,Links}`, `ContentsAdminList-LocalizationPartFilter`, `Items/ContentsAdminFilters-Culture.Thumbnail` | 4 | existing+ | `Pages/ContentItems.razor`: a culture column and filter |
| ☐ | Crest.ContentLocalization | `ContentCulturePickerSettings.Edit`, `ContentRequestCultureProviderSettings.Edit` | 2 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.ContentLocalization | `ContentCulturePicker` (site) | 1 | new | `CulturePicker` (§ New components) |
| ☐ | Crest.ContentLocalization | `LocalizationPart.Edit` | 1 | new | `LocalizationPartEditor` (§ New components) |
| ☐ | Crest.ContentPreview | `ContentPreview.Button`, `Preview/{Index,Render}` | 3 | new | `ContentPreviewPane` (§ New components) |
| ☑ | Crest.ContentPreview | `Preview/_Layout` | 1 | not needed | the preview renders in the site shell |

### Site navigation and search

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Crest.Menu | `Menu`, `MenuItem`, `MenuItemLink{,-ContentMenuItem,-LinkMenuItem}` | 5 | existing | `Menu`, `MenuItem` (`Crest.Components/Components/Interaction/LinkMenu.cs`) |
| ☐ | Crest.Menu | `MenuItemLink-HtmlMenuItem` | 1 | existing+ | `MenuItem` (`Crest.Components/Components/Interaction/LinkMenu.cs`) with a sanitized markup text |
| ☑ | Crest.Navigation | `Navigation`, `NavigationItem`, `NavigationItemLink`, `NavigationItemText` | 4 | existing | `Menu`, `MenuItem` on the site; `PrimaryNavMenu.razor` in the admin |
| ☑ | Crest.Search | `Search-Form` | 1 | existing | `SearchBox` (`Crest.Components/Components/Forms/SearchBox.cs`) |
| ☑ | Crest.Search | `Search/Search`, `Search-List`, `Search-Results` | 3 | existing | a site page in Crest.Search's site library composed of `SearchBox`, `ItemList` (`Crest.Components/Components/Interaction/ItemList.razor.cs`) and `Pager`, over a search API (api.md) |
| ☐ | Crest.Search | `SearchSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☑ | Crest.Search | `SearchFormPart`, `SearchFormPart.Edit` | 2 | not needed | the part is unused (widgets.md: `SearchBox`); delete it |
| ☐ | Crest.AzureAI | `AzureAISearchDefaultSettings.Edit`, `AzureAISearchQuerySettings.Edit` | 2 | new | `SettingsForm` (§ New components) |
| ☑ | Crest.AzureAI | `AzureAISearchIndexProfile.Edit` | 1 | not needed | native `Pages/Indexes.razor` (profiles as JSON) |
| ☐ | Crest.Lucene | `Admin/Query` | 1 | existing+ | `Pages/Indexes.razor`: a query tab per index, on the `Pages/SqlQuery.razor` pattern (views.md port) |
| ☐ | Crest.Lucene | `LuceneQuerySettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☑ | Crest.Lucene | `*IndexProfile.Edit`, `*IndexProfile.ActionsMenuItems.SummaryAdmin` | 2 | not needed | native `Pages/Indexes.razor` (profiles as JSON) |
| ☑ | Crest.Lucene | `*Query.{Edit,SummaryAdmin,Buttons.SummaryAdmin}`, `Query-*.Link` | 4 | not needed | native `Pages/Queries.razor` (provider metadata as JSON) |
| ☐ | Crest.Elasticsearch | `Admin/{Query,IndexInfo,NotConfigured}` | 3 | existing+ | `Pages/Indexes.razor`: a query tab per index, on the `Pages/SqlQuery.razor` pattern (views.md port) |
| ☐ | Crest.Elasticsearch | `ElasticsearchQuerySettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☑ | Crest.Elasticsearch | `*IndexProfile.Edit`, `*IndexProfile.ActionsMenuItems.SummaryAdmin` | 2 | not needed | native `Pages/Indexes.razor` (profiles as JSON) |
| ☑ | Crest.Elasticsearch | `*Query.{Edit,SummaryAdmin,Buttons.SummaryAdmin}`, `Query-*.Link` | 4 | not needed | native `Pages/Queries.razor` (provider metadata as JSON) |
| ☑ | Crest.Queries | `Admin/*`, `Query*`, `SqlQuery*`, `Query-Sql.Link` | 15 | not needed | native `Pages/Queries.razor`, `Pages/SqlQuery.razor` |

### Access

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Crest.Users | `Account/Login` | 1 | existing | `Pages/Login.razor` over `CrestLogin` (`Crest.Components/Components/CrestLogin`), `Crest.MemberTheme/wasm/Pages/MemberLogin.razor`; moves to Crest.Users (views.md wave 2) |
| ☑ | Crest.Users | `Registration/{Register,RegistrationPending}` | 2 | existing | `Crest.MemberTheme/wasm/Pages/MemberRegister.razor`, generalised into Crest.Users (views.md wave 2) |
| ☑ | Crest.Users | `ResetPassword/*` (4) | 4 | existing | `MemberForgotPassword.razor`, `MemberResetPassword.razor` (`Crest.MemberTheme/wasm/Pages`), generalised into Crest.Users |
| ☐ | Crest.Users | `CustomUserSettings{,.Edit,.SummaryAdmin}` | 3 | existing+ | `Pages/UserEditor.razor`: a section per custom user settings type on `ContentEditor` (as `Pages/CustomSettings.razor` does for site settings) |
| ☐ | Crest.Users | `LoginForm*`, `ForgotPasswordForm*`, `RegisterUserForm*`, `ResetPasswordForm*` (driver-built sign-in forms) | 10 | existing+ | `CrestLogin` and the member pages, with a zone modules contribute to (`Captcha`, external providers) instead of display drivers |
| ☐ | Crest.Users | `UserTimeZone.Edit` | 1 | existing+ | `Pages/ProfileSettings.razor`: a time-zone `Select` |
| ☐ | Crest.Users | `{AuthenticatorApp,EmailAuthenticator,SmsAuthenticator}LoginSettings.Edit` | 3 | existing+ | `Pages/LoginSettings.razor`: a section per two-factor method |
| ☐ | Crest.Users | `TwoFactorAuthentication/LoginWith{TwoFactorAuthentication,RecoveryCode}`, `SmsAuthenticator/ValidateCode`, `*AuthenticatorValidation` | 6 | new | `TwoFactorChallenge` (§ New components) |
| ☐ | Crest.Users | `TwoFactorAuthentication/{Index,GenerateRecoveryCodes,ShowRecoveryCodes,DisableTwoFactorAuthentication}`, `AuthenticatorApp/*`, `SmsAuthenticator/Index`, `TwoFactorMethod.*`, `Items/TwoFactorMethod.*` | 18 | new | `TwoFactorSetup` (§ New components) |
| ☑ | Crest.Users | `AdminDashboardUsers`, `AuditTrailAdmin/Index`, `Items/AuditTrailUserEventDetail.DetailAdmin` | 3 | not needed | native `Pages/Dashboard.razor`, `Crest.AuditTrail/blazor-wasm/Pages/AuditTrail.razor`, `AuditRecord.razor` |
| ☑ | Crest.Users | `LoginSettings.Edit`, `LoginSettingsRoles.Edit`, `ExternalLoginSettings.Edit`, `TwoFactorLoginSettings.Edit` | 4 | not needed | native `Pages/LoginSettings.razor` |
| ☑ | Crest.Users | `NavbarUserMenu`, `UserMenu*`, `UserMenuItems-*` | 15 | not needed | native user menus: `AdminTitleBar.razor` (`CrestProfileMenu`), `UserAccountActions.razor`, `Crest.MemberTheme/wasm/Components/MemberLayout.razor` |
| ☑ | Crest.Users | `User.Edit`, `UserName.Edit`, `UserEmail.Edit`, `UserPhoneNumber.Edit`, `UserRoleFields.Edit`, `UserFields.Edit`, `UserButtons`, `UserSaveButtons.Edit` | 8 | not needed | native `Pages/UserEditor.razor` |
| ☑ | Crest.Users | `UserCulture.Edit` | 1 | not needed | native `Pages/ProfileSettings.razor` |
| ☑ | Crest.Users | admin list and detail shapes: `User.{DetailAdmin,SummaryAdmin}`, `UserInfo`, `UserFields`, `UserRoles*`, `UserDisplayName*`, `User*ActionsMenu`, `UsersAdmin*`, `UsersAdminFilters-*`, `UserIndexOptions.Thumbnail` | 27 | not needed | native `Pages/Users.razor`, `Pages/UserEditor.razor`; `UserDisplayName` is only reached through a Razor tag helper |
| ☑ | Crest.Roles | view component `SelectRoles` | 1 | not needed | native `Pages/Roles.razor`, `Pages/UserEditor.razor` |
| ☑ | Crest.OpenId | `Access/Error` | 1 | existing | `CrestErrorPage` (`Crest.Components/Components/Errors/CrestErrorPage.razor`) in Crest.OpenId's site library (views.md wave 2) |
| ☐ | Crest.OpenId | `OpenIdClientSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.Facebook | `FacebookSettings.Edit`, `FacebookLoginSettings.Edit`, `FacebookPixelSettings.Edit` | 3 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.Google | `GoogleAnalyticsSettings.Edit`, `GoogleAuthenticationSettings.Edit`, `GoogleTagManagerSettings.Edit` | 3 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.Twitter | `TwitterSettings.Edit`, `TwitterSigninSettings.Edit` | 2 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.GitHub | `GithubAuthenticationSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.Microsoft.Authentication | `MicrosoftAccountSettings.Edit`, `MicrosoftEntraIDSettings.Edit` | 2 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.ReCaptcha | `ReCaptchaSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |

### Admin screens and settings

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Crest.Admin | `Admin/Index`, `AdminBranding`, `AdminDashboard*`, `AdminSettings.Edit`, `Navbar*`, `VisitSiteNavbarItem` | 9 | not needed | native `Pages/Dashboard.razor`, `Pages/AdminSettings.razor`, `AdminTitleBar.razor`, `AdminDashboard.razor`, `CrestShellSwitcher` |
| ☑ | Crest.AdminMenu | `Menu/*`, `Node/*`, `MenuItem.Tree*`, `Items/{Link,Placeholder}AdminNode.*` | 14 | not needed | native `Pages/AdminMenus.razor` |
| ☑ | Crest.AuditTrail | `Admin/{Index,Display}`, `AuditTrailAdminList*`, `AuditTrailAdminFilters-*`, `AuditTrailEvent*`, `AuditTrailIndexOptions.Thumbnail` | 20 | not needed | native `Crest.AuditTrail/blazor-wasm/Pages/AuditTrail.razor`, `AuditRecord.razor` |
| ☑ | Crest.Settings | `Admin/Index`, `SiteSettings.{Edit,SaveButton}`, `Settings-{Site,Cache,Resources}.Edit`, `Settings-Reload.Wrapper`, `DebugSettings.Edit`, `Shared/DisplayTemplates/{CurrentCulture,RemovableCulture}` | 10 | not needed | native `Pages/GeneralSettings.razor`, `Pages/DebugSettings.razor`, `Pages/Localization.razor` |
| ☑ | Crest.Security | `{ContentSecurityPolicy,PermissionsPolicy,ReferrerPolicy}Settings.Edit` | 3 | not needed | native `Pages/SecurityHeaders.razor` |
| ☑ | Crest.Localization | `AdminCulturePicker`, `LocalizationSettings.Edit` | 2 | not needed | native `AdminTitleBar.razor` (culture selector), `Pages/Localization.razor` |
| ☑ | Crest.DataLocalization | `Admin/{Index,Statistics}` | 2 | not needed | native `Pages/Translations.razor` |
| ☐ | Crest.Email.Azure | `AzureEmailSettings.Edit` | 1 | existing+ | `Pages/EmailSettings.razor`: the Azure provider section (backlog: left) |
| ☐ | Crest.Sms | `Admin/Test` | 1 | new | An SMS test page on the `Pages/EmailSettings.razor` pattern (`/Sms/Test`) |
| ☐ | Crest.Sms | `SmsSettings.Edit`, `TwilioSettings.Edit` | 2 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.Sms.Azure | `AzureSmsSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☑ | Crest.Notifications | `UserNotificationNavbar` | 1 | existing | `CrestPortalNotifications` (`Crest.Components/Components/Files/CrestPortalNotifications.razor`), `Pages/Notifications.razor` |
| ☑ | Crest.Notifications | `UserNotificationPreferencesPart.Edit` | 1 | not needed | native `Pages/ProfileSettings.razor` |
| ☑ | Crest.Features | `Admin/Features` | 1 | not needed | native `Pages/Features.razor` |
| ☑ | Crest.Recipes | `Admin/Index` | 1 | not needed | native `Pages/Recipes.razor` |
| ☐ | Crest.Tenants | `FeatureProfiles/{Create,Edit,Index,EditorTemplates/FeatureProfileViewModel}`, `ProfileFeatureTags` | 5 | new | A `FeatureProfiles` page (views.md port, `/TenantFeatureProfiles`) from `CrestDataGrid` and form inputs |
| ☑ | Crest.Tenants | `Admin/Index`, `Manage*Action*`, `TenantAction*` | 6 | not needed | native `Pages/Tenants.razor`, `Pages/TenantEdit.razor`, `Pages/Features.razor` |
| ☑ | Crest.Themes | `Admin/Index`, `AdminDashboardThemes`, `ThemeEntry*`, `Items/ThemeEntry-*`, `ToggleTheme*` | 12 | not needed | native `Pages/AdminThemes.razor`, `AdminTitleBar.razor` (theme toggle) |
| ☑ | Crest.Deployment | `DeploymentPlan/*`, `Import/*`, `Step/*`, `DeploymentStep.*` | 11 | not needed | native `Pages/DeploymentPlans.razor`, `Pages/DeploymentPlan.razor`, `Pages/DeploymentImport.razor`, `Pages/DeploymentImportJson.razor` |
| ☑ | Crest.Deployment.Remote | `RemoteClient/*`, `RemoteInstance/*` | 6 | not needed | native `Pages/RemoteClients.razor`, `Pages/RemoteInstances.razor` |
| ☑ | Crest.Rules | `Condition.*`, `ConditionsModal`, `Rule.Summary`, `Items/*Condition.Fields.*`, `Items/{ConditionGroup,Rule}.Fields.Summary`, view component `SelectStringOperation` | 41 | not needed | layer rules are edited as JSON conditions on `Pages/Layers.razor` (step 6) |
| ☐ | Crest.Https | `HttpsSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.OpenApi | `OpenApiSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.ReverseProxy | `ReverseProxySettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.Cors | `Admin/Index` | 1 | new | If kept: a CORS policies page from `CrestDataGrid` and form inputs (views.md port) |
| ☐ | Crest.Shortcodes | `ShortcodeModal` (insert a shortcode in the HTML editor) | 1 | existing+ | `HtmlEditor` with a shortcode tool (`CrestHtmlEditorCustomTool`) |
| ☐ | Crest.Shortcodes | `Admin/{Create,Edit,Index}`, `ShortcodeDescriptor*` | 6 | new | A `Shortcodes` page (views.md port; enabled in Venti), templates edited with `CodeEditor` |
| ☐ | Crest.Sitemaps | `Admin/*`, `SitemapIndex/*`, `Source/*`, `SitemapCache/List`, `SitemapSource.*`, `CustomPathSitemapSource.*`, `SitemapPart.Edit` | 17 | new | If kept: `Sitemaps` pages in the module's library (views.md port) |
| ☐ | Crest.Sitemaps | `SitemapsRobotsSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Crest.UrlRewriting | `Admin/*`, `RewriteRule*`, `UrlRedirectRule.Edit`, `UrlRewriteRule.Edit` | 13 | new | If kept: a rewrite rules page in the module's library (views.md port) |
| ☐ | Crest.RateLimits | `Admin/*`, `Limiter/*`, `*RateLimiter.{Edit,SummaryAdmin}`, `RateLimitLimiter*`, `RateLimitPolicy*`, `Items/RateLimit*` | 20 | new | If kept: a rate-limit policies page in the module's library (views.md port) |
| ☐ | Crest.Apis.GraphQL | `Admin/Index` (GraphiQL) | 1 | open | views.md: delete or a static page; the user decides |
| ☑ | Crest.MiniProfiler | `MiniProfiler` | 1 | not needed | a dev-tool script; through views.md's head contribution seam, not a component |

### Shells, themes and layouts

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | TheAdmin | `Hint.Admin` | 1 | existing | `CrestFormField`'s helper text |
| ☑ | TheAdmin | `Message`, `NotifyMessages` | 2 | existing | `CrestNotification` / `NotificationService`, `Alert` (`Crest.Components/Components/CrestAlert`) |
| ☑ | TheAdmin | `Pager`, `Pager_{CurrentPage,First,Gap,Last,Link,Next,PageSizeSelector,Previous}` | 9 | existing | `Pager`, `PagerSlim` (`Crest.Components/Components/Content/Pagers.cs`) |
| ☑ | TheAdmin | `Layout`, `Layout-Login`, `Layout-TwoFactor`, `Navigation-admin`, `NavigationItem*-admin`, `NavigationItemText`, `ToggleTheme` | 8 | not needed | the Blazor admin shell (`CrestAppContainer.razor`, `PrimaryNavMenu.razor`, `AdminTitleBar.razor`); TheAdmin is deleted (views.md wave 4) |
| ☑ | SafeMode | `Layout` | 1 | not needed | deleted with MVC (views.md wave 4) |
| ☑ | Crest.Server (LegacyFrame) | `Layout` | 1 | not needed | deleted with the legacy frame (views.md wave 4) |
| ☑ | Crest.SiteTheme | `Layout` | 1 | not needed | `Layout` (`Crest.SiteTheme/wasm/Components/SiteLayout.cs`) serves the content path; the MVC layout goes after views.md wave 2 |
| ☐ | Crest.Setup | `Setup/Index`, `_Layout` | 2 | open | open in views.md › Setup: the keep list, or a pre-database page |
| ☑ | Crest.Mvc.Web, Crest.Mvc.HelloWorld | `Shared/_Layout`, `Home/Index` | 2 | not needed | MVC samples (views.md › Module deletions) |

### Modules the user decides on

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Crest.Demo | views, shapes and Razor Pages (`Pages/*`, `Drivers/TestContentPartA/*`, `Home/*`, dolls, `Fake`) | 35 | not needed | sample module (views.md › Module deletions; the user decides) |
| ☑ | Crest.AdminDashboard | `Dashboard/{Index,Manage}`, `Dashboard.*Button`, `Dashboard.{DragHandle,Wrapper}`, `DashboardPart.Edit`, `DashboardWidget*`, `TitlePart.DetailAdmin` | 17 | not needed | native `Pages/Dashboard.razor` with regions (views.md › Module deletions; the user decides) |
