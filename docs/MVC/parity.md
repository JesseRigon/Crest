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
- **Reuse first.** The equivalent is a component already in `Grove.Components`, a component
  registered through the component registry (`IComponentRegistry`, bound by
  `DisplayTreeBuilder`), or a native page. A row is **new** only when nothing existing covers it;
  a mode or a binding name added to an existing component is **existing+**, never a second
  component.
- **Liquid templates are not part of the audit**: they stay and are updated to display trees
  separately (blazor-display.md step 7). `LiquidPart`'s Razor templates are in scope (§ Open).

Inventory: the 1,072 `.cshtml` files under `src/` ([views.md](views.md) › The inventory: the
597 driver-shape templates, the 261 `Views/Items/*`, controller views, layouts, Razor Pages and
`_ViewImports`/`_ViewStart`). Fruitful has no `.cshtml`. The 34 files under
`test/Grove.Tests.Pages` are Razor Pages fixtures that test modular Razor Pages; they go with
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
| ☐ | `SettingsForm` | A settings group's form, generated from the group's settings schema (fields, sections, write-only secrets, the "saving reloads the tenant" notice that `Settings-Reload.Wrapper` gave), loaded and saved through the group's settings API; served at `/Settings/{groupId}` for every group without a page of its own. The bespoke settings pages stay where the form has behaviour (test send, certificate pickers). | `Form`, `TextInput`, `TextArea`, `Select` (`Grove.Components/Components/Forms`), `GroveFieldset`, `GroveSwitch`, `GroveAlert` | 31 | Grove.Settings (component in `Grove.Components` beside `ContentEditor`); schemas declared by each settings module (api.md) |
| ☐ | `TwoFactorSetup` | The signed-in user's two-factor page: enabled methods (authenticator app, email, SMS), authenticator enrollment with a QR code and a verification code, phone number confirmation, recovery codes (generate, show once), reset and disable. One component placed by each shell's account page. | `GroveQRCode`, `GroveSecurityCode`, `GroveSteps`, `Dialog` | 18 | Grove.Users |
| ☐ | `ListPartEditor` | A list's contained items in the editor: the children of the item (search, status, paging), reorder when the list is ordered, create a child of an allowed type (the child's editor gets its container, what `ListPart.ContainerId` carried). Only if Grove.Lists is kept (views.md › Module deletions). | `GroveDataGrid`, `Pager`, `ContentEditor` | 8 | Grove.Lists |
| ☐ | `TwoFactorChallenge` | The second sign-in step: the code for the user's method (send again for email and SMS), or a recovery code; part of the login pages of every shell. | `GroveSecurityCode`, `GroveLogin` | 6 | Grove.Users |
| ☐ | `TaxonomyPartEditor` | A taxonomy's term tree: add, rename, nest and reorder terms, each term edited with its own content editor. | `GroveTree`, `Dialog`, `ContentEditor` | 5 | Grove.Taxonomies |
| ☐ | `CodeEditor` | A code text editor with a language mode (HTML, Liquid, JavaScript, CSS, JSON), line numbers and syntax highlighting; an editor component (`Edits`) chosen by the field's editor setting. `JsonEditor`, the Templates page and the Shortcodes page use it too. | `JsonEditor`'s value handling (`FieldEditorBase`) | 4 | Grove.Components |
| ☐ | `SeoMetaPartEditor` | The SEO part: meta title, description, keywords, canonical URL, robots, Open Graph, Twitter card, Google schema JSON, the social image. | `TextInput`, `TextArea`, `MediaPicker`, `GroveTabs`, `CodeEditor` | 4 | Grove.Seo |
| ☐ | `ContentPreviewPane` | The editor's live preview: the unsaved item rendered through the display endpoint beside the editor tree, refreshed as the item changes. | `ContentEditor`'s context, the display tree renderer | 3 | Grove.ContentPreview |
| ☐ | `TaxonomyFieldEditor` | A taxonomy field's terms: a tree of check boxes or radios, or a tags input with creation of new terms when the settings allow. | `GroveTree`, `GroveAutoComplete`, `GroveChipList` | 2 | Grove.Taxonomies |
| ☐ | `GeoPointFieldEditor` | Latitude and longitude, set by typing or by placing a marker on a map. | `GroveNumeric`, `GroveGoogleMap` | 2 | Grove.Spatial |
| ☐ | `HeadLink` | A `<link>` contributed to the page head (`rel`, `type`, `href`, `title`): a list's RSS feed, the RSD discovery link. Renders nothing in the body. | `HeadContent` | 2 | Grove.Components |
| ☐ | `IconPicker` | Pick an icon from the tenant's icon sets, with search; stores the icon key. | the icon catalog behind `Icons.razor`, `GroveIcon`, `GroveDropDown` | 1 | Grove.Icons |
| ☐ | `CulturePicker` | The site's culture switcher: the current item's translations (its localization set), or the site cultures. | `Select`, `Menu` | 1 | Grove.ContentLocalization |
| ☐ | `LocalizationPartEditor` | An item's culture and its translations: the set's other cultures, open or create a translation. | `ContentEditor`'s context, `Badge`, `MenuAction` | 1 | Grove.ContentLocalization |
| ☐ | `ContentDiff` | Two versions of an item side by side, changed values marked; on the audit record and the editor's versions list. | `PropertyList`, the content display API | 1 | Grove.Contents |

## New pages

Ports in [views.md](views.md); each page is a component built from existing primitives
(`GroveDataGrid`, the form inputs, `SettingsForm`, `CodeEditor`). The ones marked "if kept"
exist only if views.md › Module deletions keeps the module.

| ☐ | Page | Module | Templates | Condition |
| --- | --- | --- | ---: | --- |
| ☐ | Sitemaps (sitemaps, sitemap indexes, sources, cache) | Grove.Sitemaps (with `ContentTypesSitemapSource` from Grove.Contents) | 20 | if kept |
| ☐ | Rate-limit policies (policies, limiters per kind) | Grove.RateLimits | 20 | if kept |
| ☐ | URL rewrite rules (rewrite and redirect rules) | Grove.UrlRewriting | 13 | if kept |
| ☐ | Shortcodes (templates list and editor) | Grove.Shortcodes | 6 | enabled in Venti |
| ☐ | Feature profiles (`/TenantFeatureProfiles`) | Grove.Tenants | 5 | — |
| ☐ | CORS policies | Grove.Cors | 1 | if kept |
| ☐ | SMS test (`/Sms/Test`, the email test pattern) | Grove.Sms | 1 | — |

