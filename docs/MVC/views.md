# MVC retirement › Views: every MVC view surface to a Blazor component, or gone

Workstream (a) of the MVC retirement, done together with moving the APIs out of Grove.Server
([server.md](../server.md)). This document is the tracker for every remaining **view surface**:
controllers that return views, controller views, Razor Pages, view components, the themes and
layouts MVC renders pages into, and the result filters that inject into those layouts. The
sibling documents cover the rest:

- [shape-engine.md](shape-engine.md) (workstream b): the shape engine - display drivers, shape
  templates (`*.cshtml` named after shapes, `Views/Items/*`), `IHtmlDisplay`, the markup
  fallback. About 860 of the 1,072 `.cshtml` files are shape templates and are tracked there,
  not here; this document only lists which ports depend on which shape components.
- [api.md](api.md) (workstream c): the API layer. Every port here needs an API; where one does
  not exist yet it is named here as a dependency on api.md.
- [open-issues.md](open-issues.md): behaviour the native screens do not yet match, and the
  ports' follow-ups.

Status: planning. Nothing in this document is built yet unless marked ☑. Sizes:
**S** (an hour or two: a page over an existing API), **M** (a page plus an API), **L** (several
pages, an API and shape components), **XL** (a subsystem).

## Principles

- **The MVC layer goes**, in three workstreams: (a) views to Blazor components (this document),
  (b) the shape engine, (c) the API layer.
