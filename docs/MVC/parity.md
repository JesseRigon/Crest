# MVC retirement › Component parity: every Razor template to a Blazor component, or gone

The parity audit of [blazor-display.md](../blazor-display.md) › Rework plan, step 7, and
[shape-engine.md](shape-engine.md) C4: every Razor view and shape template, and every widget
([widgets.md](../widgets.md)), has a Blazor component equivalent or a reason it needs none.
Siblings: [views.md](views.md) (the view surfaces and their ports), [shape-engine.md](shape-engine.md)
(the renderers this audit lets go), [api.md](api.md) (the APIs the pages call), [open-issues.md](open-issues.md) (where the native
screens differ from the MVC ones).

Rules:

- **Boundaries may differ.** One component may cover several shapes (`EmbeddedContentItemsEditor` covers the
  embedding editor, its content cards and its type picker), and a shape may decompose into primitives
  or disappear into the tree builder (item and part wrappers become zones).
- **Reuse first.** The equivalent is a component already in `Grove.Display.Components`, a component
  registered through the component registry (`IComponentRegistry`, bound by
  `DisplayTreeBuilder`), or a native page. A row is **new** only when nothing existing covers it;
  a mode or a binding name added to an existing component is **existing+**, never a second
  component.
- **Liquid templates are not part of the audit**: they stay and are updated to display trees
  separately (blazor-display.md step 7). `LiquidPart`'s Razor templates are in scope: the part is retired and its bodies migrate to
  `DisplayTreeFieldSet`.

Inventory: the 1,072 `.cshtml` files under `src/` ([views.md](views.md) › The inventory: the
597 driver-shape templates, the 261 `Views/Items/*`, controller views, layouts, Razor Pages and
`_ViewImports`/`_ViewStart`). Fruitful has no `.cshtml`. The 34 files under
`test/Grove.Tests.Pages` are Razor Pages fixtures that test modular Razor Pages; they go with
`ModularPageMvcCoreBuilderExtensions` (views.md) and are not counted.

## Summary

| Fate | Templates | |
| --- | ---: | --- |
| Covered by existing components | 198 | 121 as they are (**existing**), 77 once the existing component or page gains a mode, a binding name or a section (**existing+**) |
| Needs a new component or page | 196 | **new**; § New components and § New pages |
| Not needed | 678 | **not needed**: 296 replaced by a native page or the Blazor shell (or with no caller left), 3 index profile query shapes (profiles are JSON), 77 editor/display-mode option entries of the MVC definition editor, 157 deployment step shapes (steps are JSON), 46 admin menu icon shapes, 82 `_ViewImports`/`_ViewStart`, 55 in deleted modules, 3 of the retired `LiquidPart` |
| **Total** | **1072** | every `.cshtml` under `src/` |

Widgets ([widgets.md](../widgets.md)): 26 rows, 22 covered by existing components, 4 not needed, none new (§ Widgets).

## New components

Each is registered (`[BlazorComponent]`) where a template or the editor tree places it: the
editors through `Edits` (an editor for a field's editor setting declares `{FieldType}-{Editor}`,
which the editor tree prefers to the field type's own), `HeadLink`, `CulturePicker`, `ContentDiff`.
The content part and field editors live with the other editors in `Grove.Display.Components`
(`Components/Editors/`), since the editor tree renders in the admin shell whichever modules are
enabled; the data they read and write is the owner module's API (the Owner column). Each builds
on what exists; the column says what. Ordered by the templates each covers.

| ☐ | Component | What it is | Builds on | Templates | Owner |
| --- | --- | --- | --- | ---: | --- |
| ☑ | `SettingsForm` | A settings group's form (`Grove.Display.Components/Components/Editors/SettingsForm.razor`), generated from the group's schema: sections, write-only secrets, selects and multi-selects, lists of lines, nested objects and lists of objects, code and JSON fields, fields shown by another's value, and the "saving reloads the tenant" notice; loaded and saved through `api/grove/settings/groups/{groupId}` (Grove.Infrastructure.Settings' `SettingsGroupsApi`), which answers rule failures as the error contract's map, shown under each field. Served at `/Settings/{groupId}` (`Pages/SettingsGroup.razor`) for every registered group. A module declares a group once, as its settings type (`services.AddSettingsGroup<T>`, a further section with `AddSettingsModel<T>`; api.md › Settings groups). The bespoke settings pages stay where the form has behaviour (test send, certificate pickers). | `GroveTextBox`, `GroveTextArea`, `GroveNumeric`, `GroveSwitch`, `GroveDropDown`, `GrovePassword`, `GroveFieldset`, `GroveAlert`, `ColorEditor`, `CodeEditor` | 49 | Grove.Infrastructure.Settings (component in `Grove.Display.Components` beside `ContentEditor`); schemas declared by each settings module (api.md) |
| ☑ | `TwoFactorSetup` | The signed-in user's two-factor page (`Grove.Access.Users.Components`, `TwoFactor/`): enabled methods (authenticator app, email, SMS), authenticator enrollment with a QR code and a verification code (`AuthenticatorPart`), phone number confirmation (`PhonePart`), the preferred method, forgetting the browser, recovery codes (generate, show once), reset and disable; over `api/grove/two-factor` (Grove.Access.Users' `TwoFactorApi`). On the site page `/TwoFactor`. | `GroveQRCode`, `GroveCard`, `GroveAlert` | 18 | Grove.Access.Users |
| ☑ | `ChildContentItemsFieldSetEditor` | A list's contained items in the editor (`Grove.Display.Components/Components/Editors/ContentFieldSetEditors.cs`): the children of the item (search, status, paging), moved up and down when the list is ordered, a child created as an allowed type on the native create page with `?containerId=` (the create writes `ParentContentItemFieldSet`, the server completes its list type and order); the list's feed values below. Over `api/grove/lists/{id}/items` and `/order` (`HierarchyApi`). | `GroveTextBox`, `GroveDropDown`, `GroveButton`, `GroveNumeric` | 8 | Grove.Contents.Hierarchy (API) |
| ☑ | `TwoFactorChallenge` | The second sign-in step (`Grove.Access.Users.Components`, `SignIn/`): the code for the user's method (sent on request for email and SMS), or a recovery code, over `api/grove/auth/two-factor`; in place on the staff login (`SignInForm`) and on the site pages `/LoginWithTwoFactorAuthentication`, `/LoginWithRecoveryCode`. | `GroveTextBox`, `GroveDropDown` | 6 | Grove.Access.Users |
| ☑ | `TaxonomyFieldSetEditor` | A taxonomy's term tree (`Editors/TaxonomyEditors.cs`): add (at the root or under a term), rename, move, nest, un-nest and remove terms, each term edited with its term type's editor tree; saved with the taxonomy, the server naming the taxonomy on every term. | `EmbeddedContentItemsEditor`'s nested editor trees, `GroveTextBox`, `GroveButton` | 5 | Grove.Contents.Taxonomies |
| ☑ | `CodeEditor` | A code text editor (`Grove.Display.Components/Components/Editors/CodeEditor.cs`): Monaco from Grove.Display.Resources' `monaco-esm` loader, with a language mode (HTML, Liquid, JavaScript, CSS, JSON), line numbers and syntax highlighting; a plain monospace text area until Monaco is loaded. The editor of the `code` property kind; `TextFieldEditor` and `HtmlEditor` render it for the `Monaco` and `CodeMirror` editor settings, `JsonEditor` and the settings form's code and JSON fields use it. | `JsonEditor`'s value handling (`FieldEditorBase`) | 4 | Grove.Display.Components |
| ☑ | `SeoMetaFieldSetEditor` | The SEO part (`Editors/ContentFieldSetEditors.cs`): page title, render switch, description, keywords, canonical URL (checked), robots, custom meta tags (JSON), the social images (`MediaFieldEditor` on the part's media fields), the Open Graph, Twitter card and Google schema (checked) sections the type's part settings show. | `GroveFormField`, `GroveFieldset`, `GroveSwitch`, `CodeEditor`, `MediaFieldEditor` | 4 | Grove.Seo |
| ☑ | `ContentPreviewPane` | The editor's live preview (`Editors/ContentPreviewPane.cs`, opened by the editor's Preview button): the unsaved item rendered by `POST api/grove/content-preview` (`ContentPreviewApi`) as HTML in a sandboxed frame beside the editor tree, refreshed a moment after the item stops changing. | the editor's published content, `IDisplayHtmlRenderer` | 3 | Grove.Contents.Preview (API) |
| ☑ | `TaxonomyFieldEditor` | A taxonomy field's terms (`Editors/TaxonomyEditors.cs`): a tree of check boxes, or radios for a unique field, leaves only when the settings say so; with the `Tags` editor a filtered multi-select with chips and the creation of a tag by name when the field is open (`api/grove/taxonomies/{id}/terms`, `/tags`); the field stores the taxonomy, the terms and, for tags, their names. | `GroveDropDown`, `GroveTextBox` | 2 | Grove.Contents.Taxonomies |
| ☑ | `GeoPointFieldEditor` | Latitude and longitude typed in (bounded), or placed by clicking a map with the field's `Leaflet`/`Map` editor: OpenStreetMap through Leaflet (`LeafletMap`, `Grove.Display.Components/Components/Content/LeafletMap.cs`, the Leaflet files in `Grove.Display.Components/wwwroot/leaflet`, no key), Google Maps when the field's settings name a key (`GeoPointFieldSettings.GoogleMapsApiKey`). | `GroveNumeric`, `LeafletMap`, `GroveGoogleMap` | 2 | Grove.Data.Types.Spatial |
| ☑ | `HeadLink` | A `<link>` contributed to the page head (`rel`, `type`, `href`, `title`; `Grove.Display.Components/Components/Content/HeadLink.cs`); `ChildContentItemsFieldSetFeed` is the list's RSS link (the feed driver puts the link's values on the shape, nothing when the list disables its feed). Renders nothing in the body. | `HeadContent` | 2 | Grove.Display.Components |
| ☑ | `IconPicker` | Pick an icon from the tenant's icon sets, with search; stores the icon key in the text field (`Grove.Display.Icons/Components/IconPickerEditor.cs`, `Edits = ["TextField-IconPicker"]`). | `IconSelector` | 1 | Grove.Display.Icons |
| ☑ | `CulturePicker` | The site's culture switcher (`Grove.Display.Components/Components/Content/CulturePicker.cs`): the site's cultures from `api/grove/content-localization/cultures` (anonymous, the Content Culture Picker feature), each a link to `RedirectToLocalizedContent`, which opens the current page's translation. | links | 1 | Grove.Contents.Localization (API) |
| ☑ | `LocalizationFieldSetEditor` | An item's culture and its translations (`Editors/ContentFieldSetEditors.cs`): the set's other cultures, each opened in its editor or created (`api/grove/content-localization/{id}`, `/localize`). | `GroveButton` | 1 | Grove.Contents.Localization (API) |
| ☑ | `RuleEditor` | A rule's conditions (`Grove.Display.Components/Components/Editors/RuleEditor.cs`): the conditions as cards, each edited by the registered component that `Edits` its condition type (`ConditionGroupEditor` for All and Any with its nested list, `BooleanConditionEditor`, `StringConditionEditor` with `StringOperationPicker` and the site's cultures, roles and content types offered, `JavascriptConditionEditor` in `CodeEditor`, `FlagConditionEditor`; a type without an editor as JSON), reordered, removed, added from the registered types; checked by the server before a save. On `Pages/Layers.razor`. | `CodeEditor`, `GroveDropDown`, `GroveSwitch`, `GroveTextBox`, `GroveButton` | 41 | Grove.Display.Rules (`RulesApi`, `api/grove/rules/conditions`, `/check`) |
| ☑ | `SitemapFieldSetEditor` | The sitemap part (`Editors/ContentFieldSetEditors.cs`): override the sitemap's settings, and then exclude the item, its change frequency and its priority (0.0 to 1.0). | `GroveSwitch`, `GroveDropDown`, `GroveFormField` | 1 | Grove.Seo.Sitemaps |
| ☑ | `ContentDiff` | Two versions of an item side by side (`Grove.Display.Components/Components/Content/ContentDiff.cs`): every differing value by its content path, before and after, added and removed marked, the per-version bookkeeping left out. On the audit trail's content event ("Show the changes", over `api/grove/audit/content/{eventId}/diff`, the version and the one before it). | a table | 1 | Grove.Contents (API) |