## Open

| ☐ | Templates | Question | Where it is decided |
| --- | --- | --- | --- |
| ☐ | `LiquidPart`, `LiquidPart.Summary`, `LiquidPart.Edit` | A server-side markup leaf rendered by the Liquid manager, or the bodies migrated to `DisplayTreePart` and the part retired. If the part stays, its editor is `CodeEditor` and its display a markup leaf: no new component either way. | [shape-engine.md](shape-engine.md) › Component coverage |
| ☐ | `Grove.Setup` `Setup/Index`, `_Layout` | Setup on the keep list, or a pre-database page (static HTML or static SSR) | [views.md](views.md) › Setup |
| ☐ | `Grove.Apis.GraphQL` `Admin/Index` (GraphiQL) | Delete, or a static page | [views.md](views.md) › Ports |

Fates that hold only on a pending decision: the **not needed** rows of Grove.Demo, Grove.AdminDashboard
and the MVC samples assume their deletion; the "if kept" pages and `ListPartEditor`,
`TaxonomyFieldEditor`, `TaxonomyPartEditor`, `SeoMetaPartEditor` assume their modules stay
([views.md](views.md) › Module deletions).

## Widgets

Every widget of [widgets.md](../widgets.md), with its equivalent.

| | Widget | Fate | Blazor equivalent |
| --- | --- | --- | --- |
| ☑ | Layer widgets | existing | `LayerPlacement` on `Grove.AdminTheme/wasm/Pages/Layers.razor` and the `Layers` recipe step |
| ☑ | `WidgetsListPart` | not needed | an item composes its own body with `DisplayTreePart` (`DisplayTreeEditor`, `Grove.Components/Components/Editors/DisplayTreeEditor.cs`); per-type placement is a `Content__{Type}` template |
| ☑ | `FlowPart` / Container | existing | `Container` (`Grove.Components/Components/Interaction/Fragment.cs`), `Stack` (`GroveStack`), `BagPart`, `DisplayTreePart` |
| ☑ | `BlazorComponent` content type | existing | the `component` template tag, or a layer placement |
| ☑ | `LiquidWidget` | existing | a `Template` content item (a composite when it takes inputs) |
| ☑ | `Widget.Wrapper`, zone and type alternates | existing | component variants and overrides (blazor-display.md § 5a) |
| ☑ | Alias/Autoroute widget shape providers | not needed | internal to the old shapes |
| ☑ | Paragraph | existing | `RichText` (`Grove.Components/Components/Content/RichText.cs`) |
| ☑ | Blockquote | existing | `GroveQuote` (`Grove.Components/Components/GroveQuote`) |
| ☑ | Image (`ImageWidget`) | existing | `Figure` (`Grove.Components/Components/Content/RichText.cs`) with `MediaPicker` |
| ☑ | RawHtml | not needed | templates are data, never code (blazor-display.md § 10); `RichText` for markup |
| ☑ | Container | existing | `Container`, `Stack`, `Card` (`GroveCard`) |
| ☑ | LinkMenuItem | existing | `MenuItem` in `Menu` (`Grove.Components/Components/Interaction/LinkMenu.cs`) |
| ☑ | Form | existing | `Form` (`Grove.Components/Components/Forms/Form.cs`) |
| ☑ | Label | existing | the `Label` parameter of every input (`FormInputBase`) |
| ☑ | Input | existing | `TextInput` (`Grove.Components/Components/Forms/Inputs.cs`) |
| ☑ | TextArea | existing | `TextArea` (`Inputs.cs`) |
| ☑ | Select | existing | `Select` (`Inputs.cs`) |
| ☑ | Button | existing | `Button` (`GroveButton`) |
| ☑ | Validation | existing | each input shows the run's errors for its name (`DisplayState`) |
| ☑ | ValidationSummary | existing | `ValidationSummary` (`Inputs.cs`) |
| ☑ | Visibility rules | existing | every input's `Visible`, bound to a comparison |
| ☑ | ReCaptcha | existing | `Captcha` (`Grove.Components/Components/Forms/Captcha.cs`) |
| ☑ | SearchForm | existing | `SearchBox` (`Grove.Components/Components/Forms/SearchBox.cs`) |
| ☑ | Facebook Chat, Comments, Continue With, Like, Quote, Save, Share | existing | `FacebookPlugin` (`Grove.Facebook/site-wasm/FacebookPlugin.cs`) |
| ☑ | Test-only widget types | not needed | removed with their tests |

## Tracker: shapes every module has

| | Razor templates | n | Fate | Modules |
| --- | --- | ---: | --- | --- |
| ☑ | `_ViewImports`, `_ViewStart` | 82 | not needed: Razor plumbing; each goes with the last view of its module | every module with views |
| ☑ | `NavigationItemText-*.Id` (admin menu icons) | 46 | not needed: the admin navigation is native (`Grove.AdminTheme/wasm/Components/Admin/PrimaryNavMenu.razor`); icons come with the menu API | Admin, AdminDashboard, AuditTrail, AzureAI, ContentTypes, Contents, Cors, Demo, Elasticsearch, Email, Facebook, GitHub, Google, Indexing, Localization, Lucene, Media, Microsoft.Authentication, OpenId, Queries, ReverseProxy, Roles, Search, Settings, Sms, Taxonomies, Tenants, Themes, Twitter, UrlRewriting, Users |
| ☑ | `Items/*DeploymentStep.{Edit,Summary,Thumbnail}` | 157 | not needed: a step's settings are edited as the JSON the plan stores on `Pages/DeploymentPlan.razor` (admin-routes.md); a step type a module adds needs no component | AdminMenu, AzureAI, ContentTypes, Contents, CustomSettings, DataLocalization, Deployment, Elasticsearch, Facebook, Features, Google, Indexing, Layers, Lucene, Media, Microsoft.Authentication, OpenId, Placements, Queries, RateLimits, Roles, Search, Settings, Shortcodes, Sitemaps, Templates, Tenants, Themes, Users |

## Tracker by module

Fates: **existing** (the component or page named covers it now), **existing+** (that component or page covers it after a small addition, named in the row), **new** (§ New components, § New pages), **not needed** (the reason), **open** (§ Open). ☑ = nothing to build; ☐ = work left. `n` counts `.cshtml` files. Paths are under `src/`; `Pages/` is `Grove.AdminTheme/wasm/Pages/`. Shapes every module has are in the table above.