- **A port is a page plus an API call**, nothing server-side in the page. The API is declared by
  the module that owns the screen's function and registered in Grove.Server's API registry
  ([architecture.md › Modules declare, Grove.Server registers and serves](../architecture.md#modules-declare-groveserver-registers-and-serves),
  [api.md](api.md) § Placement); the page is declared in the page registry the same way.
- **Access screens live with the Access modules** (Grove.Users, Grove.Roles, Grove.Access,
  Grove.OpenId, Grove.Members for the member portal): every sign-in, registration, password and
  two-factor screen and the site and member account pages; the themes lay them out (§ Access
  screens).
- **Shared screens are required pages**: every tenant's route table holds a standard set of
  required pages, laid out by each shell's active theme; the error pages are one set per tenant
  (§ Shared screens).
- **Setup has no MVC view**: the installation steps are static-SSR Blazor pages in Grove.Setup
  (§ Setup).

## The inventory

Counted over `src/` (bin, obj and `reference/` excluded).

| Surface | Count | Workstream |
| --- | --- | --- |
| `.cshtml` files | 1,072 | |
| — shape templates (driver shapes, part/field/settings editors and displays) | 597 | b |
| — `Views/Items/*` (deployment steps, workflow/rule items, audit details) | 261 | b |
| — controller views (`Views/{Controller}/*`) | 120 | **a** |
| — `_ViewImports` / `_ViewStart` | 82 | a (go with the last view of each module) |
| — layouts (TheAdmin ×3, SafeMode, LegacyFrame, SiteTheme) | 6 | **a** |
| — Razor Pages (`Grove.Demo/Pages`) | 6 | **a** |
| `Controller` classes (not `ControllerBase`) | 79 | a (views) / c (JSON actions) |
| — returning views | 57 | **a** |
| View components | 5 | **a** |
| Result/action filters that write into MVC layouts | 8 | **a** |
| Themes rendering MVC pages | TheAdmin, SafeMode, Grove.LegacyFrame, the site `Layout.cshtml` | **a** |

The full per-module list of controller views is in § Controller views; the per-file list of
shape templates is in [shape-engine.md](shape-engine.md). Every view here and every shape
template is covered by the Razor → Blazor component parity audit
([blazor-display.md](../blazor-display.md) › Rework plan, step 7).

## Controller views

Classification: **native** (a Blazor page already serves the route; the MVC controller and views
are deleted, cite the page), **port** (a page to build), **delete** (the feature is retired or the
screen has no use; module-level deletions are in § Module deletions and keeps), **keep** (the
justified keep list).

Target home by function (Access screens to the Access modules, and the shell model, [shells-and-themes.md](../shells-and-themes.md)):
platform admin screens go to `Grove.AdminTheme.Client` (as every platform admin page has so
far) **until** server.md's split gives each platform module a client library; module admin
screens go to the module's `blazor-wasm/` library; Access screens to the Access modules
(§ Access screens); shared screens per § Shared screens.

### Already native: delete the MVC controller and views (wave 1)

The native page serves the route and is declared in the page registry. A ticked line has no MVC
controller left: its controller, views, view models, MVC-only drivers, shapes, scripts and
styles are deleted, and so are the functional tests of those screens. An open line names what
still holds the MVC screen.

| ☐ | Module · controller | Views | Native page | Size | Deps |
| --- | --- | --- | --- | --- | --- |
| ☑ | Grove.Admin · `AdminController` | Admin/Index, the `AdminDashboard*` shapes of Admin, Contents, Users, Themes | `Dashboard.razor` (`/Dashboard`); the MVC user menu links to the admin root | S | — |
| ☑ | Grove.AdminMenu · `MenuController`, `NodeController` | Menu/{Create,Edit,List}, Node/{Create,Edit,List}, `MenuItem.Tree*`, the node editors (Link, Placeholder, ContentTypes, Lists) | `AdminMenus.razor` (`/AdminMenu/List`); nodes register with `AddAdminNode<TNode, TBuilder>()` (no driver) | S | — |
| ☑ | Grove.AuditTrail · `AdminController` | Admin/{Index,Display}, the list and event shapes, the content and user event drivers | Grove.AuditTrail blazor-wasm (`/Audit`, `/Audit/Records/{id}`); the list query, filter parser and `AuditTrailIndexOptions` are gone from Grove.AuditTrail.Abstractions | S | — |
| ☑ | Grove.Users · `AuditTrailAdminController` | AuditTrailAdmin/Index | the settings group `user-audit-trail` (`UserSnapshotSettings`, `SettingsForm` at `/Settings/user-audit-trail`): each user property the snapshot may keep and its redactor, stored as `AuditTrailUserEventSettings`; a save keeps only the properties a snapshot may hold | S | — |
| ☑ | Grove.DataLocalization · `AdminController` | Admin/{Index,Statistics} | `Translations.razor` (`/DataLocalization`); per-culture statistics are not shown natively | S | — |
| ☑ | Grove.Deployment · `DeploymentPlanController`, `ImportController`, `StepController` | DeploymentPlan/{Create,Display,Edit,Index}, Import/{Index,Json}, Step/{Create,Edit} | `DeploymentPlans.razor`, `DeploymentPlan.razor`, `DeploymentImport*.razor` | S | — (steps register with `AddDeployment<TSource, TStep>()` and are edited as JSON; the per-step drivers and `Items/*DeploymentStep*` templates, the deployment-target providers and executors are deleted; content goes to a plan or a target through `ContentDeploymentApi`) |
| ☑ | Grove.Deployment.Remote · `RemoteClientController`, `RemoteInstanceController` | RemoteClient/*, RemoteInstance/* | `RemoteClients.razor`, `RemoteInstances.razor` | S | — |
| ☑ | Grove.Features · `AdminController` | Admin/Features | `Features.razor` | S | — |
| ☑ | Grove.Media · `AdminController`, `MediaProfilesController` | Admin/{Index,Options}, MediaProfiles/*, Media.Azure's `MediaGalleryCapabilitiesFilter` | `MediaLibrary.razor`, `MediaOptions.razor`, `MediaProfiles.razor` | S | — |
| ☑ | Grove.Queries · `AdminController`, `Sql/AdminController` | Admin/{Create,Edit,Index,Query}, the `Query*`/`SqlQuery*` shapes and drivers, Lucene's and Elasticsearch's query editor drivers and `Query-*.Link` | `Queries.razor`, `SqlQuery.razor` (`SqlQueryRunner`) | S | — |
| ☑ | Grove.Recipes · `AdminController` | Admin/Index | `Recipes.razor` | S | — |
| ☑ | Grove.Settings · `AdminController` | Admin/Index, `Settings-*`, `SiteSettings.*`, every site settings display driver (`SiteDisplayDriver`, `AddSiteDisplayDriver` removed) with its view model and `*Settings.Edit` view | the native settings group pages (`SettingsForm`, [api.md › Settings groups](api.md#settings-groups)); a group's id is a constant on its settings model | M | — |
| ☑ | Grove.Tenants · `AdminController` (Index, state actions) | Admin/Index, the `TenantAction*`/`ManageTenantAction*`/`ManageFeaturesActionButton`/`ProfileFeatureTags` shapes | `Tenants.razor` | S | — |
| ☑ | Grove.Themes · `AdminController` | Admin/Index, the `ThemeEntry*` shapes and driver | `AdminThemes.razor` | S | — |
| ☑ | Grove.Contents · `AdminController` | Admin/{Create,Display,Edit,List}, the content part and field editor shapes and their drivers' `Edit`/`Update` (ContentFields, Flows, Html, Markdown, Title, Alias, Autoroute, Media, PublishLater, ArchiveLater, Menu, Seo, Spatial, Taxonomies, Lists, ContentLocalization), `CommonPart`'s date and owner editor drivers, the MVC content list (its filter parser, query service, `ContentOptionsViewModel` and the modules' list filters) | `ContentItems.razor`, `CreateContentItem.razor`, `EditContentItem.razor`, `DisplayContentItem.razor`; every part and field is edited on the editor tree ([parity.md](parity.md) › New components); an item's admin pages are `ContentAdminPaths` (Grove.ContentManagement.Abstractions), so `ContentItemMetadata` carries only its display route values; the Liquid `{% a edit_for/admin_for/create_for %}` tags link to the native pages, `display-for` stays the only link tag helper | L | — |
| ☑ | Grove.Contents · `ItemController` (Preview), `DownloadController` | Item/Preview, Download/Display | download: `ContentDeploymentApi` (the content list's download, add-to-plan and export-to-target actions; the MVC modals, drivers and scripts are deleted); preview: the display endpoint with `version=latest` (the latest version, a draft included, for whoever may preview it), shown by `DisplayContentItem.razor` at `…/Display?version=latest` | M | — |

### Ports (wave 3 unless noted)

| ☐ | Module · controller | Views | Target | Size | Deps | Wave |
| --- | --- | --- | --- | --- | --- | --- |
| ☑ | Grove.Tenants · `FeatureProfilesController` | FeatureProfiles/{Create,Edit,Index} | `Grove.AdminTheme.Client` `FeatureProfiles.razor` (`/TenantFeatureProfiles`, Default tenant) over `api/grove/tenants/feature-profiles` (`FeatureProfilesApi`); rules edited as a list (rule, expression); the JSON-schema editor and `FeatureProfilesSchemaService` are deleted | M | — | 3 |
| ☑ | Grove.AdminDashboard · `DashboardController` | Dashboard/{Index,Manage} | deleted with the module; the native `Dashboard.razor` (regions) covers it | S | — | 3 |
| ☑ | Grove.ContentPreview · `PreviewController` | Preview/{Index,Render,_Layout}, `ContentPreview.Button`, the preview start-up filter and draft cache | the native editor's preview pane (`ContentPreviewPane`) over `POST api/grove/content-preview` (Grove.ContentPreview's `ContentPreviewApi`: the unsaved item rendered as its display renders, HTML or `?format=tree`) | M | — | 3 |
| ☑ | Grove.Shortcodes · `AdminController` | Admin/{Create,Edit,Index} | `Grove.Shortcodes.BlazorWasm` `ShortcodeTemplates.razor` (`/Shortcodes`) over `api/grove/shortcodes/templates` (`ShortcodeTemplatesApi`); the template is edited with `CodeEditor` (Liquid). The editors' `ShortcodeModal` and the descriptor shapes stay with the MVC HTML editors (shape-engine.md) | M | — | 3 |
| ☑ | Grove.Sms · `AdminController` | Admin/Test | `Grove.Sms.BlazorWasm` `SmsTest.razor` (`/Sms/Test`) over `api/grove/sms/providers` and `api/grove/sms/test` (`SmsTestApi`) | S | — | 3 |
| ☑ | Grove.Cors · `AdminController` | Admin/Index | a settings group through `SettingsForm` | S | — | 3 |
| ☑ | Grove.Taxonomies · `AdminController`, `TagController` | Admin/{Create,Edit} (term editing), the tag creation action | the taxonomy's native editor: `TaxonomyPartEditor` edits the term tree, each term with its type's editor tree; tags are created through `api/grove/taxonomies/{id}/tags` (Grove.Taxonomies' `TaxonomiesApi`) | M | — | 3 |
| ☑ | Grove.Search · `SearchController` | Search/Search (site), `Search-*`, `SearchFormPart*` | `Grove.Search.BlazorWasm` (site shell) `SiteSearch.razor` (`/search`, `/search/{index}`) over `api/grove/search` (§ Search); `SearchFormPart` and its driver, migration and view models are deleted | M | — | 3 |
| ☑ | Grove.Media · `MediaCacheController`; Media.Azure / Media.AmazonS3 `AdminController` | MediaCache/Index, Admin/Options ×2 | read-only options in `MediaOptions.razor` | S | — | 3 |
| ☑ | Grove.Apis.GraphQL · `AdminController` | Admin/Index (GraphiQL) | the static page `Grove.Apis.GraphQL/wwwroot/graphiql.html` (`{tenant}/Grove.Apis.GraphQL/graphiql.html`), framed by the admin page `GraphiQL.razor` (`/GraphQL`, `Grove.Apis.GraphQL.BlazorWasm`) | S | — | 3 |
| ☑ | Grove.OpenId · `AccessController` (Error view) | Access/Error | Grove.OpenId's site page `/connect/error` (§ Access screens); the controller keeps the protocol endpoints | S | — | 2 |
| ☑ | Grove.RateLimits · `AdminController`, `LimiterController` | Admin/*, Limiter/*, the limiter and policy drivers | a settings group through `SettingsForm` | M | — | 3 |
| ☑ | Grove.UrlRewriting · `AdminController` | Admin/{Create,Edit,Index}, the rule drivers and shapes, `url-rewriting/resort` | `Grove.Routes.BlazorWasm` `RouteManagement.razor` (`/Routes`) over `api/grove/routes` (`RoutesApi`, § Routes) | M | — | 3 |
| ☑ | Grove.Sitemaps · `AdminController`, `SitemapIndexController`, `SourceController`, `SitemapCacheController` | 10 views, the source drivers and shapes (`CustomPathSitemapSource.*`, Grove.Contents' `ContentTypesSitemapSource.*`) | `Grove.Sitemaps.BlazorWasm`: `Sitemaps.razor` (`/Sitemaps/List`, `/SitemapIndexes/List`), `SitemapSources.razor` (`/Sitemaps/Display/{id}`), `SitemapsCache.razor` (`/SitemapsCache/List`) over `api/grove/sitemaps` (`SitemapsApi`); a source's settings travel as its JSON, with editors for the content types and custom path sources | L | — | 3 |
| ☑ | Grove.Elasticsearch · `AdminController`; Grove.Lucene · `AdminController` | IndexInfo, NotConfigured, Query ×2, the index-profile drivers of Lucene, Elasticsearch and Azure AI | the native indexing page (query runner tab) | M | — | 3 |

### Access screens (wave 2)

The Access modules own the screens; each shell's theme lays them out
([shells-and-themes.md](../shells-and-themes.md#account-and-protocol-pages)):

- **Grove.Users** ships a shared component library, `Grove.Users.Components`
  (`src/Grove.Users/components`): the account parts (`GroveAccountSecurity`, `GroveConfirmEmail`,
  `GroveExternalLoginCompletion`), the staff sign-in (`SignInForm`) with the second step
  (`TwoFactorChallenge`), the two-factor page (`TwoFactorSetup`), the registration and password
  recovery forms, and `AccessFormExtensions`, which renders what features contribute to a sign-in
  form. Its site library (`site-wasm`, `Grove.Users.Site.BlazorWasm`) holds the site pages. The
  admin theme's login and profile pages and the member portal's pages use the shared components;
  a theme references the component library, never a module's page library.
- **The login-extension seam** (`ILoginExtensionProvider`, Grove.Access.Abstractions): a feature
  contributes links, provider buttons and sections to a sign-in form (`AccessForms`: login,
  register, forgot-password, reset-password), served by `api/grove/auth/form/{form}`. Users
  contributes the forgot-password and register links and a button per external authentication
  scheme (every provider module, through its scheme; nothing is listed by hand); ReCaptcha a
  section (`CaptchaChallenge`, Grove.Components) whose token the form sends as
  `g-recaptcha-response`. The display drivers on `LoginForm`, `RegisterUserForm`,
  `ForgotPasswordForm` and `ResetPasswordForm` are gone.
- **Grove.Members' `member-wasm`** holds the member portal's access pages: login, registration,
  forgot and reset password, external login and the account page.
- **Grove.OpenId's `site-wasm`** (`Grove.OpenId.Site.BlazorWasm`) holds the consent and logout
  confirmation pages and the protocol error page.

| ☐ | Screen | From | To | Size | Deps |
| --- | --- | --- | --- | --- | --- |
| ☑ | Staff login (`Login.razor`) and its login-form extensions (forgot password, register, external providers, captcha) | `Grove.AdminTheme.Client`; MVC `AccountController.Login` + `Account/Login` | the admin theme's `Login.razor` (the frame: branding, the signed-in state) over Grove.Users' `SignInForm`; the extensions through `ILoginExtensionProvider` and `api/grove/auth/form/login`; the external sign-in starts at `api/grove/auth/external-login` | L | — |
| ☑ | Registration, registration pending | MVC `RegistrationController` + 2 views | Grove.Users site pages `/Register`, `/RegistrationPending` (`RegistrationForm`) over `api/grove/registration` | M | — |
| ☑ | Forgot / reset password (staff), confirmations | MVC `ResetPasswordController` + 4 views | Grove.Users site pages `/ForgotPassword`, `/ForgotPasswordConfirmation`, `/ResetPassword`, `/ResetPasswordConfirmation` over `api/grove/password/{forgot,reset}`; `UserEmailService`'s link opens `/ResetPassword` | M | — |
| ☑ | Two-factor: index, login with code, login with recovery code, recovery codes, disable; authenticator app; SMS authenticator | MVC `TwoFactorAuthenticationController`, `AuthenticatorAppController`, `SmsAuthenticatorController`, `AdminTwoFactorAuthenticationController` + 10 views + `Layout-TwoFactor`, the `TwoFactorMethod.*` shapes | `TwoFactorSetup` on the site page `/TwoFactor` (also at the old paths `/Authenticator/Configure/App`, `/Authenticator/Reset/App`, `/GenerateRecoveryCodes`, `/ShowRecoveryCodes`, `/DisableTwoFactorAuthentication`) over `api/grove/two-factor`; the second step in place on the login page (`TwoFactorChallenge` over `api/grove/auth/two-factor`), and on the site pages `/LoginWithTwoFactorAuthentication`, `/LoginWithRecoveryCode`; a user manager turns another user's two-factor authentication off from the user editor (`api/grove/users/{id}/two-factor/disable`) | XL | — |
| ☑ | Account security, email confirmation, external sign-in completion | `Grove.SiteTheme.Client` pages + `Grove.Components/Account` | Grove.Users' `site-wasm` pages over the moved components (`Grove.Users.Components`) | S | — |
| ☑ | Member login, registration, reset, external login, account | `Grove.MemberTheme.Client` pages | Grove.Members `member-wasm` | M | — |
| ☑ | OpenId consent, logout confirmation, protocol error | `Grove.SiteTheme.Client`; MVC `Access/Error` | Grove.OpenId `site-wasm` (`/connect/consent`, `/connect/logout-confirm`, `/connect/error`) | S | — |
| ☑ | Delete MVC `AccountController.Login`/`LogOff`, `Layout-Login`, the user menu shapes (`UserMenu*`, `UserMenuItems-*`, `NavbarUserMenu`) | Grove.Users, TheAdmin | deleted; sign-out is `api/grove/auth/logout`; `ExternalAuthenticationsController` keeps the external sign-in's protocol endpoints (challenge, callback, the JSON completion) | S | — |

## Themes, layouts and layout filters (wave 4)

| ☐ | Surface | What it does today | Plan | Size | Deps |
| --- | --- | --- | --- | --- | --- |
| ☐ | **TheAdmin** (19 views: `Layout`, pager, navigation, notify, toggle theme) | The classic admin theme; renders every MVC admin page, in the legacy frame through LegacyFrame | Delete the theme project once no MVC admin page remains | M | waves 1 and 3 |
| ☐ | **Grove.LegacyFrame** (`Grove.Server/Themes/Grove.LegacyFrame`) with `LegacyFrameThemeSelector`, `/legacy-host`, `LegacyHost.razor`, `LegacyAdminFrame`, the middleware bypass (`ShellSelectionMiddleware.cs:106`), the admin not-found page (`ShellRegistry.cs:148,168`) | Hosts an MVC admin page in an iframe inside the Blazor admin | Delete; the admin shell's not-found page becomes a native not-found (the member shell's is the model) | M | everything the frame can still reach is native |
| ☐ | **SafeMode** | Fallback theme when the active theme fails | Delete with MVC (the Blazor shells do not use themes' Razor layouts) | S | — |
| ☑ | **Grove.SiteTheme `Views/Layout.cshtml`** | — | Deleted: no MVC page renders on the site | S | — |
| ☑ | **Grove.ContentPreview `Preview/_Layout`** ☑ (deleted with the preview port), **Grove.Setup `Setup/_Layout`** ☑ (deleted; § Setup) | Own layouts | With their ports; Setup per § Setup | S | — |
| ☐ | **Layout filters** that write into the MVC layout: `NotifyFilter` (Grove.DisplayManagement/Notify/NotifyFilter.cs:15), `RazorViewActionFilter` (Razor/RazorViewActionFilter.cs:15), `GoogleAnalyticsFilter`, `GoogleTagManagerFilter`, `FacebookPixelFilter`, `FBInitFilter`, `MiniProfilerFilter` | Inject menus, notifications and head/body scripts into Razor layouts | Notifications are native already (delete; the admin menu filter is gone with the page registry); the analytics and pixel scripts need a **head contribution seam** on the shell documents (`App.razor` head per shell, a module-contributed `HeadOutlet` content) before their filters go | M | the head seam (a; small); c none |
| ◐ | **View components** (`SelectRoles`, `SelectStringOperation`; `SelectContentTypes` and `DisplayContentItem` are deleted with the Contents MVC screens) | Used only inside MVC views and shape templates | Delete with their last caller | S | b |
| ☐ | **Razor Pages support** (`ModularPageMvcCoreBuilderExtensions`, Grove.Admin's `AdminPageRouteModelProvider` and `AdminPageRouteModelConvention`) | Razor Pages in modules; no module ships one | Delete with the views half | S | — |
| ☐ | **`AddMvc` views half** (Grove.Mvc/Startup.cs:88 `AddMvc`, :125 Razor runtime compilation, the Razor view engine, view location expanders, tag helpers, `RazorShapeTemplateViewEngine`) | The view engine itself | Remove once no `.cshtml` outside the keep list remains; keep the controllers half until api.md H4 deletes it | L | b (Razor shape engine), c (controller plumbing) |

## Module deletions and keeps

Modules whose remaining views exist only because the module exists are either deleted or kept
and ported to Blazor. A deleted module goes whole (project, views, recipes, tests); a kept
module's screens are ports (§ Ports) and its shape templates are tracked in
[parity.md](parity.md).

**Deleted**

| ☐ | Module | Views (all kinds) | Why |
| --- | --- | --- | --- |
| ☑ | Grove.Demo | 38 (incl. 6 Razor Pages) | Sample module for MVC, shapes and Razor Pages; nothing a host uses |
| ☑ | Grove.Mvc.HelloWorld, Grove.Mvc.Web | 5 | MVC samples and the MVC-only host |
| ☑ | Grove.XmlRpc, with Grove.MetaWeblog.Abstractions and the `RemotePublishing/` features of Grove.Lists, Grove.Autoroute, Grove.Html, Grove.Markdown, Grove.Title and Grove.Media (their MetaWeblog drivers and the RSD endpoint serve XmlRpc only) | 0 views, 2 controllers | Retired protocol |
| ☑ | Grove.AdminDashboard | 19 | The native `Dashboard.razor` with regions covers it |

**Kept and ported to Blazor**

| ☐ | Module | Views (all kinds) | Port |
| --- | --- | --- | --- |
| ☑ | Grove.Taxonomies, Grove.Lists | 23, 17 | Part and field editors and displays ([parity.md](parity.md) › New components): `TaxonomyPartEditor`, `TaxonomyFieldEditor` over `TaxonomiesApi`; `ListPartEditor` over Grove.Lists' `ListsApi` (`api/grove/lists/{id}/items`, `/order`), a child created on the native create page with `?containerId=`; the MVC order action and the list's admin shapes deleted |
| ◐ | Grove.Seo, Grove.Sitemaps | 6, 22 | `SeoMetaPartEditor` ☑ (the Seo views, driver and scripts deleted); the sitemaps pages in the module's library ☑ (§ Ports); `SitemapPart.Edit` (the sitemap part's editor) ☐ |
| ☐ | Grove.Cors, Grove.RateLimits | 3, 24 | Settings groups through the generic `SettingsForm` ([parity.md](parity.md) › New components) |
| ☑ | Grove.Markdown, Grove.Html, Grove.Spatial, Grove.Title, Grove.Alias, Grove.PublishLater, Grove.ArchiveLater, Grove.ContentLocalization, Grove.ContentPreview | 15, 13, 6, 7, 3, 4, 4, 9, 5 | Content parts: editors on the editor tree (`GeoPointFieldEditor`, `LocalizationPartEditor` over Grove.ContentLocalization's `api/grove/content-localization/{id}` and its `localize` action, `ContentPreviewPane`); the edit and option views, editor scripts and editor-only drivers deleted; the display templates remain until the Razor shape engine goes ([shape-engine.md](shape-engine.md)) |
| ☐ | Grove.Apis.GraphQL, Grove.OpenApi, Grove.MiniProfiler, Grove.Sms(.Azure), Grove.Email.Azure, Grove.Media.Azure, Grove.Media.AmazonS3, Grove.ReCaptcha | 2, 2, 1, 7, 2, 2, 2, 2 | Optional providers keep their modules; GraphiQL is a static page ☑ and the SMS test page native ☑ (§ Ports) |
| ☐ | Grove.Facebook, Grove.Google, Grove.Twitter, Grove.GitHub, Grove.Microsoft.Authentication | 10, 10, 4, 3, 11 | Login providers, kept as Access providers ([access.md](../access.md)); their settings are Access settings pages |
| ☑ | Grove.Search, with Grove.Lucene, Grove.Elasticsearch, Grove.AzureAI | 12, 19, 22, 15 | § Search; the engines' `NavigationItemText-*` views are deleted |
| ☑ | Grove.UrlRewriting | 15 | Folded into **Grove.Routes** (§ Routes): the rules engine is Grove.Routes', its admin UI the route management page |

**Open**

| ☐ | Module | Views (all kinds) | Question |
| --- | --- | --- | --- |
| ☐ | Grove.Rules | 42 | Keep, fold or replace; see below |
| ☐ | Grove.ReverseProxy, Grove.Https | 3, 2 | Keep (a `SettingsForm` group each) or delete |

**Grove.Rules is the condition engine Grove.Layers uses**, not something layers replaced.
`LayerRuleReader` (Grove.Layers) reads a layer's `Rule` and its conditions; the conditions are
All/Any groups, Boolean, Homepage, Url, Culture, Role, IsAuthenticated/IsAnonymous,
ContentType and Javascript (through Grove.Scripting). Its MVC condition editors are not needed
(layer rules are edited as JSON on `Pages/Layers.razor`), so the open question is the engine,
not its views. The overlap to resolve: the display system has a second condition system of its
own, the comparison predicates of the bindings (`DisplayPredicates` in
Grove.DisplayManagement.Abstractions, `TemplateTreePredicates` in Grove.Templates;
[widgets.md](../widgets.md)'s visibility rules, `visible: nodes.country.value == "US"`). ☐ One
condition system or two, and which one layers use.

### Search

**Grove.Search is the search seam**: one search interface, its API and its UI. The interface is
`ISiteSearch` (Grove.Search.Abstractions): it resolves the index (the site's default search index
when none is named), checks the caller holds the index's query permission (`QuerySearchIndex`, per
index), runs the engine the index's provider registered, loads the hits' content items in the
engine's order (published, or latest when the index holds latest versions) and tells the
`ISearchHandler`s. Lucene, Elasticsearch, Azure AI Search and later engines are providers: each
registers its `ISearchService` with `AddSearchService<T>(providerName)`, and an index profile names
the provider that serves it; all of them are kept. This is
[queries.md](../queries.md) › "Search engines are connections; search is a consumer of Queries"
seen from the UI side.

- **API:** `api/grove/search?index=&terms=&from=&size=` (`SearchApi`, open to every caller; a
  refused caller is answered 401/403): the page title and placeholder (the `search` settings
  group), the index, and a page of hits (id, type, display text, display URL, highlight
  fragments) with whether more follow.
- **Site page:** `Grove.Search.BlazorWasm` (a site-shell library, referenced by the site theme
  client): `SiteSearch.razor` at `/search` and `/search/{index}`, the `SearchBox` and the results
  list with previous/next paging. Highlights keep only the engines' `<em>` marks.
- **Admin:** the indexing page is native; each engine's query runner is a tab on it (§ Ports).

### Routes

**Grove.Routes** owns the tenant's routes: one module, one feature (`Grove.Routes`). It lists every
route the tenant answers and applies the URL rewrite and redirect rules; there is no separate
rewriting module. Grove.UrlRewriting is folded into it (two modules managing the tenant's routes
would be parallel systems), and Grove.UrlRewriting.Abstractions is Grove.Routes.Abstractions
(namespace `Grove.Routes`): the rule sources (`IUrlRewriteRuleSource`, `AddRewriteRuleSource<T>`),
the rule handlers, the rules store and manager, the recipe step `UrlRewriting` and the permission
`ManageUrlRewritingRules` keep their names, so a module adds a rule source by registering into
Grove.Routes.

- **Page:** `Grove.Routes.BlazorWasm` `RouteManagement.razor` (`/Routes`, under Tools; `ViewRoutes`
  or `ManageUrlRewritingRules`), three tabs: the page registry's routes (shell, routes, menu
  caption, permissions, the library or source that declares the page); the content routes (the
  home route's values, and each content route source's published routes, searchable and paged);
  and the rewrite and redirect rules (create, edit, delete, reorder; shown to
  `ManageUrlRewritingRules`).
- **API:** `api/grove/routes` (`RoutesApi`): `pages`, `home`, `content-sources`, `content`
  (`ViewRoutes`); `rules` list, create, update, delete and `rules/reorder`
  (`ManageUrlRewritingRules`). A rule change requests a shell release: the rewrite options are
  built with the shell.
- **Content route sources:** `IContentRouteSource` (Grove.Routes): the Autoroute paths and the
  aliases, each registered from a startup that requires its feature (`Grove.Autoroute`,
  `Grove.Alias`).

## Shared screens: required pages, laid out by each shell's theme

Every tenant ships a **standard set of required pages in its route table**: declared entries in
the page registry ([admin-menu.md](../admin-menu.md) › The page registry), present whichever
modules are enabled. The **layouts** those routes render in come from the **active theme of each
shell**; these required layouts are what make a theme operational and a site load, so a theme
that does not provide them is not a working theme for its shell.

| Shell's theme | Required pages it lays out |
| --- | --- |
| Admin theme | the error pages, the admin home (index), the admin shell's sign-in and registration pages |
| Member theme | the member registration pages, the member home |
| Site theme | the site home (index) |

**Error pages: one shared set per tenant, editable per tenant.** The tenant has one set of error
pages (`/Error/{status}`), not one per shell; the shells' prefixes do not multiply them. The
behaviour (status handling, what is shown to whom) is one component (`GroveErrorPage`); the
admin theme provides its layout. Sign-in and registration behaviour stays with the Access
modules (§ Access screens); the theme only provides the frame.

How themes are stored in the database and switched at run time is open
([shells-and-themes.md](../shells-and-themes.md) › Open questions).

## Setup: static-SSR pages in Grove.Setup

The Grove instance installation is a **static-SSR Blazor page in Grove.Setup**. It runs in the
setup shell (`ShellContextFactory.MinimumShellDescriptor`: the host's setup features, Grove.Setup
and Grove.Recipes) before a database exists, so it does not use the Blazor host of the `Grove`
feature (`MapRazorComponents<App>`, the shell selection, the theme clients), which depends on
database-backed modules. Grove.Setup registers its own components in the setup shell: while the
tenant is uninitialized, its startup adds `AddRazorComponents()`, the antiforgery middleware and
`MapRazorComponents<SetupDocument>()`.

- `Components/SetupDocument.razor`: the document (the module's `Styles/setup.min.css`) and a
  router over Grove.Setup's assembly; no interactivity, no Grove theme.
- `Components/Pages/Installation.razor` (`/`): an `EditForm` (`FormName="setup"`) posting the
  site name, recipe, time zone, database (provider, connection string, table prefix, schema;
  hidden where the tenant's settings preset them) and the super user. A tenant created with a
  secret needs the setup token (`?token=`) or answers 404. On success it redirects to the tenant
  root.
- `Services/SetupInstallation.cs`: the choices, the token check, the validation and the call to
  `ISetupService.SetupAsync`; tested by
  `test/Grove.Tests/Modules/Grove.Setup/SetupInstallationTests.cs`.

No MVC view is kept for setup: there is no setup controller, view or layout. The host's AutoSetup
calls `ISetupService` itself.

The native Tenants page sets tenants up from the Default tenant
(`api/grove/tenants/{name}/setup`); only the Default tenant's first setup needs the
pre-database page.

## Waves

| Wave | Content | Exit |
| --- | --- | --- |
| 0 | The open rows of § Module deletions and keeps (Grove.Rules, Grove.ReverseProxy, Grove.Https) | each row decided |
| 1 | Delete the MVC controllers and views that are already native (§ Already native) | no MVC admin controller with a native counterpart |
| 2 | Access screens to the Access modules; move the site/member account pages; staff login, registration, reset, two-factor native | no Users/OpenId MVC view |
| 3 | Remaining ports of kept modules (Grove.Routes, setup's static-SSR pages); the module deletions | no controller view left |
| 4 | Delete TheAdmin, LegacyFrame (`/legacy-host`), SafeMode; the head contribution seam replaces the script filters; remove the MVC views half | see exit criterion |

Waves 1 and 2 can run in parallel with workstream b's driver-to-component work; wave 4 waits
for b's removal of the Razor shape engine (the shape templates are b's) and for c's removal of
the controller plumbing it decides to drop.

## Exit criterion

- No `.cshtml` file in `src/`. The keep list is empty: setup is static SSR (§ Setup).
- TheAdmin, SafeMode and Grove.LegacyFrame deleted; `/legacy-host` and `LegacyAdminFrame` gone;
  the middleware's legacy-frame bypass removed.
- The views half of `AddMvc` (view engine, Razor runtime compilation, view location expanders,
  tag helpers, view components, Razor Pages) removed, the setup shell included.
- Every screen a user meets is a Blazor page in the module that owns its function.
