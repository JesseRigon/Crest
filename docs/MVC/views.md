# MVC retirement › Views: every MVC view surface to a Blazor component, or gone

Workstream (a) of the MVC retirement, done together with moving the APIs out of Crest.Server
([server.md](../server.md)). This document is the tracker for every remaining **view surface**:
controllers that return views, controller views, Razor Pages, view components, the themes and
layouts MVC renders pages into, and the result filters that inject into those layouts. Two
sibling documents cover the rest:

- [shape-engine.md](shape-engine.md) (workstream b): the shape engine - display drivers, shape
  templates (`*.cshtml` named after shapes, `Views/Items/*`), `IHtmlDisplay`, the markup
  fallback. About 860 of the 1,072 `.cshtml` files are shape templates and are tracked there,
  not here; this document only lists which ports depend on which shape components.
- [api.md](api.md) (workstream c): the API layer. Every port here needs an API; where one does
  not exist yet it is named here as a dependency on api.md.

Status: planning. Nothing in this document is built yet unless marked ☑. Sizes:
**S** (an hour or two: a page over an existing API), **M** (a page plus an API), **L** (several
pages, an API and shape components), **XL** (a subsystem).

## Principles

- **The MVC layer goes**, in three workstreams: (a) views to Blazor components (this document),
  (b) the shape engine, (c) the API layer.