### Content display and editing

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Grove.Contents | `Content.Edit`, `ContentPart.Edit`, `Content.{Publish,SaveDraft,Unpublish,Delete}Button`, `ContentPart-BagPart.Edit` | 7 | existing | `ContentEditor`, `EditorSection` (`Grove.Components/Components/Editors/ContentEditor.cs`), `BagPartEditor`; the actions are `Grove.AdminTheme/wasm/Components/Admin/ContentItemVersionActions.razor` |
| ☑ | Grove.Contents | `ContentItem`, `ContentPart`, `ContentPart.Summary` (item and part wrappers) | 3 | existing | the tree builder's zone mapping and `Container` (`Grove.Components/Components/Interaction/Fragment.cs`) |
| ☑ | Grove.Contents | `Content`, `Content.Summary` | 2 | existing | `Content` (`Grove.SiteTheme/wasm/Components/SiteLayout.cs`); `_Summary` resolves to it by the registry's display-variant rule |
| ☐ | Grove.Contents | `AuditTrailPart.Edit` (a comment saved with the version) | 1 | existing+ | `ContentEditor`: a save-comment input next to the save actions |
| ☐ | Grove.Contents | `CommonPart-{Date,Owner}.Edit` | 2 | existing+ | `DateFieldEditor` and `UserPickerFieldEditor` (`Grove.Components/Components/Editors/FieldEditors.cs`) bound to `CommonPart`'s created date and owner |
| ☐ | Grove.Contents | `Items/AddToDeploymentPlan-*`, `Items/ExportContentToDeploymentTarget-*` (buttons, bulk actions, modals), `Items/Download-Button-Actions.SummaryAdmin` | 9 | existing+ | `Pages/ContentItems.razor`: row and bulk actions opening `Dialog` (`Grove.Components/Components/Interaction/Dialog.razor.cs`) with the plan or target picker |
| ☐ | Grove.Contents | `Stereotype`, `Stereotype.Summary` (retypes a stereotyped item to `Content`) | 2 | existing+ | `Content`, also bound under the name `Stereotype` |
| ☐ | Grove.Contents | `ContentVersionPruningSettings_Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.Contents | `Items/AuditTrailContentEventDiff.DetailAdmin` | 1 | new | `ContentDiff` (§ New components) |
| ☐ | Grove.Contents | `Items/ContentTypesSitemapSource.*` | 3 | new | If Sitemaps is kept: the Sitemaps page (see Grove.Sitemaps) |
| ☐ | Grove.Contents | `Items/ExportContentToDeploymentTargetSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☑ | Grove.Contents | `Admin/{Create,Display,Edit,List}` | 4 | not needed | native `Pages/ContentItems.razor`, `Pages/CreateContentItem.razor`, `Pages/EditContentItem.razor`, `Pages/DisplayContentItem.razor` (views.md wave 1) |
| ☑ | Grove.Contents | `AdminDashboardContents` | 1 | not needed | native `Pages/Dashboard.razor` |
| ☑ | Grove.Contents | `AuditTrailPart.Option` | 1 | not needed | MVC definition editor option; the native definition editor edits settings as JSON (`Pages/ContentTypes.razor`) |
| ☑ | Grove.Contents | `BodyAspect` | 1 | not needed | no driver or code produces the shape; bodies display through `HtmlBodyPart`/`MarkdownBodyPart` |
| ☑ | Grove.Contents | `ContentPart-{FlowPart,WidgetsListPart}.Edit` | 2 | not needed | widgets retired (widgets.md); an item's own body is `DisplayTreeEditor` |
| ☑ | Grove.Contents | `ContentsMetadata` (sets the page title) | 1 | not needed | the site page sets its title (`SitePageBuilder`, `PageTitle`) |
| ☑ | Grove.Contents | `Item/Preview`, `Download/Display` | 2 | not needed | the display endpoint (`?format=html`) and a download API (views.md, api.md) |
| ☑ | Grove.Contents | `Items/AuditTrailContentEvent*`, `Items/AuditTrailContentsAction_SummaryAdmin`, `Items/ContentAuditTrailSettings.Edit` | 6 | not needed | native `Grove.AuditTrail/blazor-wasm/Pages/AuditRecord.razor`, `AuditTrail.razor`, `AuditSettings.razor` |
| ☑ | Grove.Contents | `Items/ContentTypesAdminNode.Fields.Tree*` | 3 | not needed | native `Pages/AdminMenus.razor` |
| ☑ | Grove.Contents | admin list and detail shapes: `*.SummaryAdmin`, `*.DetailAdmin`, `*.Thumbnail`, `ContentsAdminList*`, `ContentsAdminFilters-*`, `Contents*_SummaryAdmin` | 28 | not needed | native `Pages/ContentItems.razor`, `Pages/DisplayContentItem.razor` |
| ☑ | Grove.Contents | view components `DisplayContentItem`, `SelectContentTypes` | 2 | not needed | used only inside MVC views; deleted with their last caller (views.md) |
| ☑ | Grove.ContentFields | `Media-HtmlField.Wrapper` (media picker wiring) | 1 | existing | `HtmlEditor`'s image tool with `MediaPicker` (`Grove.Components/Components/Files/MediaPicker.cs`) |
| ☑ | Grove.ContentFields | displays: `BooleanField`, `ContentPickerField`, `DateField`, `DateTimeField`, `HtmlField`, `LinkField`, `LocalizationSetContentPickerField`, `MultiTextField`, `NumericField`, `TextField`, `TextField-Header.Display`, `TimeField`, `UserPickerField`, `UserPickerField-UserNames.Display`, `YoutubeField` | 15 | existing | the field components of the same names in `Grove.Components/Components/Content/FieldDisplays.cs` (`TextField_Display__Header` for the heading mode) |
| ☑ | Grove.ContentFields | editors: `TextField.Edit`, `BooleanField.Edit`, `ContentPickerField.Edit`, `DateField.Edit`, `DateTimeField.Edit`, `LinkField.Edit`, `NumericField.Edit`, `UserPickerField.Edit`, `HtmlField{,-Wysiwyg,-Trumbowyg}.Edit` | 11 | existing | `TextFieldEditor`, `BooleanFieldEditor`, `ContentPickerFieldEditor`, `DateFieldEditor`, `LinkFieldEditor`, `NumericFieldEditor`, `HtmlEditor` (`Grove.Components/Components/Editors/FieldEditors.cs`), `UserPickerFieldEditor` (`Grove.Components/Components/Editors/UserPickerFieldEditor.cs`) |
| ☐ | Grove.ContentFields | `BooleanField-Switch.Edit` | 1 | existing+ | `BooleanFieldEditor` with a switch mode (`GroveSwitch`) |
| ☐ | Grove.ContentFields | `DateField-Localized.Edit` | 1 | existing+ | `DateFieldEditor` with the culture's date format |
| ☐ | Grove.ContentFields | `HtmlField-Multiline.Edit` | 1 | existing+ | `HtmlEditor` with a plain multi-line mode (`GroveTextArea`) |
| ☐ | Grove.ContentFields | `LocalizationSetContentPickerField.Edit` | 1 | existing+ | `ContentPickerFieldEditor` storing localization set ids |
| ☐ | Grove.ContentFields | `MultiTextField{,-CheckboxList,-Picker}.Edit` | 3 | existing+ | `OptionPickerFieldEditor` also `Edits` `MultiTextField` (check-box list or picker over `GroveCheckBoxList`/`GroveDropDown`) |
| ☐ | Grove.ContentFields | `NumericField-{Range,Select,Slider,Spinner}.Edit` | 4 | existing+ | `NumericFieldEditor` modes over `GroveSlider`, `GroveDropDown`, `GroveNumeric` |
| ☐ | Grove.ContentFields | `TextField-Color.Edit` | 1 | existing+ | `ColorEditor` (`Grove.Components/Components/Editors/ColorEditor.cs`) chosen by the field's editor setting |
| ☐ | Grove.ContentFields | `TextField-{Email,Tel,Url,TextArea,PredefinedList}.Edit` | 5 | existing+ | `TextFieldEditor` follows the field's editor setting: input type, multi-line, or `Select` (`Grove.Components/Components/Forms/Inputs.cs`) over the predefined options |
| ☐ | Grove.ContentFields | `TimeField.Edit` | 1 | existing+ | `DateFieldEditor` also `Edits` `TimeField` (time only, `GroveTimeSpanPicker`) |
| ☐ | Grove.ContentFields | `YoutubeField.Edit` | 1 | existing+ | `TextFieldEditor` on the raw address; the field handler derives the embed address |
| ☐ | Grove.ContentFields | `TextField-IconPicker.Edit` | 1 | new | `IconPicker` (§ New components) |
| ☐ | Grove.ContentFields | `TextField-{Monaco,CodeMirror}.Edit`, `HtmlField-Monaco.Edit` | 3 | new | `CodeEditor` (§ New components) |
| ☑ | Grove.ContentFields | `*.Option`, `*.DisplayOption` (editor and display-mode entries of the MVC field settings) | 47 | not needed | the native definition editor edits field settings as JSON (`Pages/ContentParts.razor`); the editor/display choice reads the registry |
| ☑ | Grove.Flows | `BagPart`, `BagPart.Empty` | 2 | existing | `BagPart`, `BagPart_Empty` (`Grove.Components/Components/Content/FieldDisplays.cs`) |
| ☑ | Grove.Flows | `BagPart{,.Fields,-Blocks}.Edit`, `ContentCard*`, `ContentTypePicker` | 12 | existing | `BagPartEditor` (`Grove.Components/Components/Editors/BagPartEditor.cs`): cards, type picker, reorder, blocks |
| ☑ | Grove.Flows | `BagPart.Option`, `BagPart-Blocks.Option` | 2 | not needed | native definition editor (settings as JSON) |
| ☑ | Grove.Html | `HtmlBodyPart`, `HtmlBodyPart.Summary` | 2 | existing | `HtmlBodyPart` (`Grove.Components/Components/Content/RichText.cs`) |
| ☑ | Grove.Html | `HtmlBodyPart{,-Wysiwyg,-Trumbowyg}.Edit` | 3 | existing | `HtmlEditor` (`Grove.Components/Components/Editors/FieldEditors.cs`) |
| ☑ | Grove.Html | `Media-HtmlBodyPart.Wrapper` | 1 | existing | `HtmlEditor`'s image tool with `MediaPicker` |
| ☐ | Grove.Html | `HtmlBodyPart-Monaco.Edit` | 1 | new | `CodeEditor` |
| ☑ | Grove.Html | `HtmlBodyPart*.Option`, `HtmlBodyPart.DisplayOption` | 5 | not needed | native definition editor |
| ☑ | Grove.Markdown | `MarkdownBodyPart`, `MarkdownBodyPart.Summary`, `MarkdownField` | 3 | existing | `MarkdownBodyPart` (`Grove.Components/Components/Content/RichText.cs`), `MarkdownField` (`Grove.Components/Components/Content/FieldDisplays.cs`) |
| ☑ | Grove.Markdown | `MarkdownBodyPart{,-Wysiwyg}.Edit`, `MarkdownField{,-Wysiwyg}.Edit`, `Media-MarkdownBodyPart.Wrapper` | 5 | existing | `MarkdownEditor` (`Grove.Components/Components/Editors/FieldEditors.cs`) with `MediaPicker` |
| ☑ | Grove.Markdown | `*.Option`, `*.DisplayOption` | 6 | not needed | native definition editor |
| ☑ | Grove.Title | `TitlePart.Edit`, `ContentPart-TitlePart.Edit` | 2 | existing | `TitlePartEditor` (`Grove.Components/Components/Editors/FieldEditors.cs`) |
| ☑ | Grove.Title | `TitlePart`, `TitlePart.Summary` | 2 | existing | `TitlePart`, `TitlePart_Summary` (`Grove.Components/Components/Content/FieldDisplays.cs`) |
| ☑ | Grove.Title | `TitlePart.Option`, `TitlePart.DisplayOption` | 2 | not needed | native definition editor |
| ☑ | Grove.Autoroute | `AutoroutePart.Edit` | 1 | existing | `AutoroutePartEditor` (`Grove.Components/Components/Editors/FieldEditors.cs`) |
| ☐ | Grove.Alias | `AliasPart.Edit` | 1 | existing+ | `TextFieldEditor` also `Edits` `AliasPart` (its `Alias` value) |
| ☑ | Grove.Alias | `AliasPart.Option` | 1 | not needed | native definition editor |
| ☑ | Grove.Media | `MediaField` | 1 | existing | `MediaField` (`Grove.Components/Components/Content/FieldDisplays.cs`) |
| ☑ | Grove.Media | `MediaField{,-Attached,-Gallery}.Edit`, `Shared/MediaFieldEdit{AnchorModal,Localization,MediaTextModal}` | 6 | existing | `MediaFieldEditor` (`Grove.Components/Components/Files/MediaFieldEditor.cs`, `GroveMediaFieldInput.razor`: paths, texts, anchors) |
| ☐ | Grove.Media | `MediaCache/Index` | 1 | existing+ | `Pages/MediaOptions.razor`: the cache section (views.md port) |
| ☐ | Grove.Media | `MediaApiSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☑ | Grove.Media | `Admin/{Index,Options}`, `MediaProfiles/*`, `Shared/MediaStoreEntries` | 6 | not needed | native `Pages/MediaLibrary.razor`, `Pages/MediaOptions.razor`, `Pages/MediaProfiles.razor` |
| ☑ | Grove.Media | `MediaField*.Option`, `MediaField.DisplayOption` | 4 | not needed | native definition editor (settings as JSON) |
| ☐ | Grove.Media.Azure, Grove.Media.AmazonS3 | `Admin/Options` ×2 | 2 | existing+ | `Pages/MediaOptions.razor`: the provider's read-only section |
| ☑ | Grove.Lists | `ListPart` | 1 | existing | `ListPart` (`Grove.Components/Components/Content/FieldDisplays.cs`) with `Pager` |
| ☐ | Grove.Lists | `ListPartFeed.Edit` | 1 | existing+ | the part's values on the editor tree (the `JsonEditor` fallback today), or `TextFieldEditor`/`NumericFieldEditor` bound to them |
| ☐ | Grove.Lists | `ListPart.{ContainerId,Sortable}`, `ListPartDetailAdmin`, `ListPartDetailAdminSearchPanel`, `ListPartHeaderAdmin`, `ListPartNavigationAdmin`, `ListPartSummaryAdmin`, `ContentsAdminList-ListPartFilter` | 8 | new | New (if Lists is kept): `ListPartEditor` (§ New components) |
| ☐ | Grove.Lists | `ListPartFeed` (RSS link), `ListPart.RemotePublishing` (RSD link) | 2 | new | `HeadLink` (§ New components) |
| ☑ | Grove.Lists | `Items/ListsAdminNode.Fields.Tree*` | 3 | not needed | native `Pages/AdminMenus.razor` |
| ☑ | Grove.Lists | `ListPart.DisplayOption` | 1 | not needed | native definition editor |
| ☑ | Grove.Taxonomies | `TaxonomyField`, `TaxonomyPart`, `TaxonomyPart.Empty`, `Term`, `TermItem`, `TermContentItem` | 6 | existing | `TaxonomyField`, `TaxonomyPart`, `TaxonomyPart_Empty` (`Grove.Components/Components/Content/FieldDisplays.cs`) |
| ☐ | Grove.Taxonomies | `ContentsAdminListTaxonomyFilter`, `TaxonomyContentsAdminListSettings.Edit` | 2 | existing+ | `Pages/ContentItems.razor`: a taxonomy filter (the settings name the taxonomies) |
| ☐ | Grove.Taxonomies | `TaxonomyField-Tags.Display` | 1 | existing+ | `TaxonomyField` with a tags mode (`Badge`) |
| ☐ | Grove.Taxonomies | `TermPart` (a term's page: the tagged items) | 1 | existing+ | `ListPart` (`Grove.Components/Components/Content/FieldDisplays.cs`) also bound to `TermPart`, with `Pager` |
| ☐ | Grove.Taxonomies | `TaxonomyField{,-Tags}.Edit` | 2 | new | `TaxonomyFieldEditor` (§ New components) |
| ☐ | Grove.Taxonomies | `TaxonomyPart.Edit`, `Admin/{Create,Edit}`, `Content.TermAdmin`, `ContentPart.TermAdmin` | 5 | new | `TaxonomyPartEditor` (§ New components); a term is edited in `Pages/EditContentItem.razor` |
| ☑ | Grove.Taxonomies | `TaxonomyField*.Option`, `*.DisplayOption` | 4 | not needed | native definition editor |
| ☑ | Grove.Spatial | `GeoPointField` | 1 | existing | `GeoPointField` (`Grove.Components/Components/Content/FieldDisplays.cs`) |
| ☐ | Grove.Spatial | `GeoPointField{,-Leaflet}.Edit` | 2 | new | `GeoPointFieldEditor` (§ New components) |
| ☑ | Grove.Spatial | `GeoPointField*.Option` | 2 | not needed | native definition editor |
| ☐ | Grove.Seo | `RobotsSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.Seo | `SeoMetaPart{,GoogleSchema,OpenGraph,Twitter}.Edit` | 4 | new | `SeoMetaPartEditor` (§ New components) |
| ☐ | Grove.PublishLater | `PublishLaterPart.Edit` | 1 | existing+ | `DateFieldEditor` (`Grove.Components/Components/Editors/FieldEditors.cs`) also `Edits` `PublishLaterPart` (its scheduled UTC date) |
| ☐ | Grove.PublishLater | `PublishLaterPart.SummaryAdmin` (the scheduled-date note) | 1 | existing+ | `Badge` (`Grove.Components/Components/GroveBadge`) bound to `PublishLaterPart` in `/display` and the content list |
| ☑ | Grove.PublishLater | `PublishLaterPart.Option` | 1 | not needed | native definition editor |
| ☐ | Grove.ArchiveLater | `ArchiveLaterPart.Edit` | 1 | existing+ | `DateFieldEditor` (`Grove.Components/Components/Editors/FieldEditors.cs`) also `Edits` `ArchiveLaterPart` (its scheduled UTC date) |
| ☐ | Grove.ArchiveLater | `ArchiveLaterPart.SummaryAdmin` (the scheduled-date note) | 1 | existing+ | `Badge` (`Grove.Components/Components/GroveBadge`) bound to `ArchiveLaterPart` in `/display` and the content list |
| ☑ | Grove.ArchiveLater | `ArchiveLaterPart.Option` | 1 | not needed | native definition editor |
| ☐ | Grove.Liquid | `LiquidPart`, `LiquidPart.Summary`, `LiquidPart.Edit` | 3 | open | open in shape-engine.md › Component coverage: a server-side markup leaf, or migrate the bodies to `DisplayTreePart`; if kept, the editor is `CodeEditor` |
| ☐ | Grove.ContentLocalization | `LocalizationPart.SummaryAdmin{,Links}`, `ContentsAdminList-LocalizationPartFilter`, `Items/ContentsAdminFilters-Culture.Thumbnail` | 4 | existing+ | `Pages/ContentItems.razor`: a culture column and filter |
| ☐ | Grove.ContentLocalization | `ContentCulturePickerSettings.Edit`, `ContentRequestCultureProviderSettings.Edit` | 2 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.ContentLocalization | `ContentCulturePicker` (site) | 1 | new | `CulturePicker` (§ New components) |
| ☐ | Grove.ContentLocalization | `LocalizationPart.Edit` | 1 | new | `LocalizationPartEditor` (§ New components) |
| ☐ | Grove.ContentPreview | `ContentPreview.Button`, `Preview/{Index,Render}` | 3 | new | `ContentPreviewPane` (§ New components) |
| ☑ | Grove.ContentPreview | `Preview/_Layout` | 1 | not needed | the preview renders in the site shell |

### Site navigation and search

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Grove.Menu | `Menu`, `MenuItem`, `MenuItemLink{,-ContentMenuItem,-LinkMenuItem}` | 5 | existing | `Menu`, `MenuItem` (`Grove.Components/Components/Interaction/LinkMenu.cs`) |
| ☐ | Grove.Menu | `MenuItemLink-HtmlMenuItem` | 1 | existing+ | `MenuItem` (`Grove.Components/Components/Interaction/LinkMenu.cs`) with a sanitized markup text |
| ☑ | Grove.Navigation | `Navigation`, `NavigationItem`, `NavigationItemLink`, `NavigationItemText` | 4 | existing | `Menu`, `MenuItem` on the site; `PrimaryNavMenu.razor` in the admin |
| ☑ | Grove.Search | `Search-Form` | 1 | existing | `SearchBox` (`Grove.Components/Components/Forms/SearchBox.cs`) |
| ☑ | Grove.Search | `Search/Search`, `Search-List`, `Search-Results` | 3 | existing | a site page in Grove.Search's site library composed of `SearchBox`, `ItemList` (`Grove.Components/Components/Interaction/ItemList.razor.cs`) and `Pager`, over a search API (api.md) |
| ☐ | Grove.Search | `SearchSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☑ | Grove.Search | `SearchFormPart`, `SearchFormPart.Edit` | 2 | not needed | the part is unused (widgets.md: `SearchBox`); delete it |
| ☐ | Grove.AzureAI | `AzureAISearchDefaultSettings.Edit`, `AzureAISearchQuerySettings.Edit` | 2 | new | `SettingsForm` (§ New components) |
| ☑ | Grove.AzureAI | `AzureAISearchIndexProfile.Edit` | 1 | not needed | native `Pages/Indexes.razor` (profiles as JSON) |
| ☐ | Grove.Lucene | `Admin/Query` | 1 | existing+ | `Pages/Indexes.razor`: a query tab per index, on the `Pages/SqlQuery.razor` pattern (views.md port) |
| ☐ | Grove.Lucene | `LuceneQuerySettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☑ | Grove.Lucene | `*IndexProfile.Edit`, `*IndexProfile.ActionsMenuItems.SummaryAdmin` | 2 | not needed | native `Pages/Indexes.razor` (profiles as JSON) |
| ☑ | Grove.Lucene | `*Query.{Edit,SummaryAdmin,Buttons.SummaryAdmin}`, `Query-*.Link` | 4 | not needed | native `Pages/Queries.razor` (provider metadata as JSON) |
| ☐ | Grove.Elasticsearch | `Admin/{Query,IndexInfo,NotConfigured}` | 3 | existing+ | `Pages/Indexes.razor`: a query tab per index, on the `Pages/SqlQuery.razor` pattern (views.md port) |
| ☐ | Grove.Elasticsearch | `ElasticsearchQuerySettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☑ | Grove.Elasticsearch | `*IndexProfile.Edit`, `*IndexProfile.ActionsMenuItems.SummaryAdmin` | 2 | not needed | native `Pages/Indexes.razor` (profiles as JSON) |
| ☑ | Grove.Elasticsearch | `*Query.{Edit,SummaryAdmin,Buttons.SummaryAdmin}`, `Query-*.Link` | 4 | not needed | native `Pages/Queries.razor` (provider metadata as JSON) |
| ☑ | Grove.Queries | `Admin/*`, `Query*`, `SqlQuery*`, `Query-Sql.Link` | 15 | not needed | native `Pages/Queries.razor`, `Pages/SqlQuery.razor` |

### Access

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Grove.Users | `Account/Login` | 1 | existing | `Pages/Login.razor` over `GroveLogin` (`Grove.Components/Components/GroveLogin`), `Grove.MemberTheme/wasm/Pages/MemberLogin.razor`; moves to Grove.Users (views.md wave 2) |
| ☑ | Grove.Users | `Registration/{Register,RegistrationPending}` | 2 | existing | `Grove.MemberTheme/wasm/Pages/MemberRegister.razor`, generalised into Grove.Users (views.md wave 2) |
| ☑ | Grove.Users | `ResetPassword/*` (4) | 4 | existing | `MemberForgotPassword.razor`, `MemberResetPassword.razor` (`Grove.MemberTheme/wasm/Pages`), generalised into Grove.Users |
| ☐ | Grove.Users | `CustomUserSettings{,.Edit,.SummaryAdmin}` | 3 | existing+ | `Pages/UserEditor.razor`: a section per custom user settings type on `ContentEditor` (as `Pages/CustomSettings.razor` does for site settings) |
| ☐ | Grove.Users | `LoginForm*`, `ForgotPasswordForm*`, `RegisterUserForm*`, `ResetPasswordForm*` (driver-built sign-in forms) | 10 | existing+ | `GroveLogin` and the member pages, with a zone modules contribute to (`Captcha`, external providers) instead of display drivers |
| ☐ | Grove.Users | `UserTimeZone.Edit` | 1 | existing+ | `Pages/ProfileSettings.razor`: a time-zone `Select` |
| ☐ | Grove.Users | `{AuthenticatorApp,EmailAuthenticator,SmsAuthenticator}LoginSettings.Edit` | 3 | existing+ | `Pages/LoginSettings.razor`: a section per two-factor method |
| ☐ | Grove.Users | `TwoFactorAuthentication/LoginWith{TwoFactorAuthentication,RecoveryCode}`, `SmsAuthenticator/ValidateCode`, `*AuthenticatorValidation` | 6 | new | `TwoFactorChallenge` (§ New components) |
| ☐ | Grove.Users | `TwoFactorAuthentication/{Index,GenerateRecoveryCodes,ShowRecoveryCodes,DisableTwoFactorAuthentication}`, `AuthenticatorApp/*`, `SmsAuthenticator/Index`, `TwoFactorMethod.*`, `Items/TwoFactorMethod.*` | 18 | new | `TwoFactorSetup` (§ New components) |
| ☑ | Grove.Users | `AdminDashboardUsers`, `AuditTrailAdmin/Index`, `Items/AuditTrailUserEventDetail.DetailAdmin` | 3 | not needed | native `Pages/Dashboard.razor`, `Grove.AuditTrail/blazor-wasm/Pages/AuditTrail.razor`, `AuditRecord.razor` |
| ☑ | Grove.Users | `LoginSettings.Edit`, `LoginSettingsRoles.Edit`, `ExternalLoginSettings.Edit`, `TwoFactorLoginSettings.Edit` | 4 | not needed | native `Pages/LoginSettings.razor` |
| ☑ | Grove.Users | `NavbarUserMenu`, `UserMenu*`, `UserMenuItems-*` | 15 | not needed | native user menus: `AdminTitleBar.razor` (`GroveProfileMenu`), `UserAccountActions.razor`, `Grove.MemberTheme/wasm/Components/MemberLayout.razor` |
| ☑ | Grove.Users | `User.Edit`, `UserName.Edit`, `UserEmail.Edit`, `UserPhoneNumber.Edit`, `UserRoleFields.Edit`, `UserFields.Edit`, `UserButtons`, `UserSaveButtons.Edit` | 8 | not needed | native `Pages/UserEditor.razor` |
| ☑ | Grove.Users | `UserCulture.Edit` | 1 | not needed | native `Pages/ProfileSettings.razor` |
| ☑ | Grove.Users | admin list and detail shapes: `User.{DetailAdmin,SummaryAdmin}`, `UserInfo`, `UserFields`, `UserRoles*`, `UserDisplayName*`, `User*ActionsMenu`, `UsersAdmin*`, `UsersAdminFilters-*`, `UserIndexOptions.Thumbnail` | 27 | not needed | native `Pages/Users.razor`, `Pages/UserEditor.razor`; `UserDisplayName` is only reached through a Razor tag helper |
| ☑ | Grove.Roles | view component `SelectRoles` | 1 | not needed | native `Pages/Roles.razor`, `Pages/UserEditor.razor` |
| ☑ | Grove.OpenId | `Access/Error` | 1 | existing | `GroveErrorPage` (`Grove.Components/Components/Errors/GroveErrorPage.razor`) in Grove.OpenId's site library (views.md wave 2) |
| ☐ | Grove.OpenId | `OpenIdClientSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.Facebook | `FacebookSettings.Edit`, `FacebookLoginSettings.Edit`, `FacebookPixelSettings.Edit` | 3 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.Google | `GoogleAnalyticsSettings.Edit`, `GoogleAuthenticationSettings.Edit`, `GoogleTagManagerSettings.Edit` | 3 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.Twitter | `TwitterSettings.Edit`, `TwitterSigninSettings.Edit` | 2 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.GitHub | `GithubAuthenticationSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.Microsoft.Authentication | `MicrosoftAccountSettings.Edit`, `MicrosoftEntraIDSettings.Edit` | 2 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.ReCaptcha | `ReCaptchaSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |

### Admin screens and settings

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Grove.Admin | `Admin/Index`, `AdminBranding`, `AdminDashboard*`, `AdminSettings.Edit`, `Navbar*`, `VisitSiteNavbarItem` | 9 | not needed | native `Pages/Dashboard.razor`, `Pages/AdminSettings.razor`, `AdminTitleBar.razor`, `AdminDashboard.razor`, `GroveShellSwitcher` |
| ☑ | Grove.AdminMenu | `Menu/*`, `Node/*`, `MenuItem.Tree*`, `Items/{Link,Placeholder}AdminNode.*` | 14 | not needed | native `Pages/AdminMenus.razor` |
| ☑ | Grove.AuditTrail | `Admin/{Index,Display}`, `AuditTrailAdminList*`, `AuditTrailAdminFilters-*`, `AuditTrailEvent*`, `AuditTrailIndexOptions.Thumbnail` | 20 | not needed | native `Grove.AuditTrail/blazor-wasm/Pages/AuditTrail.razor`, `AuditRecord.razor` |
| ☑ | Grove.Settings | `Admin/Index`, `SiteSettings.{Edit,SaveButton}`, `Settings-{Site,Cache,Resources}.Edit`, `Settings-Reload.Wrapper`, `DebugSettings.Edit`, `Shared/DisplayTemplates/{CurrentCulture,RemovableCulture}` | 10 | not needed | native `Pages/GeneralSettings.razor`, `Pages/DebugSettings.razor`, `Pages/Localization.razor` |
| ☑ | Grove.Security | `{ContentSecurityPolicy,PermissionsPolicy,ReferrerPolicy}Settings.Edit` | 3 | not needed | native `Pages/SecurityHeaders.razor` |
| ☑ | Grove.Localization | `AdminCulturePicker`, `LocalizationSettings.Edit` | 2 | not needed | native `AdminTitleBar.razor` (culture selector), `Pages/Localization.razor` |
| ☑ | Grove.DataLocalization | `Admin/{Index,Statistics}` | 2 | not needed | native `Pages/Translations.razor` |
| ☐ | Grove.Email.Azure | `AzureEmailSettings.Edit` | 1 | existing+ | `Pages/EmailSettings.razor`: the Azure provider section (backlog: left) |
| ☐ | Grove.Sms | `Admin/Test` | 1 | new | An SMS test page on the `Pages/EmailSettings.razor` pattern (`/Sms/Test`) |
| ☐ | Grove.Sms | `SmsSettings.Edit`, `TwilioSettings.Edit` | 2 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.Sms.Azure | `AzureSmsSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☑ | Grove.Notifications | `UserNotificationNavbar` | 1 | existing | `GrovePortalNotifications` (`Grove.Components/Components/Files/GrovePortalNotifications.razor`), `Pages/Notifications.razor` |
| ☑ | Grove.Notifications | `UserNotificationPreferencesPart.Edit` | 1 | not needed | native `Pages/ProfileSettings.razor` |
| ☑ | Grove.Features | `Admin/Features` | 1 | not needed | native `Pages/Features.razor` |
| ☑ | Grove.Recipes | `Admin/Index` | 1 | not needed | native `Pages/Recipes.razor` |
| ☐ | Grove.Tenants | `FeatureProfiles/{Create,Edit,Index,EditorTemplates/FeatureProfileViewModel}`, `ProfileFeatureTags` | 5 | new | A `FeatureProfiles` page (views.md port, `/TenantFeatureProfiles`) from `GroveDataGrid` and form inputs |
| ☑ | Grove.Tenants | `Admin/Index`, `Manage*Action*`, `TenantAction*` | 6 | not needed | native `Pages/Tenants.razor`, `Pages/TenantEdit.razor`, `Pages/Features.razor` |
| ☑ | Grove.Themes | `Admin/Index`, `AdminDashboardThemes`, `ThemeEntry*`, `Items/ThemeEntry-*`, `ToggleTheme*` | 12 | not needed | native `Pages/AdminThemes.razor`, `AdminTitleBar.razor` (theme toggle) |
| ☑ | Grove.Deployment | `DeploymentPlan/*`, `Import/*`, `Step/*`, `DeploymentStep.*` | 11 | not needed | native `Pages/DeploymentPlans.razor`, `Pages/DeploymentPlan.razor`, `Pages/DeploymentImport.razor`, `Pages/DeploymentImportJson.razor` |
| ☑ | Grove.Deployment.Remote | `RemoteClient/*`, `RemoteInstance/*` | 6 | not needed | native `Pages/RemoteClients.razor`, `Pages/RemoteInstances.razor` |
| ☑ | Grove.Rules | `Condition.*`, `ConditionsModal`, `Rule.Summary`, `Items/*Condition.Fields.*`, `Items/{ConditionGroup,Rule}.Fields.Summary`, view component `SelectStringOperation` | 41 | not needed | layer rules are edited as JSON conditions on `Pages/Layers.razor` (step 6) |
| ☐ | Grove.Https | `HttpsSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.OpenApi | `OpenApiSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.ReverseProxy | `ReverseProxySettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.Cors | `Admin/Index` | 1 | new | If kept: a CORS policies page from `GroveDataGrid` and form inputs (views.md port) |
| ☐ | Grove.Shortcodes | `ShortcodeModal` (insert a shortcode in the HTML editor) | 1 | existing+ | `HtmlEditor` with a shortcode tool (`GroveHtmlEditorCustomTool`) |
| ☐ | Grove.Shortcodes | `Admin/{Create,Edit,Index}`, `ShortcodeDescriptor*` | 6 | new | A `Shortcodes` page (views.md port; enabled in Venti), templates edited with `CodeEditor` |
| ☐ | Grove.Sitemaps | `Admin/*`, `SitemapIndex/*`, `Source/*`, `SitemapCache/List`, `SitemapSource.*`, `CustomPathSitemapSource.*`, `SitemapPart.Edit` | 17 | new | If kept: `Sitemaps` pages in the module's library (views.md port) |
| ☐ | Grove.Sitemaps | `SitemapsRobotsSettings.Edit` | 1 | new | `SettingsForm` (§ New components) |
| ☐ | Grove.UrlRewriting | `Admin/*`, `RewriteRule*`, `UrlRedirectRule.Edit`, `UrlRewriteRule.Edit` | 13 | new | If kept: a rewrite rules page in the module's library (views.md port) |
| ☐ | Grove.RateLimits | `Admin/*`, `Limiter/*`, `*RateLimiter.{Edit,SummaryAdmin}`, `RateLimitLimiter*`, `RateLimitPolicy*`, `Items/RateLimit*` | 20 | new | If kept: a rate-limit policies page in the module's library (views.md port) |
| ☐ | Grove.Apis.GraphQL | `Admin/Index` (GraphiQL) | 1 | open | views.md: delete or a static page; the user decides |
| ☑ | Grove.MiniProfiler | `MiniProfiler` | 1 | not needed | a dev-tool script; through views.md's head contribution seam, not a component |

### Shells, themes and layouts

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | TheAdmin | `Hint.Admin` | 1 | existing | `GroveFormField`'s helper text |
| ☑ | TheAdmin | `Message`, `NotifyMessages` | 2 | existing | `GroveNotification` / `NotificationService`, `Alert` (`Grove.Components/Components/GroveAlert`) |
| ☑ | TheAdmin | `Pager`, `Pager_{CurrentPage,First,Gap,Last,Link,Next,PageSizeSelector,Previous}` | 9 | existing | `Pager`, `PagerSlim` (`Grove.Components/Components/Content/Pagers.cs`) |
| ☑ | TheAdmin | `Layout`, `Layout-Login`, `Layout-TwoFactor`, `Navigation-admin`, `NavigationItem*-admin`, `NavigationItemText`, `ToggleTheme` | 8 | not needed | the Blazor admin shell (`GroveAppContainer.razor`, `PrimaryNavMenu.razor`, `AdminTitleBar.razor`); TheAdmin is deleted (views.md wave 4) |
| ☑ | SafeMode | `Layout` | 1 | not needed | deleted with MVC (views.md wave 4) |
| ☑ | Grove.Server (LegacyFrame) | `Layout` | 1 | not needed | deleted with the legacy frame (views.md wave 4) |
| ☑ | Grove.SiteTheme | `Layout` | 1 | not needed | `Layout` (`Grove.SiteTheme/wasm/Components/SiteLayout.cs`) serves the content path; the MVC layout goes after views.md wave 2 |
| ☐ | Grove.Setup | `Setup/Index`, `_Layout` | 2 | open | open in views.md › Setup: the keep list, or a pre-database page |
| ☑ | Grove.Mvc.Web, Grove.Mvc.HelloWorld | `Shared/_Layout`, `Home/Index` | 2 | not needed | MVC samples (views.md › Module deletions) |

### Modules the user decides on

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Grove.Demo | views, shapes and Razor Pages (`Pages/*`, `Drivers/TestContentPartA/*`, `Home/*`, dolls, `Fake`) | 35 | not needed | sample module (views.md › Module deletions; the user decides) |
| ☑ | Grove.AdminDashboard | `Dashboard/{Index,Manage}`, `Dashboard.*Button`, `Dashboard.{DragHandle,Wrapper}`, `DashboardPart.Edit`, `DashboardWidget*`, `TitlePart.DetailAdmin` | 17 | not needed | native `Pages/Dashboard.razor` with regions (views.md › Module deletions; the user decides) |