## New pages

Ports in [views.md](views.md); each page is a component built from existing primitives
(`GroveDataGrid`, the form inputs, `SettingsForm`, `CodeEditor`).

| ☐ | Page | Module | Templates | Note |
| --- | --- | --- | ---: | --- |
| ☑ | Sitemaps (sitemaps, sitemap indexes, sources, cache) | Grove.Seo.Sitemaps (`Grove.Seo.Sitemaps.BlazorWasm`: `Sitemaps.razor`, `SitemapSources.razor`, `SitemapsCache.razor`; with `ContentTypesSitemapSource` from Grove.Contents) | 20 | `api/grove/sitemaps` |
| ☑ | Route management (the page registry's routes, Autoroute, alias and home routes, rewrite and redirect rules) | Grove.Routes (`Grove.Routes.BlazorWasm` `RouteManagement.razor`, `/Routes`) | 13 | views.md › Routes |
| ☑ | Shortcodes (templates list and editor) | Grove.Shortcodes (`Grove.Shortcodes.BlazorWasm` `ShortcodeTemplates.razor`, `/Shortcodes`) | 6 | `api/grove/shortcodes/templates` |
| ☑ | Feature profiles (`/TenantFeatureProfiles`) | Grove.Infrastructure.Tenants (`Pages/FeatureProfiles.razor`) | 5 | `api/grove/tenants/feature-profiles` |
| ☑ | SMS test (`/Sms/Test`, the email test pattern) | Grove.Communications.Sms (`Grove.Communications.Sms.BlazorWasm` `SmsTest.razor`) | 1 | `api/grove/sms/test` |
| ☑ | Setup (the installation steps, static SSR, pre-database) | Grove.Infrastructure.Setup (`Components/Pages/Installation.razor`) | 2 | views.md › Setup |
| ☑ | GraphiQL (a static page) | Grove.Data.Protocols.GraphQL (`wwwroot/graphiql.html`, framed by `GraphiQL.razor` at `/GraphQL`) | 1 | — |
| ☑ | User audit settings (which user properties the snapshot keeps, and each one's redactor) | Grove.Access.Users | 1 | the settings group `user-audit-trail` (`UserSnapshotSettings`) through `SettingsForm` |

Module fates follow [views.md](views.md) › Module deletions and keeps: the **not needed** rows of
Grove.Demo, Grove.AdminDashboard and the MVC samples are deleted with their modules; the pages
and components of the kept modules are built: Grove.Display.Rules' condition editors are
`RuleEditor` and its condition editors, Grove.Infrastructure.Server.ReverseProxy and
Grove.Data.Protocols.Https a `SettingsForm` group each.

## Widgets

Every widget of [widgets.md](../widgets.md), with its equivalent.

| | Widget | Fate | Blazor equivalent |
| --- | --- | --- | --- |
| ☑ | Layer widgets | existing | `LayerPlacement` on `Grove.Themes.Admin/wasm/Pages/Layers.razor` and the `Layers` recipe step |
| ☑ | `WidgetsListPart` | not needed | an item composes its own body with `DisplayTreeFieldSet` (`DisplayTreeEditor`, `Grove.Display.Components/Components/Editors/DisplayTreeEditor.cs`); per-type placement is a `Content__{Type}` template |
| ☑ | `FlowPart` / Container | existing | `Container` (`Grove.Display.Components/Components/Interaction/Fragment.cs`), `Stack` (`GroveStack`), `EmbeddedContentItems`, `DisplayTreeFieldSet` |
| ☑ | `BlazorComponent` content type | existing | the `component` template tag, or a layer placement |
| ☑ | `LiquidWidget` | existing | a `Template` content item (a composite when it takes inputs) |
| ☑ | `Widget.Wrapper`, zone and type alternates | existing | component variants and overrides (blazor-display.md § 5a) |
| ☑ | Alias/Autoroute widget shape providers | not needed | internal to the old shapes |
| ☑ | Paragraph | existing | `RichText` (`Grove.Display.Components/Components/Content/RichText.cs`) |
| ☑ | Blockquote | existing | `GroveQuote` (`Grove.Display.Components/Components/GroveQuote`) |
| ☑ | Image (`ImageWidget`) | existing | `Figure` (`Grove.Display.Components/Components/Content/RichText.cs`) with `MediaPicker` |
| ☑ | RawHtml | not needed | templates are data, never code (blazor-display.md § 10); `RichText` for markup |
| ☑ | Container | existing | `Container`, `Stack`, `Card` (`GroveCard`) |
| ☑ | LinkMenuItem | existing | `MenuItem` in `Menu` (`Grove.Display.Components/Components/Interaction/LinkMenu.cs`) |
| ☑ | Form | existing | `Form` (`Grove.Display.Components/Components/Forms/Form.cs`) |
| ☑ | Label | existing | the `Label` parameter of every input (`FormInputBase`) |
| ☑ | Input | existing | `TextInput` (`Grove.Display.Components/Components/Forms/Inputs.cs`) |
| ☑ | TextArea | existing | `TextArea` (`Inputs.cs`) |
| ☑ | Select | existing | `Select` (`Inputs.cs`) |
| ☑ | Button | existing | `Button` (`GroveButton`) |
| ☑ | Validation | existing | each input shows the run's errors for its name (`DisplayState`) |
| ☑ | ValidationSummary | existing | `ValidationSummary` (`Inputs.cs`) |
| ☑ | Visibility rules | existing | every input's `Visible`, bound to a comparison |
| ☑ | ReCaptcha | existing | `Captcha` (`Grove.Display.Components/Components/Forms/Captcha.cs`) |
| ☑ | SearchForm | existing | `SearchBox` (`Grove.Display.Components/Components/Forms/SearchBox.cs`) |
| ☑ | Facebook Chat, Comments, Continue With, Like, Quote, Save, Share | existing | `FacebookPlugin` (`Grove.Integrations.Facebook/site-wasm/FacebookPlugin.cs`) |
| ☑ | Test-only widget types | not needed | removed with their tests |

## Tracker: shapes every module has

| | Razor templates | n | Fate | Modules |
| --- | --- | ---: | --- | --- |
| ☑ | `_ViewImports`, `_ViewStart` | 82 | not needed: Razor plumbing; each goes with the last view of its module | every module with views |
| ☑ | `NavigationItemText-*.Id` (admin menu icons) | 46 | not needed: the admin navigation is native (`Grove.Themes.Admin/wasm/Components/Admin/PrimaryNavMenu.razor`); icons come with the menu API; the views are deleted | Admin, AdminDashboard, AuditTrail, AzureAI, ContentTypes, Contents, Cors, Demo, Elasticsearch, Email, Facebook, GitHub, Google, Indexing, Localization, Lucene, Media, Microsoft.Authentication, OpenId, Queries, ReverseProxy, Roles, Search, Settings, Sms, Taxonomies, Tenants, Themes, Twitter, UrlRewriting, Users |
| ☑ | `Items/*DeploymentStep.{Edit,Summary,Thumbnail}` | 157 | not needed: a step's settings are edited as the JSON the plan stores on `Pages/DeploymentPlan.razor` (admin-routes.md); a step type a module adds needs no component | AdminMenu, AzureAI, ContentTypes, Contents, CustomSettings, DataLocalization, Deployment, Elasticsearch, Facebook, Features, Google, Indexing, Layers, Lucene, Media, Microsoft.Authentication, OpenId, Placements, Queries, RateLimits, Roles, Search, Settings, Shortcodes, Sitemaps, Templates, Tenants, Themes, Users |

## Tracker by module

Fates: **existing** (the component or page named covers it now), **existing+** (that component or page covers it after a small addition, named in the row), **new** (§ New components, § New pages), **not needed** (the reason). ☑ = nothing to build; ☐ = work left. `n` counts `.cshtml` files. Paths are under `src/`; `Pages/` is `Grove.Themes.Admin/wasm/Pages/`. Shapes every module has are in the table above.

### Content display and editing

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Grove.Contents | `Content.Edit`, `ContentFieldSet.Edit`, `Content.{Publish,SaveDraft,Unpublish,Delete}Button`, `ContentFieldSet-BagPart.Edit` | 7 | existing | `ContentEditor`, `EditorSection` (`Grove.Display.Components/Components/Editors/ContentEditor.cs`), `EmbeddedContentItemsEditor`; the actions are `Grove.Themes.Admin/wasm/Components/Admin/ContentItemVersionActions.razor` |
| ☑ | Grove.Contents | `ContentItem`, `ContentFieldSet`, `ContentFieldSet.Summary` (item and part wrappers) | 3 | existing | the tree builder's zone mapping; a part with fields and no driver is `ContentFieldSet` (`Grove.Display.Components/Components/Content/ContentFieldSetDisplay.cs`, its `Content` zone; `ContentFieldSet_Summary` by the variant rule); `ContentItem` was only built by the Razor `<contentitem>` tag helper; the templates are deleted |
| ☑ | Grove.Contents | `Content`, `Content.Summary` | 2 | existing | `Content` (`Grove.Themes.Site/wasm/Components/SiteLayout.cs`); `_Summary` resolves to it by the registry's display-variant rule; the templates are deleted |
| ☑ | Grove.Contents | `AuditTrailFieldSet.Edit` (a comment saved with the version) | 1 | existing+ | `TextFieldEditor` also `Edits` `AuditTrailFieldSet`: its comment, shown when the type part's settings ask for it |
| ☑ | Grove.Contents | `CommonFieldSet-{Date,Owner}.Edit` | 2 | existing+ | `DateFieldEditor` and `UserPickerFieldEditor` on the common part (the editor tree adds them when the type part shows them); the content items API takes the creation date and the owner out of `CommonFieldSet` on save, the owner only for a caller with `EditContentOwner` |
| ☑ | Grove.Contents | `Items/AddToDeploymentPlan-*`, `Items/ExportContentToDeploymentTarget-*` (buttons, bulk actions, modals), `Items/Download-Button-Actions.SummaryAdmin` | 9 | existing+ | `Pages/ContentItems.razor`: row and bulk actions opening `Dialog` (`Grove.Display.Components/Components/Interaction/Dialog.razor.cs`) with the plan or target picker |
| ☑ | Grove.Contents | `Stereotype`, `Stereotype.Summary` (retypes a stereotyped item to `Content`) | 2 | existing+ | `Content` (`SiteContent`), also bound under the name `Stereotype` (`SiteStereotype`) |
| ☑ | Grove.Contents | `ContentVersionPruningSettings_Edit` | 1 | new | `SettingsForm`, group `ContentVersionPruningSettings` |
| ☑ | Grove.Contents | `Items/AuditTrailContentEventDiff.DetailAdmin` | 1 | new | `ContentDiff` (§ New components) |
| ☑ | Grove.Contents | `Items/ContentTypesSitemapSource.*` | 3 | new | the content types source editor of `SitemapSources.razor` (Grove.Seo.Sitemaps); the driver and views are deleted |
| ☑ | Grove.Contents | `Items/ExportContentToDeploymentTargetSettings.Edit` | 1 | new | `SettingsForm`, group `ExportContentToDeploymentTarget` |
| ☑ | Grove.Contents | `Admin/{Create,Display,Edit,List}` | 4 | not needed | native `Pages/ContentItems.razor`, `Pages/CreateContentItem.razor`, `Pages/EditContentItem.razor`, `Pages/DisplayContentItem.razor` (views.md wave 1) |
| ☑ | Grove.Contents | `AdminDashboardContents` | 1 | not needed | native `Pages/Dashboard.razor` |
| ☑ | Grove.Contents | `AuditTrailFieldSet.Option` | 1 | not needed | MVC definition editor option; the native definition editor edits settings as JSON (`Pages/ContentTypes.razor`) |
| ☑ | Grove.Contents | `BodyAspect` | 1 | not needed | no driver or code produces the shape; bodies display through `HtmlBodyFieldSet`/`MarkdownBodyFieldSet` |
| ☑ | Grove.Contents | `ContentFieldSet-{FlowPart,WidgetsListPart}.Edit` | 2 | not needed | widgets retired (widgets.md); an item's own body is `DisplayTreeEditor` |
| ☑ | Grove.Contents | `ContentsMetadata` (sets the page title) | 1 | not needed | the site page sets its title (`SitePageBuilder`, `PageTitle`) |
| ☑ | Grove.Contents | `Item/Preview`, `Download/Display` | 2 | not needed | the display endpoint (`?format=html`) and a download API (views.md, api.md) |
| ☑ | Grove.Contents | `Items/AuditTrailContentEvent*`, `Items/AuditTrailContentsAction_SummaryAdmin` | 5 | not needed | native `Grove.AuditTrail/blazor-wasm/Pages/AuditRecord.razor`, `AuditTrail.razor` |
| ☑ | Grove.Contents | `Items/ContentAuditTrailSettings.Edit` | 1 | existing+ | `Grove.AuditTrail/blazor-wasm/Pages/AuditSettings.razor`: a Content section (the excluded content types) served by Grove.Contents' `api/grove/audit/settings/content` |
| ☑ | Grove.Contents | `Items/ContentTypesAdminNode.Fields.Tree*` | 3 | not needed | native `Pages/AdminMenus.razor` |
| ☑ | Grove.Contents | admin list and detail shapes: `*.SummaryAdmin`, `*.DetailAdmin`, `*.Thumbnail`, `ContentsAdminList*`, `ContentsAdminFilters-*`, `Contents*_SummaryAdmin` | 28 | not needed | native `Pages/ContentItems.razor`, `Pages/DisplayContentItem.razor` |
| ☑ | Grove.Contents | view components `DisplayContentItem`, `SelectContentTypes` | 2 | not needed | used only inside MVC views; deleted with their last caller (views.md) |
| ☑ | Grove.Contents.Fields | `Media-HtmlField.Wrapper` (media picker wiring) | 1 | existing | `HtmlEditor`'s image tool with `MediaPicker` (`Grove.Display.Components/Components/Files/MediaPicker.cs`) |
| ☑ | Grove.Contents.Fields | displays: `BooleanField`, `DateField`, `DateTimeField`, `HtmlField`, `LinkField`, `LocalizationSetContentPickerField`, `MultiTextField`, `NumericField`, `TextField`, `TextField-Header.Display`, `TimeField`, `UserPickerField`, `UserPickerField-UserNames.Display`, `YoutubeField` | 14 | existing | the field components of the same names in `Grove.Display.Components/Components/Content/FieldDisplays.cs` (`TextField_Display__Header` for the heading mode, `UserPickerField` for both user display modes); the templates are deleted |
| ☑ | Grove.Contents.Fields | `ContentPickerField` (display and editor) | 2 | merged | the field is retired: content items are a source of `OptionListField`; display `OptionListField` (`FieldDisplays.cs`, from `OptionListFieldDisplayDriver`), editor `OptionListFieldEditor` in `Grove.Contents.OptionPicker.BlazorWasm` (the selection in order with edit links and up/down, search by title, a create link per allowed creatable type) |
| ☑ | Grove.Contents.Fields | editors: `TextField.Edit`, `BooleanField.Edit`, `DateField.Edit`, `DateTimeField.Edit`, `LinkField.Edit`, `NumericField.Edit`, `UserPickerField.Edit`, `HtmlField{,-Wysiwyg,-Trumbowyg}.Edit` | 11 | existing | `TextFieldEditor`, `BooleanFieldEditor`, `DateFieldEditor`, `LinkFieldEditor`, `NumericFieldEditor`, `HtmlEditor` (`Grove.Display.Components/Components/Editors/FieldEditors.cs`), `UserPickerFieldEditor` (`Grove.Display.Components/Components/Editors/UserPickerFieldEditor.cs`) |
| ☑ | Grove.Contents.Fields | `BooleanField-Switch.Edit` | 1 | existing+ | `BooleanFieldEditor` with the `Switch` editor setting (`GroveSwitch`) |
| ☑ | Grove.Contents.Fields | `DateField-Localized.Edit` | 1 | existing+ | `DateFieldEditor` with the `Localized` editor setting: the culture's date format |
| ☑ | Grove.Contents.Fields | `HtmlField-Multiline.Edit` | 1 | existing+ | `HtmlEditor` with the `Multiline` editor setting: a plain multi-line text area |
| ☑ | Grove.Contents.Fields | `LocalizationSetContentPickerField.Edit` | 1 | existing+ | `LocalizationSetPickerFieldEditor`, storing localization set ids (its candidates from `api/grove/content-fields/localization-sets`) |
| ☑ | Grove.Contents.Fields | `MultiTextField{,-CheckboxList,-Picker}.Edit` | 3 | existing+ | `MultiTextFieldEditor`: the field's options as check boxes (`GroveCheckBoxList`), or a multi-select (`GroveDropDown`) with the `Picker` editor setting |
| ☑ | Grove.Contents.Fields | `NumericField-{Range,Select,Slider,Spinner}.Edit` | 4 | existing+ | `NumericFieldEditor`: a spinner (`GroveNumeric`) by default, a slider (`GroveSlider`) for `Range` and `Slider`, the allowed values (`GroveDropDown`) for `Select` |
| ☑ | Grove.Contents.Fields | `TextField-Color.Edit` | 1 | existing+ | `ColorEditor`, which `TextFieldEditor` renders for the `Color` editor setting |
| ☑ | Grove.Contents.Fields | `TextField-{Email,Tel,Url,TextArea,PredefinedList}.Edit` | 5 | existing+ | `TextFieldEditor` follows the field's editor setting: the input type, a text area, or the predefined options (`GroveDropDown` or `GroveRadioButtonList`) |
| ☑ | Grove.Contents.Fields | `TimeField.Edit` | 1 | existing+ | `DateFieldEditor` also `Edits` `TimeField` (time only, `GroveTimeSpanPicker`) |
| ☑ | Grove.Contents.Fields | `YoutubeField.Edit` | 1 | existing+ | `TextFieldEditor` also `Edits` `YoutubeField`: the raw address; `YoutubeFieldHandler` derives the embed address on update |
| ☑ | Grove.Contents.Fields | `TextField-IconPicker.Edit` | 1 | new | `IconPicker` (§ New components) |
| ☑ | Grove.Contents.Fields | `TextField-{Monaco,CodeMirror}.Edit`, `HtmlField-Monaco.Edit` | 3 | new | `CodeEditor`, which `TextFieldEditor` and `HtmlEditor` render for those editor settings |
| ☑ | Grove.Contents.Fields | `*.Option`, `*.DisplayOption` (editor and display-mode entries of the MVC field settings) | 47 | not needed | the native definition editor edits field settings as JSON (`Pages/ContentFieldSets.razor`); the editor/display choice reads the registry |
| ☑ | Grove.Contents.BagParts | `BagPart`, `BagPart.Empty` | 2 | existing | `EmbeddedContentItems` (`Grove.Display.Components/Components/Content/FieldDisplays.cs`); an empty embedding has no shape |
| ☑ | Grove.Contents.BagParts | `BagPart{,.Fields,-Blocks}.Edit`, `ContentCard*`, `ContentTypePicker` | 12 | existing | `EmbeddedContentItemsEditor` (`Grove.Display.Components/Components/Editors/EmbeddedContentItemsEditor.cs`): items, the allowed types' add buttons, reorder |
| ☑ | Grove.Contents.BagParts | `BagPart.Option`, `BagPart-Blocks.Option` | 2 | not needed | the embedding editor on the Content types page |
| ☑ | Grove.Data.Protocols.Html | `HtmlBodyFieldSet`, `HtmlBodyFieldSet.Summary` | 2 | existing | `HtmlBodyFieldSet` (`Grove.Display.Components/Components/Content/RichText.cs`) |
| ☑ | Grove.Data.Protocols.Html | `HtmlBodyFieldSet{,-Wysiwyg,-Trumbowyg}.Edit` | 3 | existing | `HtmlEditor` (`Grove.Display.Components/Components/Editors/FieldEditors.cs`) |
| ☑ | Grove.Data.Protocols.Html | `Media-HtmlBodyFieldSet.Wrapper` | 1 | existing | `HtmlEditor`'s image tool with `MediaPicker` |
| ☑ | Grove.Data.Protocols.Html | `HtmlBodyFieldSet-Monaco.Edit` | 1 | new | `CodeEditor`, which `HtmlEditor` renders for the part's `Monaco` editor setting |
| ☑ | Grove.Data.Protocols.Html | `HtmlBodyFieldSet*.Option`, `HtmlBodyFieldSet.DisplayOption` | 5 | not needed | native definition editor |
| ☑ | Grove.Contents.Markdown | `MarkdownBodyFieldSet`, `MarkdownBodyFieldSet.Summary`, `MarkdownField` | 3 | existing | `MarkdownBodyFieldSet` (`Grove.Display.Components/Components/Content/RichText.cs`), `MarkdownField` (`Grove.Display.Components/Components/Content/FieldDisplays.cs`) |
| ☑ | Grove.Contents.Markdown | `MarkdownBodyFieldSet{,-Wysiwyg}.Edit`, `MarkdownField{,-Wysiwyg}.Edit`, `Media-MarkdownBodyFieldSet.Wrapper` | 5 | existing | `MarkdownEditor` (`Grove.Display.Components/Components/Editors/FieldEditors.cs`) with `MediaPicker` |
| ☑ | Grove.Contents.Markdown | `*.Option`, `*.DisplayOption` | 6 | not needed | native definition editor |
| ☑ | Grove.Contents.Title | `TitleFieldSet.Edit`, `ContentFieldSet-TitleFieldSet.Edit` | 2 | existing | `TitleFieldSetEditor` (`Grove.Display.Components/Components/Editors/FieldEditors.cs`) |
| ☑ | Grove.Contents.Title | `TitleFieldSet`, `TitleFieldSet.Summary` | 2 | existing | `TitleFieldSet`, `Title_Summary` (`Grove.Display.Components/Components/Content/FieldDisplays.cs`) |
| ☑ | Grove.Contents.Title | `TitleFieldSet.Option`, `TitleFieldSet.DisplayOption` | 2 | not needed | native definition editor |
| ☑ | Grove.Routes.Auto | `AutorouteFieldSet.Edit` | 1 | existing | `AutorouteFieldSetEditor` (`Grove.Display.Components/Components/Editors/FieldEditors.cs`) |
| ☑ | Grove.Contents.Alias | `AliasFieldSet.Edit` | 1 | existing+ | `TextFieldEditor` also `Edits` `AliasFieldSet` (its `Alias` value) |
| ☑ | Grove.Contents.Alias | `AliasFieldSet.Option` | 1 | not needed | native definition editor |
| ☑ | Grove.Media | `MediaField` | 1 | existing | `MediaField` (`Grove.Display.Components/Components/Content/FieldDisplays.cs`) |
| ☑ | Grove.Media | `MediaField{,-Attached,-Gallery}.Edit`, `Shared/MediaFieldEdit{AnchorModal,Localization,MediaTextModal}` | 6 | existing | `MediaFieldEditor` (`Grove.Display.Components/Components/Files/MediaFieldEditor.cs`, `GroveMediaFieldInput.razor`: paths, texts, anchors) |
| ☑ | Grove.Media | `MediaCache/Index` | 1 | existing+ | `Pages/MediaOptions.razor`: the cache section (views.md port) |
| ☑ | Grove.Media | `MediaApiSettings.Edit` | 1 | new | `SettingsForm`, group `mediaApi` |
| ☑ | Grove.Media | `Admin/{Index,Options}`, `MediaProfiles/*`, `Shared/MediaStoreEntries` | 6 | not needed | native `Pages/MediaLibrary.razor`, `Pages/MediaOptions.razor`, `Pages/MediaProfiles.razor` |
| ☑ | Grove.Media | `MediaField*.Option`, `MediaField.DisplayOption` | 4 | not needed | native definition editor (settings as JSON) |
| ☑ | Grove.Media.Azure, Grove.Media.AmazonS3 | `Admin/Options` ×2 | 2 | existing+ | `Pages/MediaOptions.razor`: the provider's read-only section |
| ☑ | Grove.Contents.Hierarchy | `ChildContentItemsFieldSet` | 1 | existing | `ChildContentItemsFieldSet` (`Grove.Display.Components/Components/Content/FieldDisplays.cs`) with `Pager` |
| ☑ | Grove.Contents.Hierarchy | `ChildContentItemsFieldSetFeed.Edit` | 1 | existing+ | the feed section of `ChildContentItemsFieldSetEditor` (the part's own values) |
| ☑ | Grove.Contents.Hierarchy | `ChildContentItemsFieldSet.{ContainerId,Sortable}`, `ChildContentItemsFieldSetDetailAdmin`, `ChildContentItemsFieldSetDetailAdminSearchPanel`, `ChildContentItemsFieldSetHeaderAdmin`, `ChildContentItemsFieldSetNavigationAdmin`, `ChildContentItemsFieldSetSummaryAdmin`, `ContentsAdminList-ChildContentItemsFieldSetFilter` | 8 | new | `ChildContentItemsFieldSetEditor` (§ New components) |
| ☑ | Grove.Contents.Hierarchy | `ChildContentItemsFieldSetFeed` (RSS link) | 1 | new | `HeadLink` (§ New components), bound as `ChildContentItemsFieldSetFeed` |
| ☑ | Grove.Contents.Hierarchy | `ChildContentItemsFieldSet.RemotePublishing` (RSD link) | 1 | not needed | deleted with Grove.XmlRpc (views.md › Module deletions and keeps) |
| ☑ | Grove.Contents.Hierarchy | `Items/ListsAdminNode.Fields.Tree*` | 3 | not needed | native `Pages/AdminMenus.razor` |
| ☑ | Grove.Contents.Hierarchy | `ChildContentItemsFieldSet.DisplayOption` | 1 | not needed | native definition editor |
| ☑ | Grove.Contents.Taxonomies | `TaxonomyField`, `TaxonomyFieldSet`, `TaxonomyFieldSet.Empty`, `Term`, `TermItem`, `TermContentItem` | 6 | existing | `TaxonomyField`, `TaxonomyFieldSet` (the term tree), `Taxonomy_Empty` (`Grove.Display.Components/Components/Content/FieldDisplays.cs`); the templates and the Razor platform helpers are deleted |
| ☑ | Grove.Contents.Taxonomies | `ContentsAdminListTaxonomyFilter`, `TaxonomyContentsAdminListSettings.Edit` | 2 | existing+ | `Pages/ContentItems.razor`: a taxonomy filter (the settings name the taxonomies) |
| ☑ | Grove.Contents.Taxonomies | `TaxonomyField-Tags.Display` | 1 | existing+ | `TaxonomyField_Display__Tags`: the terms as linked badges (`GroveBadge`); the tags display driver fills the terms like the default one |
| ☑ | Grove.Contents.Taxonomies | `TermFieldSet` (a term's page: the tagged items) | 1 | existing+ | `ChildContentItemsFieldSet` also bound as `TermFieldSet` (`TermFieldSetDisplay`) |
| ☑ | Grove.Contents.Taxonomies | `TaxonomyField{,-Tags}.Edit` | 2 | new | `TaxonomyFieldEditor` (§ New components) |
| ☑ | Grove.Contents.Taxonomies | `TaxonomyFieldSet.Edit`, `Admin/{Create,Edit}`, `Content.TermAdmin`, `ContentFieldSet.TermAdmin` | 5 | new | `TaxonomyFieldSetEditor` (§ New components): a term is edited in its taxonomy's editor (`Pages/EditContentItem.razor`) |
| ☑ | Grove.Contents.Taxonomies | `TaxonomyField*.Option`, `*.DisplayOption` | 4 | not needed | native definition editor |
| ☑ | Grove.Data.Types.Spatial | `GeoPointField` | 1 | existing | `GeoPointField` (`Grove.Display.Components/Components/Content/FieldDisplays.cs`) |
| ☑ | Grove.Data.Types.Spatial | `GeoPointField{,-Leaflet}.Edit` | 2 | new | `GeoPointFieldEditor` (§ New components) |
| ☑ | Grove.Data.Types.Spatial | `GeoPointField*.Option` | 2 | not needed | native definition editor |
| ☑ | Grove.Seo | `RobotsSettings.Edit` | 1 | new | `SettingsForm`, group `robotsSettings` (with `SitemapsRobotsSettings` as a second section) |
| ☑ | Grove.Seo | `SeoMetaFieldSet{,GoogleSchema,OpenGraph,Twitter}.Edit` | 4 | new | `SeoMetaFieldSetEditor` (§ New components) |
| ☑ | Grove.Contents.PublishLater | `PublishLaterFieldSet.Edit` | 1 | existing+ | `DateFieldEditor` also `Edits` `PublishLaterFieldSet` (its scheduled UTC date) |
| ☑ | Grove.Contents.PublishLater | `PublishLaterFieldSet.SummaryAdmin` (the scheduled-date note) | 1 | existing+ | `PublishLaterFieldSet` (`PublishLaterFieldSetDisplay`): the scheduled date as a badge (`GroveBadge`) |
| ☑ | Grove.Contents.PublishLater | `PublishLaterFieldSet.Option` | 1 | not needed | native definition editor |
| ☑ | Grove.Contents.ArchiveLater | `ArchiveLaterFieldSet.Edit` | 1 | existing+ | `DateFieldEditor` also `Edits` `ArchiveLaterFieldSet` (its scheduled UTC date) |
| ☑ | Grove.Contents.ArchiveLater | `ArchiveLaterFieldSet.SummaryAdmin` (the scheduled-date note) | 1 | existing+ | `ArchiveLaterFieldSet` (`ArchiveLaterFieldSetDisplay`): the scheduled date as a badge |
| ☑ | Grove.Contents.ArchiveLater | `ArchiveLaterFieldSet.Option` | 1 | not needed | native definition editor |
| ☑ | Grove.Display.Liquid | `LiquidPart`, `LiquidPart.Summary`, `LiquidPart.Edit` | 3 | not needed | the part is retired; its bodies are `DisplayTreeFieldSet` trees (Grove.Display.Templates' migration, shape-engine.md › Component coverage) |
| ☑ | Grove.Contents.Localization | `LocalizationFieldSet.SummaryAdmin{,Links}`, `ContentsAdminList-LocalizationFieldSetFilter`, `Items/ContentsAdminFilters-Culture.Thumbnail` | 4 | existing+ | `Pages/ContentItems.razor`: a culture column and filter |
| ☑ | Grove.Contents.Localization | `ContentCulturePickerSettings.Edit`, `ContentRequestCultureProviderSettings.Edit` | 2 | new | `SettingsForm`, groups `ContentCulturePicker` and `ContentRequestCultureProvider` |
| ☑ | Grove.Contents.Localization | `ContentCulturePicker` (site) | 1 | new | `CulturePicker` (§ New components) |
| ☑ | Grove.Contents.Localization | `LocalizationFieldSet.Edit` | 1 | new | `LocalizationFieldSetEditor` (§ New components) |
| ☑ | Grove.Contents.Preview | `ContentPreview.Button`, `Preview/{Index,Render}` | 3 | new | `ContentPreviewPane` (§ New components) |
| ☑ | Grove.Contents.Preview | `Preview/_Layout` | 1 | not needed | the preview renders in the site shell |

### Site navigation and search

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Grove.Contents.SiteMenu | `Menu`, `MenuItem`, `MenuItemLink{,-ContentMenuItem,-LinkMenuItem}` | 5 | existing | `Menu`, `MenuItem` (`Grove.Display.Components/Components/Interaction/LinkMenu.cs`): the menu shapes carry each item's text, link and target; the templates are deleted |
| ☑ | Grove.Contents.SiteMenu | `MenuItemLink-HtmlMenuItem` | 1 | existing+ | `MenuItem` (`LinkMenuItem`) with its `Html` text, sanitized by the menu shapes before it reaches the tree |
| ☑ | Grove.Display.Navigation | `Navigation`, `NavigationItem`, `NavigationItemLink`, `NavigationItemText` | 4 | existing | `Menu`, `MenuItem` on the site; `PrimaryNavMenu.razor` in the admin; the MVC navigation shapes (`NavigationShapes`, `NavigationHelper`) and templates are deleted |
| ☑ | Grove.Search | `Search-Form` | 1 | existing | `SearchBox` (`Grove.Display.Components/Components/Forms/SearchBox.cs`) |
| ☑ | Grove.Search | `Search/Search`, `Search-List`, `Search-Results` | 3 | existing | `SiteSearch.razor` in Grove.Search's site library (`Grove.Search.BlazorWasm`): `SearchBox`, the results list and previous/next paging over `api/grove/search` (views.md › Search) |
| ☑ | Grove.Search | `SearchSettings.Edit` | 1 | new | `SettingsForm`, group `search` |
| ☑ | Grove.Search | `SearchFormPart`, `SearchFormPart.Edit` | 2 | not needed | the part is unused (widgets.md: `SearchBox`); deleted |
| ☑ | Grove.Integrations.AI.Azure | `AzureAISearchDefaultSettings.Edit` | 1 | new | `SettingsForm`, group `azureAISearch` (the API key write-only; unavailable when the host configures the service) |
| ☑ | Grove.Integrations.AI.Azure | `AzureAISearchQuerySettings.Edit` | 1 | not needed | an index profile's query metadata, edited as JSON on `Pages/Indexes.razor` |
| ☑ | Grove.Integrations.AI.Azure | `AzureAISearchIndexProfile.Edit` | 1 | not needed | native `Pages/Indexes.razor` (profiles as JSON) |
| ☑ | Grove.Search.Lucene | `Admin/Query` | 1 | existing+ | `Pages/Indexes.razor`: a query tab per index, on the `Pages/SqlQuery.razor` pattern (views.md port) |
| ☑ | Grove.Search.Lucene | `LuceneQuerySettings.Edit` | 1 | not needed | an index profile's query metadata, edited as JSON on `Pages/Indexes.razor` |
| ☑ | Grove.Search.Lucene | `*IndexProfile.Edit`, `*IndexProfile.ActionsMenuItems.SummaryAdmin` | 2 | not needed | native `Pages/Indexes.razor` (profiles as JSON) |
| ☑ | Grove.Search.Lucene | `*Query.{Edit,SummaryAdmin,Buttons.SummaryAdmin}`, `Query-*.Link` | 4 | not needed | native `Pages/Queries.razor` (provider metadata as JSON) |
| ☑ | Grove.Search.Elasticsearch | `Admin/{Query,IndexInfo,NotConfigured}` | 3 | existing+ | `Pages/Indexes.razor`: a query tab per index, on the `Pages/SqlQuery.razor` pattern (views.md port) |
| ☑ | Grove.Search.Elasticsearch | `ElasticsearchQuerySettings.Edit` | 1 | not needed | an index profile's query metadata, edited as JSON on `Pages/Indexes.razor` |
| ☑ | Grove.Search.Elasticsearch | `*IndexProfile.Edit`, `*IndexProfile.ActionsMenuItems.SummaryAdmin` | 2 | not needed | native `Pages/Indexes.razor` (profiles as JSON) |
| ☑ | Grove.Search.Elasticsearch | `*Query.{Edit,SummaryAdmin,Buttons.SummaryAdmin}`, `Query-*.Link` | 4 | not needed | native `Pages/Queries.razor` (provider metadata as JSON) |
| ☑ | Grove.Data.Queries | `Admin/*`, `Query*`, `SqlQuery*`, `Query-Sql.Link` | 15 | not needed | native `Pages/Queries.razor`, `Pages/SqlQuery.razor` |

### Access

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Grove.Access.Users | `Account/Login` | 1 | existing | the admin theme's `Pages/Login.razor` over Grove.Access.Users' `SignInForm` (`Grove.Access.Users.Components`); the member portal's `MemberLogin.razor` (`Grove.Access.Members/member-wasm`) |
| ☑ | Grove.Access.Users | `Registration/{Register,RegistrationPending}` | 2 | existing | `RegistrationForm` on the site pages `/Register`, `/RegistrationPending` (`Grove.Access.Users/site-wasm`); the portal's `MemberRegister.razor` |
| ☑ | Grove.Access.Users | `ResetPassword/*` (4) | 4 | existing | `PasswordRecoveryForm`, `PasswordResetForm` on the site pages `/ForgotPassword`, `/ResetPassword` and their confirmations (`Grove.Access.Users/site-wasm`); the portal's `MemberForgotPassword.razor`, `MemberResetPassword.razor` |
| ☑ | Grove.Access.Users | `CustomUserSettings{,.Edit,.SummaryAdmin}` | 3 | existing+ | `Pages/UserEditor.razor`: a section per custom user settings type on `ContentEditor` (as `Pages/CustomSettings.razor` does for site settings) |
| ☑ | Grove.Access.Users | `LoginForm*`, `ForgotPasswordForm*`, `RegisterUserForm*`, `ResetPasswordForm*` (driver-built sign-in forms) | 10 | existing+ | `SignInForm`, `RegistrationForm`, `PasswordRecoveryForm`, `PasswordResetForm` with `AccessFormExtensions`: what features contribute through `ILoginExtensionProvider` (links, a button per external provider, the `CaptchaChallenge` section) instead of display drivers |
| ☑ | Grove.Access.Users | `UserTimeZone.Edit` | 1 | existing+ | `Pages/ProfileSettings.razor`: a time-zone `Select` |
| ☑ | Grove.Access.Users | `{AuthenticatorApp,EmailAuthenticator,SmsAuthenticator}LoginSettings.Edit` | 3 | existing+ | `Pages/LoginSettings.razor`: a section per two-factor method |
| ☑ | Grove.Access.Users | `TwoFactorAuthentication/LoginWith{TwoFactorAuthentication,RecoveryCode}`, `SmsAuthenticator/ValidateCode`, `*AuthenticatorValidation` | 6 | new | `TwoFactorChallenge` (§ New components) |
| ☑ | Grove.Access.Users | `TwoFactorAuthentication/{Index,GenerateRecoveryCodes,ShowRecoveryCodes,DisableTwoFactorAuthentication}`, `AuthenticatorApp/*`, `SmsAuthenticator/Index`, `TwoFactorMethod.*`, `Items/TwoFactorMethod.*` | 18 | new | `TwoFactorSetup` (§ New components) |
| ☑ | Grove.Access.Users | `AdminDashboardUsers`, `Items/AuditTrailUserEventDetail.DetailAdmin` | 2 | not needed | native `Pages/Dashboard.razor`, `Grove.AuditTrail/blazor-wasm/Pages/AuditRecord.razor` |
| ☑ | Grove.Access.Users | `AuditTrailAdmin/Index` (user-snapshot redactor settings) | 1 | new | User audit settings (§ New pages) |
| ☑ | Grove.Access.Users | `LoginSettings.Edit`, `ExternalLoginSettings.Edit`, `TwoFactorLoginSettings.Edit` | 3 | not needed | native `Pages/LoginSettings.razor` |
| ☑ | Grove.Access.Users | `LoginSettingsRoles.Edit` | 1 | existing+ | `Pages/LoginSettings.razor`: the roles two-factor authentication is required for, in the two-factor area (`api/grove/settings/login/two-factor`) |
| ☑ | Grove.Access.Users | `NavbarUserMenu`, `UserMenu*`, `UserMenuItems-*` | 15 | not needed | native user menus: `AdminTitleBar.razor` (`GroveProfileMenu`), `UserAccountActions.razor`, `Grove.Themes.Members/wasm/Components/MemberLayout.razor` |
| ☑ | Grove.Access.Users | `User.Edit`, `UserName.Edit`, `UserEmail.Edit`, `UserPhoneNumber.Edit`, `UserRoleFields.Edit`, `UserFields.Edit`, `UserButtons`, `UserSaveButtons.Edit` | 8 | not needed | native `Pages/UserEditor.razor` |
| ☑ | Grove.Access.Users | `UserCulture.Edit` | 1 | not needed | native `Pages/ProfileSettings.razor` |
| ☑ | Grove.Access.Users | admin list and detail shapes: `User.{DetailAdmin,SummaryAdmin}`, `UserInfo`, `UserFields`, `UserRoles*`, `UserDisplayName*`, `User*ActionsMenu`, `UsersAdmin*`, `UsersAdminFilters-*`, `UserIndexOptions.Thumbnail` | 27 | not needed | native `Pages/Users.razor`, `Pages/UserEditor.razor`; the templates, `UserDisplayNameShapeTableProvider` and the Razor user helpers are deleted (`UserDisplayName` was only reached through a Razor tag helper) |
| ☑ | Grove.Access.Roles | view component `SelectRoles` | 1 | not needed | native `Pages/Roles.razor`, `Pages/UserEditor.razor`; the view component and the MVC role scripts are deleted |
| ☑ | Grove.Access.OpenId | `Access/Error` | 1 | existing | the site page `/connect/error` (`OpenIdError.razor`, Grove.Access.OpenId's `site-wasm`): the protocol error and its description |
| ☑ | Grove.Access.OpenId | `OpenIdClientSettings.Edit` | 1 | new | `SettingsForm`, group `Grove.Access.OpenId.Client` (the client service checks the settings on save) |
| ☑ | Grove.Integrations.Facebook | `FacebookSettings.Edit`, `FacebookLoginSettings.Edit`, `FacebookPixelSettings.Edit` | 3 | new | `SettingsForm`, one group per settings type |
| ☑ | Grove.Integrations.Google | `GoogleAnalyticsSettings.Edit`, `GoogleAuthenticationSettings.Edit`, `GoogleTagManagerSettings.Edit` | 3 | new | `SettingsForm`, one group per settings type |
| ☑ | Grove.Integrations.Twitter | `TwitterSettings.Edit`, `TwitterSigninSettings.Edit` | 2 | new | the `x` connection on the Connections screen (app credentials and the posting grant); `SettingsForm` for the sign-in options |
| ☑ | Grove.Integrations.GitHub | `GithubAuthenticationSettings.Edit` | 1 | new | `SettingsForm` |
| ☑ | Grove.Integrations.Microsoft | `MicrosoftAccountSettings.Edit`, `MicrosoftEntraIDSettings.Edit` | 2 | new | `SettingsForm`, one group per settings type |
| ☑ | Grove.Security.ReCaptcha | `ReCaptchaSettings.Edit` | 1 | new | `SettingsForm`, group `recaptcha` |

### Admin screens and settings

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Grove.Infrastructure.Shells.Admin | `Admin/Index`, `AdminBranding`, `AdminDashboard*`, `AdminSettings.Edit`, `Navbar*`, `VisitSiteNavbarItem` | 9 | not needed | native `Pages/Dashboard.razor`, `Pages/AdminSettings.razor`, `AdminTitleBar.razor`, `AdminDashboard.razor`, `GroveShellSwitcher`; the navbar drivers (visit site, notifications, admin culture picker) and the Liquid `Navbar` function are deleted, the `Navbar` model goes with Grove.Themes.TheAdmin's toggle driver |
| ☑ | Grove.Contents.AdminMenu | `Menu/*`, `Node/*`, `MenuItem.Tree*`, `Items/{Link,Placeholder}AdminNode.*` | 14 | not needed | native `Pages/AdminMenus.razor` |
| ☑ | Grove.AuditTrail | `Admin/{Index,Display}`, `AuditTrailAdminList*`, `AuditTrailAdminFilters-*`, `AuditTrailEvent*`, `AuditTrailIndexOptions.Thumbnail` | 20 | not needed | native `Grove.AuditTrail/blazor-wasm/Pages/AuditTrail.razor`, `AuditRecord.razor` |
| ☑ | Grove.Infrastructure.Settings | `Admin/Index`, `SiteSettings.{Edit,SaveButton}`, `Settings-{Site,Cache,Resources}.Edit`, `Settings-Reload.Wrapper`, `DebugSettings.Edit`, `Shared/DisplayTemplates/{CurrentCulture,RemovableCulture}` | 10 | not needed | native `Pages/GeneralSettings.razor`, `Pages/DebugSettings.razor`, `Pages/Localization.razor` |
| ☑ | Grove.Security | `{ContentSecurityPolicy,PermissionsPolicy,ReferrerPolicy}Settings.Edit` | 3 | not needed | native `Pages/SecurityHeaders.razor` |
| ☑ | Grove.Data.Localization | `AdminCulturePicker`, `LocalizationSettings.Edit` | 2 | not needed | native `AdminTitleBar.razor` (culture selector), `Pages/Localization.razor` |
| ☑ | Grove.Data.Localization.Translations | `Admin/{Index,Statistics}` | 2 | not needed | native `Pages/Translations.razor` |
| ☑ | Grove.Integrations.Microsoft (Azure e-mail) | `AzureEmailSettings.Edit` | 1 | existing+ | `SettingsForm` group `email`: the Azure section; the connection string on the Connections screen |
| ☑ | Grove.Communications.Sms | `Admin/Test` | 1 | new | `SmsTest.razor` (`/Sms/Test`, Grove.Communications.Sms.BlazorWasm) on the `EmailTest.razor` pattern (Communications section) |
| ☑ | Grove.Communications.Sms | `SmsSettings.Edit`, `TwilioSettings.Edit` | 2 | new | `SettingsForm`, group `sms`: the default provider, a section per provider (`AddSettingsModel`) |
| ☑ | Grove.Integrations.Microsoft (Azure SMS) | `AzureSmsSettings.Edit` | 1 | new | `SettingsForm`, group `sms`: the default provider, a section per provider (`AddSettingsModel`) |
| ☑ | Grove.Communications | `UserNotificationNavbar` | 1 | existing | `GrovePortalNotifications` (`Grove.Display.Components/Components/Files/GrovePortalNotifications.razor`), `Pages/Notifications.razor`; the navbar driver and the module's MVC scripts are deleted |
| ☑ | Grove.Communications | `UserNotificationPreferencesFieldSet.Edit` | 1 | existing+ | `Pages/ProfileSettings.razor` and `Pages/UserEditor.razor`: a notification methods section (`NotificationPreferences`, Grove.Communications' `api/grove/notifications/preferences`) |
| ☑ | Grove.Infrastructure.Features | `Admin/Features` | 1 | not needed | native `Pages/Features.razor` |
| ☑ | Grove.Infrastructure.Recipes | `Admin/Index` | 1 | not needed | native `Pages/Recipes.razor` |
| ☑ | Grove.Infrastructure.Tenants | `FeatureProfiles/{Create,Edit,Index,EditorTemplates/FeatureProfileViewModel}`, `ProfileFeatureTags` | 5 | new | `Pages/FeatureProfiles.razor` (`/TenantFeatureProfiles`) from `GroveDataGrid` and form inputs |
| ☑ | Grove.Infrastructure.Tenants | `Admin/Index`, `Manage*Action*`, `TenantAction*` | 6 | not needed | native `Pages/Tenants.razor`, `Pages/TenantEdit.razor`, `Pages/Features.razor` |
| ☑ | Grove.Themes | `Admin/Index`, `AdminDashboardThemes`, `ThemeEntry*`, `Items/ThemeEntry-*`, `ToggleTheme*` | 12 | not needed | native `Pages/AdminThemes.razor`, `AdminTitleBar.razor` (theme toggle) |
| ☑ | Grove.Deployment | `DeploymentPlan/*`, `Import/*`, `Step/*`, `DeploymentStep.*` | 11 | not needed | native `Pages/DeploymentPlans.razor`, `Pages/DeploymentPlan.razor`, `Pages/DeploymentImport.razor`, `Pages/DeploymentImportJson.razor` |
| ☑ | Grove.Deployment.Remote | `RemoteClient/*`, `RemoteInstance/*` | 6 | not needed | native `Pages/RemoteClients.razor`, `Pages/RemoteInstances.razor` |
| ☑ | Grove.Display.Rules | `Condition.*`, `ConditionsModal`, `Rule.Summary`, `Items/*Condition.Fields.*`, `Items/{ConditionGroup,Rule}.Fields.Summary`, view component `SelectStringOperation` | 41 | new | `RuleEditor` and the condition editors (§ New components) on `Pages/Layers.razor`; `SelectStringOperation` is `StringOperationPicker`; the condition display drivers, views, view models and scripts are deleted (views.md › Module deletions and keeps) |
| ☑ | Grove.Data.Protocols.Https | `HttpsSettings.Edit` | 1 | new | `SettingsForm`, group `Https` |
| ☑ | Grove.Data.Protocols.OpenApi | `OpenApiSettings.Edit` | 1 | new | `SettingsForm`, group `openapi` |
| ☑ | Grove.Infrastructure.Server.ReverseProxy | `ReverseProxySettings.Edit` | 1 | new | `SettingsForm`, group `ReverseProxy` (the forwarded headers as switches) |
| ☑ | Grove.Security.Cors | `Admin/Index` | 1 | new | `SettingsForm`, group `cors`: the policies as a list of objects, stored through `CorsService` |
| ☑ | Grove.Shortcodes | `ShortcodeModal` (insert a shortcode in the HTML editor) | 1 | existing+ | `HtmlEditor` and `MarkdownEditor` with a shortcode list (`GroveDropDown`) over `api/grove/shortcodes` (Grove.Shortcodes' `ShortcodesApi`) |
| ☑ | Grove.Shortcodes | `Admin/{Create,Edit,Index}`, `ShortcodeDescriptor*` | 6 | new | `ShortcodeTemplates.razor` (`/Shortcodes`, Grove.Shortcodes.BlazorWasm), templates edited with `CodeEditor`; the descriptors are listed by `HtmlEditor`'s and `MarkdownEditor`'s shortcode list over `api/grove/shortcodes`, so the `ShortcodeDescriptor*` shapes, their driver, the modal's scripts and the Razor helper are deleted |
| ☑ | Grove.Seo.Sitemaps | `Admin/*`, `SitemapIndex/*`, `Source/*`, `SitemapCache/List`, `SitemapSource.*`, `CustomPathSitemapSource.*`, `SitemapFieldSet.Edit` | 17 | new | the sitemaps pages in Grove.Seo.Sitemaps.BlazorWasm (views.md port); `SitemapFieldSetEditor` (§ New components) |
| ☑ | Grove.Seo.Sitemaps | `SitemapsRobotsSettings.Edit` | 1 | new | `SettingsForm`, group `robotsSettings` (with `SitemapsRobotsSettings` as a second section) |
| ☑ | Grove.UrlRewriting (now Grove.Routes) | `Admin/*`, `RewriteRule*`, `UrlRedirectRule.Edit`, `UrlRewriteRule.Edit` | 13 | new | `RouteManagement.razor`, the rules tab (§ New pages; views.md › Routes) |
| ☑ | Grove.Security.RateLimits | `Admin/*`, `Limiter/*`, `*RateLimiter.{Edit,SummaryAdmin}`, `RateLimitLimiter*`, `RateLimitPolicy*`, `Items/RateLimit*` | 20 | new | `SettingsForm`, group `ratelimits`: the policies and their limiters as lists of objects over the policy store; a policy is enabled, disabled, added and removed in the list |
| ☑ | Grove.Data.Protocols.GraphQL | `Admin/Index` (GraphiQL) | 1 | new | the static `graphiql.html` (§ New pages) |
| ☐ | Grove.Diagnostics.MiniProfiler | `MiniProfiler` | 1 | not needed | a dev-tool script; through views.md's head contribution seam, not a component; the view and `MiniProfilerFilter` stay until the seam exists |

### Shells, themes and layouts

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Grove.Themes.TheAdmin | `Hint.Admin` | 1 | existing | `GroveFormField`'s helper text |
| ☑ | Grove.Themes.TheAdmin | `Message`, `NotifyMessages` | 2 | existing | `GroveNotification` / `NotificationService`, `Alert` (`Grove.Display.Components/Components/GroveAlert`) |
| ☑ | Grove.Themes.TheAdmin | `Pager`, `Pager_{CurrentPage,First,Gap,Last,Link,Next,PageSizeSelector,Previous}` | 9 | existing | `Pager`, `PagerSlim` (`Grove.Display.Components/Components/Content/Pagers.cs`) |
| ☑ | Grove.Themes.TheAdmin | `Layout`, `Layout-Login`, `Layout-TwoFactor`, `Navigation-admin`, `NavigationItem*-admin`, `NavigationItemText`, `ToggleTheme` | 8 | not needed | the Blazor admin shell (`GroveAppContainer.razor`, `PrimaryNavMenu.razor`, `AdminTitleBar.razor`); Grove.Themes.TheAdmin is deleted (views.md wave 4) |
| ☑ | Grove.Infrastructure.SafeMode | `Layout` | 1 | not needed | deleted with MVC (views.md wave 4) |
| ☑ | Grove.Infrastructure.Server (LegacyFrame) | `Layout` | 1 | not needed | deleted with the legacy frame (views.md wave 4) |
| ☑ | Grove.Themes.Site | `Layout` | 1 | not needed | `Layout` (`Grove.Themes.Site/wasm/Components/SiteLayout.cs`) serves the content path; the MVC layout is deleted |
| ☑ | Grove.Infrastructure.Setup | `Setup/Index`, `_Layout` | 2 | new | `SetupDocument` and `Installation.razor`, static SSR (§ New pages; views.md › Setup) |
| ☑ | Grove.Mvc.Web, Grove.Mvc.HelloWorld | `Shared/_Layout`, `Home/Index` | 2 | not needed | MVC samples, deleted (views.md › Module deletions and keeps) |

### Deleted modules

| | Module | Razor templates | n | Fate | Blazor equivalent |
| --- | --- | --- | ---: | --- | --- |
| ☑ | Grove.Demo | views, shapes and Razor Pages (`Pages/*`, `Drivers/TestContentFieldSetA/*`, `Home/*`, dolls, `Fake`) | 35 | not needed | sample module, deleted (views.md › Module deletions and keeps) |
| ☑ | Grove.AdminDashboard | `Dashboard/{Index,Manage}`, `Dashboard.*Button`, `Dashboard.{DragHandle,Wrapper}`, `DashboardPart.Edit`, `DashboardWidget*`, `TitleFieldSet.DetailAdmin` | 17 | not needed | deleted; native `Pages/Dashboard.razor` with regions (views.md › Module deletions and keeps) |
