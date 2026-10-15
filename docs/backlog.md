# Backlog: decided but not built

Items the docs record as decided, with no open question against them, that the code does not
have yet. Each line names the doc that holds the design; that doc is the spec. Mark an item
done here when it lands, and update its doc to describe what exists.

Left out on purpose, because a question is still open against them: the connection system
(the protocol modules, the data layer's SQL connections, queries.md › Connections) and the
Queries API (queries.md Q6, Q7; workflows.md "what the platform offers for connections"),
audit feeds at volume (A4), the designer UI and its move (blazordesigner.md, workflows.md W6),
design systems as content (part vs fields), cross-tenant translation suggestions, the global
editing UI (which UI first), the geocoder, Regions and Spatial (regions.md), merging Grove.Routes and Grove.Routes.Auto, the content model's naming (content types, fields, parts; bags and part lists, today Grove.Contents.Flows and the `Grove.Contents.PartLists` feature), approvals, theme storage and switching
(shells-and-themes.md), shells on their own hostnames, user settings, and the parked security audit and sandboxed custom elements (blazor-display.md).

Status: ☐ open, ◐ partial, ☑ done.

## Small fixes

- ☐ Data localization against the one page registry: the translations store, `AdminMenuTranslationCleanup`, `MenuCaptionResolver` and the translations editor key admin menu and route captions by page registry entry id, and a route or page entry is a data localization source (localization.md, admin-menu.md)
- ☑ `ShellCompatibilityService` matches the longest module id, not `StartsWith(id + ".")` (shells-and-themes.md)
- ☑ The server provides the tenant base to the WASM host instead of the client peeling the shell suffix (shells-and-themes.md)
- ☑ Currency lookup asks the providers before the culture table (types.md; host bugs list #12)
- ☑ The Iconify cache is not a hosted service (host bugs list #7)
- ☑ Member self-registration sends FirstName/LastName (host bugs list #1)
- ☑ The global store needs no host call (`AddGroveGlobalStore`) (globals.md)
- ☑ Global schema versioning moves onto `DataMigration` (queries.md)
- ☑ One `GroveTenant` model, not two (regions.md)
- ☑ `option_label` / `option_list` Liquid filters (content-items.md)
- ☑ "DBA" out of the organization-type seed; `convert-to-staff` removed (parties.md)
- ☑ Platform modules' unused activity display drivers and views deleted (workflows.md)
- ◐ Host references out of Grove's test solution and comments; a publishing gate test (architecture.md #4) - left: item-classification list move, pre-publish review
- ☑ A Grove-owned party role or fixture (architecture.md #3)
- ☑ Location data removed from PNG, WebP and HEIC uploads (media.md)
- ☑ An admin editor for the tenant's upload scanning settings (media.md)
- ☑ Machine actors: OpenId in the setup recipe, antiforgery skipped for bearer requests, the client id in the workflow actor (machine-actors.md)
- ☐ Shortcodes: retired into Liquid (blazor-display.md, shortcodes.md)

## Registries (architecture.md › Modules declare, Grove.Infrastructure.Server registers and serves)

- ☑ **One page registry** (admin-menu.md › The page registry): each page declares its route,
  shell, permission and menu placement (parent path, caption, position, icon) once; the route
  table, the route gate and the admin menu's default tree derive from it. The per-module
  `IRoutePermissionProvider`s, the navigation providers for native pages and
  `IWebAssemblyRouteProvider` retire. The admin menu becomes a view over the registry plus the
  defined layer (tenant renames, hides, moves, custom placements and links, by stable entry id)
  plus the user hide layer; the caption-matching provider import and sync go. Page permissions
  per tenant and per user resolve through the access model, one permission for menu and gate.
  Legacy MVC pages are declared entries pointing at their legacy route until ported; dynamic
  items (New › *content type*) are a registry source.
- ☐ **Page registry captions in PO extraction** (admin-menu.md › The page registry): captions
  declared on `[PageEntry]` and in page sources are not extracted into PO catalogs.
- ☑ **The dual-layer API registry** (MVC/api.md P5, P15): the declared layer from module code
  through `IApiContributor`, mapped at shell build and following feature enablement; the defined
  layer from `IDefinedApiSource`s through a per-tenant dynamic endpoint data source invalidated on
  publish. The workflow engine's `HttpEndpoint` routes are defined entries (its private route
  table is gone) and its management API a declared contributor; generated operation endpoints
  (P14) join the defined layer.
- ☑ **APIs out of Grove.Infrastructure.Server** into their declaring modules as route groups, registered
  through `Grove.Infrastructure.Server.Abstractions`; Grove.Infrastructure.Server keeps the hosting plumbing, the bootstrap,
  routing and display endpoints, and the admin menu and navigation over the page registry, as
  route groups (server.md § 1, MVC/api.md P1-P9); the option picker is Grove.Contents.OptionPicker's (S2).
- ☐ **A server-side icons module**: the icon APIs (`IconsApi`, `IconProvidersApi`, `IconifyApi`,
  `TenantIconsApi`) and their stores leave Grove.Infrastructure.Server (server.md § 1).
- ☑ **Minimal APIs, explicit binding, native validation** (MVC/api.md › Hosting, › Validation):
  every route group's handler names its binding source, request DTOs carry Grove's validation
  ruleset (`Grove.Validation`), the mapper leaves a contributor that cannot build out and reports
  it, and the conformance test covers every shipped contributor.
- ☑ **In-module controllers to route groups** (MVC/api.md P16): every module's and Fruitful's
  API is a route group; the protocol endpoints (OpenIddict, feeds, sitemaps, remote import,
  scanner callback, external sign-in) too; no `Controller` class is left.
- ☑ **MVC plumbing off the API path** (MVC/api.md › Plumbing): `IShellLinkGenerator` instead of
  `IUrlHelper` outside the views, the address scheme and file versions registered by
  Grove.Infrastructure, `ApiAuthenticationHandler` in Grove.Access, problem details in
  Grove.Infrastructure.Server, `LoginService` on `LoginRefusal`, the MVC formatters and filters gone.
- ☐ **Functional checks off the retired `api/content`**: `ApiAuthenticationTests` and
  `OpenApiTests` (test/Grove.Tests.Functional) need another endpoint that challenges through the
  "Api" scheme.
- ☐ Localization as a registry: modules declare localization sources for their data types (Regions: country, subdivision and place names) and the translations editor lists them all (localization.md › Localization is a registry)

## Communications (communications.md)

- ☐ Communications as capabilities, protocols and integrations: SMTP types to `Grove.Data.Protocols.Smtp`, Twilio to `Grove.Integrations.Twilio`, `SmsNotificationProvider` into `Grove.Communications`, the Azure Communication Services providers into `Grove.Integrations.Microsoft`, the channel settings pages into the communications screens (communications.md › Code that still has to change)

## Access (access.md, permissions.md)

- ☑ Anonymous actors: machine identity and roles on connections, the "accepts anonymous runs" flag, login and registration triggers run as the user the event names
- ☑ Members onto the extension system (resource mapper, scope provider on the org binding index, `OrganizationScopeProvider` out of Server); member admins through `ManageOrgMembers`
- ☑ Parties onto the extension system (mapper, scope provider, route permissions off `IGroveRoutePermissionProvider`)
- ◐ Page and route gates move to Access (`IGroveRoutePermissionProvider`, `GroveRouteAuthorizationService`, `AssignmentScopeProvider`; `AdminFilter` goes). Left: `AdminFilter` (goes with the MVC admin). `AssignmentScopeProvider` is retired
- ☑ Admin controllers' per-type content authorization goes; the scope is the filter (content-items list; the MVC list goes with the MVC admin)
- ☑ Version bumps only for rights writes; implied-by resolved from the permission instance; the revocation check run (the check is in the suite; it runs with the tests at the end)
- ☑ Recorded defects: SSRF guard on messaging and HTTP activities, per-definition narrowing of engine instance reads, `UsersController` scoped. Instance-read narrowing is the View list
- ☑ Per-part permissions as scope providers (permissions.md): the seam; the first rule comes with the first type that needs one
- ☑ The permission model (permissions.md): sets, groups, overrides, the projection, the editors, Fruitful's groups folded in (Fruitful compiles after the repack)
- ☑ Who manages the permission model and the organization owner (permissions.md): CRUD sets, the hierarchy bound, the default hierarchy, the owner record, protection, owner-locked ceiling, transfer. The owner panel is on the admin organization page; staff with ManageOrgMembers reach every organization

## Display (blazor-display.md, widgets.md)

- ☑ Widget replacements (widgets.md): rich text, figure, forms, validation, search box, the per-item page body (`DisplayTreePart`), visibility rules, the captcha, `FacebookPlugin` and the media picker editor
- ☑ Step 1 leftovers: route scanner reads the catalogs, page regions as zone keys, editor facet entries, primitives annotated (icon, markdown, layout hosts, colour and media editors; the form editors stay unregistered as a parallel set, reason in blazor-display.md)
- ◐ Step 2 leftovers: tree cache (versioned and validated, see decision 12; the unfiltered cache is left); display events for bound nodes ☑
- ☐ Razor → Blazor component parity audit: every Razor view, shape template and widget has a Blazor component equivalent, reusing `Grove.Display.Components` (blazor-display.md step 7; MVC/views.md, MVC/shape-engine.md, widgets.md; tracker: [MVC/parity.md](MVC/parity.md))
- ☐ The Liquid templates updated to display trees and optionalities (blazor-display.md step 7)
- ☑ Step 7 remainder: content displays (fields with localized labels, title, bag, list with its pager and feed link, taxonomy, geo point) and every part and field editor on the tree (MVC/parity.md › New components); the MVC screens, their shapes and the HTML path deleted
- ◐ Decision 6: zone keys as registry entries with a schema; single or multiple instances per page: keys, contributions, validation and the single-instance check built; left: priority reordering through overrides and a `Zone` component node (designer zone settings)
- ◐ Decision 9 / § 6a: designed pages (page record and route-table provider): record, store, API, recipe step, data route provider and site rendering built; admin and member pages are not routed (no shell component renders a data-defined page)
- ☑ Decision 10: the default permitted-fields `Content` template
- ◐ Decision 12: client ETag trees, unfiltered server cache: ETags, client-held trees and the shared anonymous cache built; the unfiltered per-page cache needs the builder's per-viewer decisions moved into the node filter
- ◐ § 10: per-page permission sets (Design, Schema, Data, Publish): per designed page built; Publish per component waits for component drafts
- ◐ Module homes: `Grove.Data.Types` ☑ (registry; readers not wired), `Grove.Global` → `Grove.Data.Global` ☑ (Fruitful needs the repack), `Grove.Display.Components.Primitives` / `.Templates` skipped (open question in blazor-display.md)
- ◐ § 12: one expression engine: the engine's Liquid runs through the platform's, one Fluid version; `Grove.Scripting` removal waits for workflows.md's Retire step, limits wait for the parked sandbox

## Workflows (workflows.md)

- ◐ Operations step 1 remainder: caller lifetime rules for circuits and websockets ☑, read logging as a tenant setting ☑, conformance fixtures for organizations and policies ☑. Left: OpenId validation in the host recipe (the same work as the Machine actors line above)
- ◐ Step 3: typed per-type content operations ☑ (Copy/Move fields write through them). Left: one registry (`WorkflowRegistryCatalog` folded) and per-tenant operation names, both waiting on a decision (workflows.md › Tasks › 3)
- ☑ Step 4: queue partitioned by correlation id, idempotency keys (unit id + slot); the object version in the payload is not built (no raiser names one)
- ◐ `IGroveAntiforgery`; the hook attachment page ☑ and webhook insight; per-process metrics. Left: `IGroveAntiforgery` (comes with the designer move, W6 open); webhook insight and per-process metrics (as audit feeds, which wait on run history in audit)

- ☑ The engine's management API as minimal APIs with explicit binding (`EngineBody<T>`, `EngineApiPolicies`, the error contract's problem details); the request DTOs on the shared validation ruleset (`Validated<T>`); FastEndpoints, FluentValidation and `Grove.Workflows.Api.Common` removed from the engine (workflows.md › The engine API)
- ☑ The View list (`Grove.Access.View`): the ceiling, the access API, the engine API instance operations decided per definition, the instance list narrowed (workflows.md › Ownership and permissions; access.md defect closed). Left: Studio's access editor (with the designer UI list)

## Audit (audit.md)

- ☑ Steps 1–4: impersonation actor, session and impersonation events, non-content write events, the audit index, content history as diffs with checkpoints, workflow run history in audit, audit pages
- ◐ Step 5: feeds' data side (indexed record, parties, owner, organization, both attributions) built; feed definitions as saved queries wait on A4

## Sharing (sharing.md)

- ☑ Phase 3: business roles and the role registry (`RoleKind`, derived roles)
- ☑ Phase 4: Share panel, rules screens, access indicators, derived-role picker
- ☑ System accounts; delete/move manager checks; comments; items following a folder chain

## Media (media.md)

- ☑ CDN URL signers; Azure and S3 storage on the file system (not verified against live cloud accounts or CDNs)
- ☑ Private resize cache; multi-path inheritance; organization and personal drives created automatically; `FileDriveSettings` screen
- ☑ Module-generated files through the pipeline with exemptions
- ◐ Drive-aware picker; Secure Media mapping gaps (done: the Blazor media field editor and both gaps; left: the classic Razor media picker and the HTML/Markdown editors' insert still use library paths)
- ☑ Portal notifications; viewer pane and add-on seam; drive browser

## Shells and themes (shells-and-themes.md)

- ☐ Tenant-address conformance (shells-and-themes.md › The tenant base and enabled modules): a tenant on a URL prefix and a tenant on its own host, each with non-default admin, login and member paths, log in on each shell and reach `api/grove/*`, hubs and the login redirects at the tenant's own addresses; absolute links in email and OAuth callbacks carry the tenant's host
- ☑ A disabled module is absent per tenant; theme clients and UIs load per shell
- ☑ Member password-reset and external-login pages; side-checking login gate
- ☑ Organization slugs in paths and an organization picker; one device-local tenant switcher; impersonation under one account
- ☑ Shell registry (replaces `RouteBucket` and `BlazorAdminThemeMiddleware`); render mode declared by the page
- ☑ One routing system on the Blazor side: the site resolved through the route table, `ContentPage` is "/" only, legacy-owned paths forwarded to MVC
- ☑ Step 7 waves 4–5 (shells): native tenant create/edit/setup; OpenId applications, scopes, server and validation; account security, email confirmation, external sign-in completion, OpenId consent/logout and error pages as Blazor pages; email bodies as templates
- ☑ Step 7 waves 1 and 3 (architecture): native Email/SMTP with test send, audit trail settings, background tasks (run now), notifications list, role clone, user unlock/confirm/verification/external sign-ins, content clone/save draft/discard draft/versions, user registration/reset/change-email settings, custom settings on the editor tree, the content definition editor (types, parts, fields; settings as JSON) and index profiles; the MVC controllers, drivers and views they replace deleted (blazor-display.md › step 7). Left: Email.Azure settings page, the Users list shapes/filters (unused), the index providers' profile drivers, structured settings forms, browser checks for these pages

## Parties (parties.md)

- ☑ Legal entity names; parent organization picker; the tenant's own organization
- ☑ Class-as-set connection index; the binding record as the single source; closure-table hierarchy
- ☑ Association content type; bag-element service; contact-point and organization-name indexes
- ☑ NAICS reseed; user-list class filter; Person creation for external logins
- ☑ Role bases (organization and person roles), Individual organizations, portal promotion, organization-derived business roles, organization-role transaction parties
- ☑ Assignments retired; organization ownership part, index and scope moved into Grove.Parties (access.md › Members, server.md S3)
- ☑ Parties UI: add role, Individual badge and conversion, owner panel, invite to portal, settings section

- ☐ Required pages (shells-and-themes.md › Required pages): each tenant's route table declares the standard required pages; the admin theme lays out the error pages, the admin home and the admin sign-in and registration pages, the member theme the member registration pages and home, the site theme the site home
- ☐ Error pages as one shared set per tenant, editable per tenant, instead of one per shell (shells-and-themes.md › Account and protocol pages)
- ☑ Setup as hardcoded static-SSR Blazor pages in Grove.Infrastructure.Setup; the MVC setup controller and views deleted (MVC/views.md › Setup, MVC/api.md P10)

## MVC retirement (MVC/views.md, MVC/parity.md)

- ☐ Open issues of the ports: behaviour the native screens do not yet match, and follow-ups ([MVC/open-issues.md](MVC/open-issues.md))
- ◐ Wave 1, the MVC admin screens with a native page deleted (MVC/views.md › Already native): Admin dashboard, AdminMenu, AuditTrail list, DataLocalization, Deployment plans/import/steps (and the per-step drivers and deployment-target executors), remote clients and instances, Features, Media library/options/profiles/cache and the storage options, Queries and the SQL runner, Lucene and Elasticsearch query and index screens, Recipes, Tenants list, Themes, Settings (with every site settings driver), Cors, RateLimits, the content download/add-to-plan/export modals, the `User` editor drivers ☑; the Contents MVC editor, list and preview (wave 3: the editors on the tree, the preview pane, `ContentAdminPaths` instead of `ContentItemMetadata`'s admin route values) ☑; left: the user audit settings are the settings group `user-audit-trail`
- ☑ Module deletions: Grove.Demo, Grove.Mvc.HelloWorld, Grove.Mvc.Web, Grove.XmlRpc with Grove.XmlRpc.Abstractions, Grove.MetaWeblog.Abstractions and the modules' `RemotePublishing/` features, Grove.AdminDashboard (MVC/views.md › Module deletions and keeps)
- ☑ Settings groups: `SettingsForm` at `/Settings/{groupId}` over `api/grove/settings/groups`, every MVC settings group declared as its settings type (`AddSettingsGroup<T>`, `AddSettingsModel<T>`), Cors and RateLimits included; `CodeEditor` (MVC/api.md › Settings groups, MVC/parity.md)
- ☑ Wave 2, the Access screens with the Access modules (MVC/views.md › Access screens): the staff sign-in over `SignInForm` with the login-extension seam (`ILoginExtensionProvider`: links, external providers, the captcha section) and the second step in place (`TwoFactorChallenge`); registration, password recovery and `TwoFactorSetup` as Grove.Access.Users site pages over their APIs; the account components moved to `Grove.Access.Users.Components`, the site account pages to Grove.Access.Users' `site-wasm`, the member access pages to Grove.Access.Members' `member-wasm`, the OpenId pages and the protocol error page to Grove.Access.OpenId's `site-wasm`; the MVC account, registration, password, two-factor and email confirmation controllers and views, the `LoginForm`/`RegisterUserForm`/`ForgotPasswordForm`/`ResetPasswordForm`/`TwoFactorMethod` drivers and shapes, the user menu shapes, `Layout-Login`, `Layout-TwoFactor` and SiteTheme's `Layout.cshtml` deleted
- ☑ Native tests of the Access APIs (`Grove.Tests/Modules/Grove.Access.Users/AccessApiTests.cs`: sign-in with and without two factors, the challenge and a recovery code, registration, forgot and reset password, the gate's setup-required rule); the test host maps the declared API route groups as Grove.Infrastructure.Server does. Left: the user audit settings group's filter
- ☑ The member portal's second sign-in step and captcha section: `TwoFactorChallenge` and `AccessFormExtensions` on the portal login (shells-and-themes.md › The member portal)
- ☑ A user who must set up two-factor authentication reaches only the setup, enforced by the access gate (`IPendingSignInStepProvider`, access.md › Authentication); one source for the sign-in paths (`UserOptions.LoginPath`; the other path options and the "use the site theme" settings removed)
- ☑ Kept modules ported to Blazor: Taxonomies, Lists, Seo, the content-part modules (their editors on the tree over the modules' APIs, `ContentPreviewPane`, `CulturePicker`, `HeadLink`, `IconPicker`, `ContentDiff`, `SitemapPartEditor`); Sitemaps, Shortcodes templates, the SMS test page, feature profiles and GraphiQL as a static page; Grove.Display.Rules' condition editors as `RuleEditor` and its condition editors on the Layers page over `api/grove/rules`; Grove.Infrastructure.Server.ReverseProxy and Grove.Data.Protocols.Https as settings groups; every module display template replaced by its registered component and deleted (MVC/views.md, MVC/parity.md)
- ☑ The head contribution seam (`IDocumentContributor`): Google Analytics, Tag Manager, the Meta Pixel, the Facebook SDK and MiniProfiler's includes in the shell documents; the MVC layout filters and the last module view deleted (MVC/views.md › Themes, layouts and layout filters)
- ☑ Grove.Search as the search seam (`ISiteSearch`, `api/grove/search`, the site search page) with Lucene, Elasticsearch and Azure AI Search as providers (MVC/views.md › Search, queries.md)
- ☐ The site search through the engines' query sources with the caller's scope injected (queries.md › Search engines are connections)
- ☑ Grove.Routes: the tenant's route management page (page registry routes, Autoroute, alias and home routes, rewrite rules); Grove.UrlRewriting folded into Grove.Routes and its MVC admin deleted (MVC/views.md › Routes)
- ☐ Grove.HomeRoute merged into Grove.Routes (MVC/views.md › Routes)
- ☑ `LiquidPart` bodies migrated to `DisplayTreePart`, the part retired (MVC/shape-engine.md › Component coverage)
- ☑ Tenant Liquid without `Request` and `HttpContext` (MVC/shape-engine.md L3)
- ☑ The shape engine without MVC: renderers, `[Shape]` bindings, tag helpers, `IHtmlDisplay`, the editor halves and `IUpdateModel` deleted; a shape without a component is an error node; the coverage report `api/grove/components/coverage` (MVC/shape-engine.md)
- ☑ The validation sink (`IValidationSink`) as the content manager's error sink, answered by the APIs as `errors` (MVC/shape-engine.md › The validation sink, MVC/api.md E3)
- ☑ One Liquid language (`Grove.Display.Liquid`'s `LiquidParser`); `Grove.Display.Management.Liquid` (the Liquid view engine) deleted (MVC/shape-engine.md › Liquid)
- ☑ Wave 4: Grove.Themes.TheAdmin, Grove.Infrastructure.SafeMode, Grove.LegacyFrame (`/legacy-host`, `LegacyAdminFrame`) and Razor Pages support deleted; the admin's not-found page is native (MVC/views.md › Themes, layouts and layout filters)
- ☑ MVC deleted: `AddMvc`, Grove.Mvc, Grove.Mvc.Abstractions, Grove.Application.Mvc.Targets, Grove.Contents.TagHelpers (MVC/api.md H4)
- ☐ The shape contract in Grove.Display.Management.Abstractions, or dropped (MVC/shape-engine.md E4)

## Content items (content-items.md)

- ☑ Picker relationship handlers; hierarchical Category; Fluid sandboxing with data scope
- ☑ The option picker field family to Grove.Contents.OptionPicker, contracts and DTOs in Grove.Contents.OptionPicker.Abstractions; field visibility in Grove.Contents.Types over `IFieldComparisonValueResolver` (server.md § 3, S2)
- ☑ Content Part Lists as the `Grove.Contents.PartLists` feature of Grove.Contents (its page the `Grove.Contents.PartLists.BlazorWasm` library, served while the feature is on); the option sources API gated by Grove.Contents.OptionPicker's `QueryOptionSources` (content-items.md, server.md § 3)
- ☑ The option picker's UI in its module's client library, `Grove.Contents.OptionPicker.BlazorWasm`: the `OptionPicker` component and the `OptionPickerField` editor facet in the component registry (server.md § 3)
- ☑ ContentPickerField merged into the option picker: the content items source gained types/stereotypes/all, the Liquid title pattern, edit and create links and ordered selection; definitions and stored values converted by migration and on recipe import; the display, GraphQL, Liquid, index, save guard, workflows, Menu, Parties and Fruitful moved over; the field, its settings, drivers, index, result providers (Default, Lucene, Elasticsearch, and their `*.ContentPicker` features) and the content-items endpoint of `ContentPickerApi` deleted (content-items.md › Option pickers, server.md § 3)

## Architecture (architecture.md)

- ☑ X3: "Grove" only in module and package names (stored names, platform clashes and the component library kept — list in architecture.md › Naming)
- ☑ #5 standalone host test (`test/Grove.Tests.Standalone`)
- ☑ #6 generic pieces into Grove (the workflow browser checks are written, not yet run)

## Other

- ◐ Branding: renditions and settings ☑, `branding.*` in the site query ☑, settings editor group ☑, head favicon and login/tenant-chip logos ☑; left: the shipped Login template and login as a workflow action (the admin and member shells do not render data-defined pages yet), the site theme's layout and email bindings (branding.md)
- ☑ Denomination (types.md)
- ☑ Queries index and SQL runner, Deployments pages as Blazor pages (admin-routes.md)
- ◐ Regions: maps as runtime rules ☑; postal codes and boundaries: the loaders ☑, the rows wait on the data-sourcing and licence decision (regions.md)