- **A port is a page plus an API call**, nothing server-side in the page. The API is declared by
  the module that owns the screen's function and registered in Crest.Server's API registry
  ([architecture.md › Modules declare, Crest.Server registers and serves](../architecture.md#modules-declare-crestserver-registers-and-serves),
  [api.md](api.md) § Placement); the page is declared in the page registry the same way.
- **Access screens live with the Access modules** (today Crest.Users, Crest.Roles, Crest.Access,
  Crest.OpenId): every sign-in, registration, password and two-factor screen, including the
  account pages now in Crest.SiteTheme and Crest.MemberTheme (§ Access screens).
- **Open:** where shared screens' per-tenant defaults live (§ Shared screens) and whether setup
  stays the one MVC exception (§ Setup). Both sections set out the options and mark a
  recommendation as such.

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
| — Razor Pages (`Crest.Demo/Pages`) | 6 | **a** |
| `Controller` classes (not `ControllerBase`) | 79 | a (views) / c (JSON actions) |
| — returning views | 57 | **a** |
| View components | 5 | **a** |
| Result/action filters that write into MVC layouts | 8 | **a** |
| Themes rendering MVC pages | TheAdmin, SafeMode, Crest.LegacyFrame, the site `Layout.cshtml` | **a** |

The full per-module list of controller views is in § Controller views; the per-file list of
shape templates is in [shape-engine.md](shape-engine.md). Every view here and every shape
template is covered by the Razor → Blazor component parity audit
([blazor-display.md](../blazor-display.md) › Rework plan, step 7).

## Controller views

Classification: **native** (a Blazor page already serves the route; the MVC controller and views
are deleted, cite the page), **port** (a page to build), **delete** (the feature is retired or the
screen has no use; module-level deletions are in § Module deletions, they need the user's
decision), **keep** (the justified keep list).

Target home by function (Access screens to the Access modules, and the shell model, [shells-and-themes.md](../shells-and-themes.md)):
platform admin screens go to `Crest.AdminTheme.Client` (as every platform admin page has so
far) **until** server.md's split gives each platform module a client library; module admin
screens go to the module's `blazor-wasm/` library; Access screens to the Access modules
(§ Access screens); shared screens per § Shared screens.

### Already native: delete the MVC controller and views (wave 1)

The native page serves the route today; the MVC controller is kept only as the legacy frame's
fallback and goes with it. Each line deletes the controller, its views and its `_ViewImports`
when it is the module's last view.

| ☐ | Module · controller | Views | Native page | Size | Deps |
| --- | --- | --- | --- | --- | --- |
| ☐ | Crest.Admin · `AdminController` | Admin/Index | `Dashboard.razor` (`/Dashboard`) | S | — |
| ☐ | Crest.AdminMenu · `MenuController`, `NodeController` | Menu/{Create,Edit,List}, Node/{Create,Edit,List} | `AdminMenus.razor` (`/AdminMenu/List`) | S | b: AdminMenu node `Items` shapes go with it |
| ☐ | Crest.AuditTrail · `AdminController` | Admin/{Index,Display} | Crest.AuditTrail blazor-wasm (`/Audit`, `/Audit/Records/{id}`) | S | b: audit event detail shapes |
| ☐ | Crest.Users · `AuditTrailAdminController` | AuditTrailAdmin/Index | `/Audit` (user events) | S | — |
| ☐ | Crest.DataLocalization · `AdminController` | Admin/{Index,Statistics} | `DataLocalization.razor` | S | c: statistics endpoint if not yet in the native page |
| ☐ | Crest.Deployment · `DeploymentPlanController`, `ImportController`, `StepController` | DeploymentPlan/{Create,Display,Edit,Index}, Import/{Index,Json}, Step/{Create,Edit} | `DeploymentPlans.razor`, `DeploymentPlan.razor`, `DeploymentImport*.razor` | S | b: the per-step editor drivers go (steps are edited as JSON, [admin-routes.md](../admin-routes.md)) |
| ☐ | Crest.Deployment.Remote · `RemoteClientController`, `RemoteInstanceController` | RemoteClient/*, RemoteInstance/* | `RemoteClients.razor`, `RemoteInstances.razor` | S | — |
| ☐ | Crest.Features · `AdminController` | Admin/Features | `Features.razor` | S | — |
| ☐ | Crest.Media · `AdminController`, `MediaProfilesController` | Admin/{Index,Options}, MediaProfiles/* | `MediaLibrary.razor`, `MediaOptions.razor`, `MediaProfiles.razor` | S | — |
| ☐ | Crest.Queries · `AdminController`, `Sql/AdminController` | Admin/{Create,Edit,Index,Query}, Sql | `Queries.razor` (`SqlQueryRunner` shared, keep the service) | S | — |
| ☐ | Crest.Recipes · `AdminController` | Admin/Index | `Recipes.razor` | S | — |
| ☐ | Crest.Settings · `AdminController` | Admin/Index | `Settings.razor` + the group editors | M | b: every site settings driver still reachable only through this screen must have a native group first (check `/Settings/{groupId}` coverage) |
| ☐ | Crest.Tenants · `AdminController` (Index, state actions) | Admin/Index | `Tenants.razor` | S | — |
| ☐ | Crest.Themes · `AdminController` | Admin/Index | `AdminThemes.razor` | S | — |
| ☐ | Crest.Contents · `AdminController` | Admin/{Create,Display,Edit,List} | `ContentItems.razor`, `CreateContentItem.razor`, `EditContentItem.razor`, `DisplayContentItem.razor` | L | b: the ContentFields and Flows edit shapes that only the MVC editor renders ([backlog.md](../backlog.md) step 7 line); every enabled type editable on the tree |
| ☐ | Crest.Contents · `ItemController` (Preview), `DownloadController` | Item/Preview, Download/Display | content display endpoint / `?format=html`; download as a JSON API (c) | M | c: content download endpoint |

### Ports (wave 3 unless noted)

| ☐ | Module · controller | Views | Target | Size | Deps | Wave |
| --- | --- | --- | --- | --- | --- | --- |
| ☐ | Crest.Tenants · `FeatureProfilesController` | FeatureProfiles/{Create,Edit,Index} | `Crest.AdminTheme.Client` `FeatureProfiles.razor` (`/TenantFeatureProfiles`) | M | c: `api/crest/tenants/feature-profiles` | 3 |
| ☐ | Crest.AdminDashboard · `DashboardController` | Dashboard/{Index,Manage} | delete if the native `Dashboard.razor` (regions) replaces widgets-on-dashboard; else port to the module's blazor-wasm | M | b: dashboard widget shapes; needs the user's decision (module not in the Venti recipe) | 3 |
| ☐ | Crest.ContentPreview · `PreviewController` | Preview/{Index,Render,_Layout} | the native editor's preview pane over the display endpoint | M | c: preview render of an unsaved item | 3 |
| ☐ | Crest.Shortcodes · `AdminController` | Admin/{Create,Edit,Index} | `Crest.AdminTheme.Client` (enabled in Venti; admin-routes "deferred") | M | c: shortcode templates API | 3 |
| ☐ | Crest.Sms · `AdminController` | Admin/Test | the native email test page pattern (`/Admin/Sms/Test`) | S | c: sms test endpoint | 3 |
| ☐ | Crest.Cors · `AdminController` | Admin/Index | settings group page | S | c: cors policies API | 3 |
| ☐ | Crest.Taxonomies · `AdminController` | Admin/{Create,Edit} (term editing) | native content editor (terms are content items) | M | b: TaxonomyPart editor component | 3 |
| ☐ | Crest.Search · `SearchController` | Search/Search (site) | site page in the module's `site-wasm/` | M | c: search API | 3 |
| ☐ | Crest.Media · `MediaCacheController`; Media.Azure / Media.AmazonS3 `AdminController` | MediaCache/Index, Admin/Options ×2 | read-only options in `MediaOptions.razor` | S | c | 3 |
| ☐ | Crest.Apis.GraphQL · `AdminController` | Admin/Index (GraphiQL) | delete or a static page; needs the user's decision | S | — | 3 |
| ☐ | Crest.OpenId · `AccessController` (Error view) | Access/Error | Access screen (§ Access screens) | S | — | 2 |
| ☐ | Crest.RateLimits · `AdminController`, `LimiterController` | Admin/*, Limiter/* | module blazor-wasm | M | c | 3 (if kept) |
| ☐ | Crest.UrlRewriting · `AdminController` | Admin/{Create,Edit,Index} | module blazor-wasm | M | c | 3 (if kept) |
| ☐ | Crest.Sitemaps · `AdminController`, `SitemapIndexController`, `SourceController`, `SitemapCacheController` | 10 views | module blazor-wasm | L | b: sitemap source editor shapes; c | 3 (if kept) |
| ☐ | Crest.Elasticsearch · `AdminController`; Crest.Lucene · `AdminController` | IndexInfo, NotConfigured, Query ×2 | the native indexing page (query runner tab) | M | c: index query API | 3 (if kept) |

### Access screens (wave 2)

Today the screens are split between Crest.Users MVC, the admin theme client (the
staff login), the member theme client (member login, registration, reset, external login) and
the site theme client (account, email confirmation, external sign-in completion and
OpenId consent pages, [shells-and-themes.md](../shells-and-themes.md#account-and-protocol-pages)).
They all move to the Access modules:

- **Crest.Users gets client libraries per shell** (`Crest.Users/site-wasm`, `member-wasm`, and
  the login page for the admin's login shell) plus shared components (the account components
  now in `Crest.Components/Components/Account` move with them). A module library per shell is
  how every other module contributes pages, so the theme clients keep only chrome.
- **Crest.OpenId gets a `site-wasm/`** for the consent and logout confirmation pages (now in
  `Crest.SiteTheme.Client`) and the protocol error page.

| ☐ | Screen | From | To | Size | Deps |
| --- | --- | --- | --- | --- | --- |
| ☐ | Staff login (`Login.razor`) and its login-form extensions (forgot password, register, external providers, two-factor methods: today display drivers on `LoginForm`) | `Crest.AdminTheme.Client`; MVC `AccountController.Login` + `Account/Login` | Crest.Users (login shell page) | L | b: the `LoginForm` drivers become components a module contributes; c: the JSON login already exists (`api/crest/auth/login`) |
| ☐ | Registration, registration pending | MVC `RegistrationController` + 2 views | Crest.Users site page | M | c: registration API (the member portal's `register` is the model) |
| ☐ | Forgot / reset password (staff), confirmations | MVC `ResetPasswordController` + 4 views | Crest.Users site page (the member portal's pages are the model) | M | c: the portal's `forgot-password`/`reset-password` generalised; `UserEmailService`'s link points at the new page |
| ☐ | Two-factor: index, login with code, login with recovery code, recovery codes, disable; authenticator app; SMS authenticator | MVC `TwoFactorAuthenticationController`, `AuthenticatorAppController`, `SmsAuthenticatorController` + 10 views + `Layout-TwoFactor` | Crest.Users site pages; the 2FA sign-in step joins the login page | XL | c: two-factor API (enable, codes, verify); b: the `TwoFactorMethod.*` item shapes become components |
| ☐ | Account security, email confirmation, external sign-in completion | `Crest.SiteTheme.Client` pages + `Crest.Components/Account` | Crest.Users site-wasm (move, no rewrite) | S | — |
| ☐ | Member login, registration, reset, external login | `Crest.MemberTheme.Client` pages | Crest.Members `member-wasm` (members are an Access concern: bindings, classes) | M | — |
| ☐ | OpenId consent, logout confirmation, protocol error | `Crest.SiteTheme.Client`; MVC `Access/Error` | Crest.OpenId `site-wasm` | S | — |
| ☐ | Delete MVC `AccountController.Login`/`LogOff` views, `Layout-Login`, the user menu shapes (`UserMenuItems-*`) | Crest.Users, TheAdmin | — | S | after the above |

## Themes, layouts and layout filters (wave 4)

| ☐ | Surface | What it does today | Plan | Size | Deps |
| --- | --- | --- | --- | --- | --- |
| ☐ | **TheAdmin** (21 views: `Layout`, `Layout-Login`, `Layout-TwoFactor`, pager, navigation, notify, toggle theme) | The classic admin theme; renders every MVC admin page, in the legacy frame through LegacyFrame | Delete the theme project once no MVC admin page and no MVC Access screen remains | M | waves 1-3; Access wave 2 (`Layout-Login`, `Layout-TwoFactor`) |
| ☐ | **Crest.LegacyFrame** (`Crest.Server/Themes/Crest.LegacyFrame`) with `LegacyFrameThemeSelector`, `/legacy-host`, `LegacyHost.razor`, `LegacyAdminFrame`, the middleware bypass (`ShellSelectionMiddleware.cs:106`), the admin not-found page (`ShellRegistry.cs:148,168`) | Hosts an MVC admin page in an iframe inside the Blazor admin | Delete; the admin shell's not-found page becomes a native not-found (the member shell's is the model) | M | everything the frame can still reach is native |
| ☐ | **SafeMode** | Fallback theme when the active theme fails | Delete with MVC (the Blazor shells do not use themes' Razor layouts) | S | — |
| ☐ | **Crest.SiteTheme `Views/Layout.cshtml`** | The layout of the remaining MVC site pages (login, registration, reset, two-factor) | Delete after wave 2 | S | wave 2 |
| ☐ | **Crest.ContentPreview `Preview/_Layout`**, **Crest.Setup `Setup/_Layout`** | Own layouts | With their ports; Setup per § Setup | S | — |
| ☐ | **Layout filters** that write into the MVC layout: `NotifyFilter` (Crest.DisplayManagement/Notify/NotifyFilter.cs:15), `RazorViewActionFilter` (Razor/RazorViewActionFilter.cs:15), `GoogleAnalyticsFilter`, `GoogleTagManagerFilter`, `FacebookPixelFilter`, `FBInitFilter`, `MiniProfilerFilter` | Inject menus, notifications and head/body scripts into Razor layouts | Notifications are native already (delete; the admin menu filter is gone with the page registry); the analytics and pixel scripts need a **head contribution seam** on the shell documents (`App.razor` head per shell, a module-contributed `HeadOutlet` content) before their filters go | M | the head seam (a; small); c none |
| ☐ | **View components** (`SelectRoles`, `SelectContentTypes`, `DisplayContentItem`, `SelectStringOperation`, Demo's `Fake`) | Used only inside MVC views and shape templates | Delete with their last caller | S | b |
| ☐ | **Razor Pages** (`Crest.Demo/Pages`, 6) and `ModularPageMvcCoreBuilderExtensions` | Demo of Razor Pages in modules | Delete with Crest.Demo (§ Module deletions) | S | user decision |
| ☐ | **`AddMvc` views half** (Crest.Mvc/Startup.cs:88 `AddMvc`, :125 Razor runtime compilation, the Razor view engine, view location expanders, tag helpers, `RazorShapeTemplateViewEngine`) | The view engine itself | Remove once no `.cshtml` outside the keep list remains; keep the controllers half until api.md H4 deletes it | L | b (Razor shape engine), c (controller plumbing) |

## Module deletions: needs the user's decision

Modules whose remaining views exist only because the module exists, and which the Venti recipe
does not enable (verify the dependency closure before deciding). Each would delete the module,
not port it; none is deleted without the user's decision.

| ☐ | Module | Views (all kinds) | Why it is a candidate |
| --- | --- | --- | --- |
| ☐ | Crest.Demo | 38 (incl. 6 Razor Pages) | Sample module for MVC, shapes and Razor Pages; nothing a host uses |
| ☐ | Crest.Mvc.HelloWorld, Crest.Mvc.Web | 5 | MVC samples and the MVC-only host |
| ☐ | Crest.XmlRpc (MetaWeblog) | 0 views, 2 controllers | Retired protocol |
| ☐ | Crest.AdminDashboard | 19 | Widget dashboard; the native `Dashboard.razor` with regions covers it |
| ☐ | Crest.Rules | 42 | Rules engine for layers/menus; layers now place components by layer rules (step 6) |
| ☐ | Crest.Lists, Crest.Taxonomies | 17, 23 | Content structure that Crest.ContentPartLists and the party/organization model replace (verify Fruitful usage) |
| ☐ | Crest.Sitemaps, Crest.UrlRewriting, Crest.RateLimits, Crest.Cors, Crest.ReverseProxy, Crest.Https | 22, 15, 24, 3, 3, 2 | Infrastructure features not enabled; port only if kept |
| ☐ | Crest.Elasticsearch, Crest.Lucene, Crest.AzureAI, Crest.Search | 22, 19, 15, 12 | Search providers; the indexing page is native; keep at most one provider |
| ☐ | Crest.Facebook, Crest.Google, Crest.Twitter, Crest.GitHub, Crest.Microsoft.Authentication | 10, 10, 4, 3, 11 | Login providers and site scripts; keep as Access providers if wanted (their settings become Access settings pages) |
| ☐ | Crest.Markdown, Crest.Html, Crest.Spatial, Crest.Seo, Crest.Title, Crest.Alias, Crest.PublishLater, Crest.ArchiveLater, Crest.ContentLocalization, Crest.ContentPreview | 15, 13, 6, 6, 7, 3, 4, 4, 9, 5 | Content parts: their views are shape templates (b); keep or drop per part |
| ☐ | Crest.Apis.GraphQL, Crest.OpenApi, Crest.MiniProfiler, Crest.Sms(.Azure), Crest.Email.Azure, Crest.Media.Azure, Crest.Media.AmazonS3, Crest.ReCaptcha | 2, 2, 1, 7, 2, 2, 2, 2 | Optional providers and dev tools |

## Shared screens (open)

Shared screens are the ones every tenant has and some tenant may want to change: the error
pages, not-found, the sign-in surfaces' frame, the account pages' frame, the consent page, the
email bodies (already templates), the maintenance and "tenant disabled" pages. Two places can
hold the default:

**Option A - the components live in the tenant themes.** Each theme client (admin, site,
member) ships its own error page, not-found, sign-in frame; a tenant changes them by choosing or
forking a theme.

- Uses: theme clients per shell (a theme's UI is its pages, [shells-and-themes.md](../shells-and-themes.md));
  the theme compatibility guards; a forked theme (`BaseTheme`) inherits the parent's pages.
- For: a theme is a complete look; a branded fork changes everything in one place; no extra
  override lookup at render time.
- Against: every theme must ship every shared screen (a site theme without an error page breaks
  error handling); a tenant's small change means forking a whole theme; shared screens are
  behaviour, not only look (the error page decides what a 401 shows) and that behaviour would be
  duplicated per theme; the planned theme rework (structure plus design system,
  [shells-and-themes.md](../shells-and-themes.md) › Open questions) moves themes towards data,
  away from compiled pages.

**Option B - Crest.Server (or the owning module) holds the default; tenants override.** One
component per shared screen (as `CrestErrorPage` in Crest.Components is today), rendered by
each shell; tenants change it through the override systems.

- Uses: **component overrides and variants (§ 5a)** - a tenant forks the shipped component's
  node, changes its settings or slots, every page that places it follows; **Templates** - the
  screen's body is a shipped template (as `AccountEmailTemplates` is for the email bodies) that a
  tenant template of the same name replaces; **designed pages** for a whole replacement page.
- For: one implementation of the behaviour, tested once; a tenant's change is an override of a
  few values, inherited by every shell; matches the override tenet ("a tenant never edits the
  base everyone shares"); themes stay look-only and can become data.
- Against: shared screens need override points (slots, template names) designed in; the admin
  and member shells do not render data-defined pages yet (branding.md notes this), so the
  template route works on the site first; an override lookup per render (cached like every
  template).

**Option C - hybrid**: the default in Crest.Server/Access modules (behaviour), the frame and
text as a shipped template the theme may also ship (theme templates are a template qualifier
today: `Theme` on a template record), the tenant's own template winning over both.

**Recommendation (not a decision):** Option C, which is Option B plus the theme qualifier the
template chain already has. The behaviour (status handling, what is shown to whom) stays in one
component owned by the module whose function it is (errors: Crest.Server; sign-in: Access); its
visible body is a template resolved tenant → theme → shipped, so a theme can restyle it, a
tenant can override it per § 5a, and nothing is duplicated. The error pages and email bodies
already follow it halfway.

## Setup (open)

Setup is the one remaining MVC page with its own layout (`Crest.Setup/Views/Setup/{Index,_Layout}`,
`SetupController`). It runs in the minimum shell (`ShellContextFactory.MinimumShellDescriptor`:
the host's setup features, Crest.Setup and Crest.Recipes), before a database exists; the Blazor
host (`MapRazorComponents`, the shell selection, the theme clients) is the `Crest` feature, which
depends on database-backed modules.

- **Keep it as the one exception**: the keep list holds `Crest.Setup/Views/**` and the MVC view
  engine stays registered for the setup shell only (it can be registered by Crest.Setup's own
  startup rather than globally, so the tenant shells drop it). Cheapest; the exit criterion then
  allows exactly these files.
- **A minimal pre-database page**: a static HTML page (no Razor, no Blazor) served by
  Crest.Setup with a small JSON setup API (the stock `api/tenants/setup` already takes the same
  input), or a Blazor static-SSR component registered in the setup shell without the `Crest`
  feature. Removes MVC views entirely; costs a page and a setup API owned by Crest.Setup.

Either way the native Tenants page sets tenants up from the Default tenant
(`api/crest/tenants/{name}/setup`); only the Default tenant's first setup needs the
pre-database page.

## Waves

| Wave | Content | Exit |
| --- | --- | --- |
| 0 | Answer § Shared screens and § Setup and the module deletions; agree the keep list | decisions recorded here |
| 1 | Delete the MVC controllers and views that are already native (§ Already native) | no MVC admin controller with a native counterpart |
| 2 | Access screens to the Access modules; move the site/member account pages; staff login, registration, reset, two-factor native | no Users/OpenId MVC view |
| 3 | Remaining ports of kept modules; module deletions as decided | no controller view outside the keep list |
| 4 | Delete TheAdmin, LegacyFrame (`/legacy-host`), SafeMode, the site `Layout.cshtml`; the head contribution seam replaces the script filters; remove the MVC views half | see exit criterion |

Waves 1 and 2 can run in parallel with workstream b's driver-to-component work; wave 4 waits
for b's removal of the Razor shape engine (the shape templates are b's) and for c's removal of
the controller plumbing it decides to drop.

## Exit criterion

- No `.cshtml` file in `src/` outside the **keep list**. The keep list is explicit and each
  entry justified; the proposal is: `Crest.Setup/Views/**` if § Setup keeps setup on MVC,
  nothing otherwise.
- TheAdmin, SafeMode and Crest.LegacyFrame deleted; `/legacy-host` and `LegacyAdminFrame` gone;
  the middleware's legacy-frame bypass removed.
- The views half of `AddMvc` (view engine, Razor runtime compilation, view location expanders,
  tag helpers, view components, Razor Pages) removed from the tenant shells (kept for the setup
  shell only under the keep option).
- Every screen a user meets is a Blazor page in the module that owns its function.
