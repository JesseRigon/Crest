# MVC retirement › Open issues: behaviour the native screens do not yet match, and follow-ups

What the native screens do differently from the MVC screens they replaced, and the follow-ups the
ports left. Siblings: [views.md](views.md) (the view surfaces and the waves),
[parity.md](parity.md) (the component audit), [shape-engine.md](shape-engine.md),
[api.md](api.md). An item tracked in another document is linked, not restated.

Each row: ☐, what differs or is missing, the target (**decide** = needs a ruling), the owner.

## Settings (`SettingsForm`)

| ☐ | Differs / missing | Target | Owner |
| --- | --- | --- | --- |
| ☐ | No decryption-error banner when a stored secret cannot be decrypted | the form shows the field as unreadable and asks for the value again | Grove.Infrastructure.Settings |
| ☐ | Value trimming some MVC forms did is not done | trim declared on the settings type, applied by the API | Grove.Infrastructure.Settings |
| ☐ | RateLimits: no clone action, no read-only list of the built-in limits | both on the group (a read-only section and a clone) | Grove.Security.RateLimits |
| ☐ | Https and ReverseProxy (kept, a `SettingsForm` group each) no longer show values that come from configuration | the group shows configuration-provided values read-only | Grove.Data.Protocols.Https, Grove.Infrastructure.Server.ReverseProxy |
| ☐ | OpenApi page badges and doc links inside field hints gone | hints may carry a link; badges as section notes | Grove.Infrastructure.Settings, Grove.Data.Protocols.OpenApi |
| ☐ | Only `mediaApi` was exercised live before the dev recipe enabled every feature | open every settings group live, load and save | Grove.Infrastructure.Settings |

## Admin pages: content list, media, indexes, users

| ☐ | Differs / missing | Target | Owner |
| --- | --- | --- | --- |
| ☐ | Content list: no links to an item's other translations (was `LocalizationPart.SummaryAdminLinks`); the `?Localization.ShowLocalizedContentTypes` menu option is gone | the translations as links in the row; **decide** whether the menu option returns | Grove.Contents.Localization |
| ☐ | Content list: "View as JSON" is a panel in the actions dialog, not a page; the draft preview only at `…/Display?version=latest`, no button | a "Preview draft" action in the row | Grove.Contents |
| ☐ | Media: a user holding only `ManageAssetCache`, or only the Azure/S3 options permission without `ViewMediaOptions`, has no menu entry | the page registry entries declare each permission that admits the page | Grove.Media |
| ☐ | Media.Azure: the storage-account warning on the media page is gone | a notice on the media page when the provider reports a misconfiguration | Grove.Media.Azure |
| ☐ | Lucene/Elasticsearch: no stand-alone query page, no `…/query/{indexName}?query=<base64>` deep link, no "Save Query" (now panels on Indexes) | deep link to the Indexes query tab; save as a query in Queries | Grove.Data.Indexing, Grove.Data.Queries |
| ☐ | Custom user settings: saved per card with its own button; not offered on "Add User"; no summary column in the user list | one save for the user editor, the cards on create | Grove.Infrastructure.CustomSettings, Grove.Access.Users |
| ☐ | Notification preferences: drag-reorder became up/down | see § Content editing, drag and drop | Grove.Communications |

The Users list query service and filter providers left without a UI are tracked in
[backlog.md › Shells and themes](../backlog.md#shells-and-themes-shells-and-themesmd) (step 7 waves 1 and 3, "left").

## Content editing

| ☐ | Differs / missing | Target | Owner |
| --- | --- | --- | --- |
| ☐ | Drag and drop gone: up/down in lists (and only within one page), move/indent buttons in taxonomies, up/down in a content item option picker; same in notification preferences, URL rules | one drag-reorder primitive in `Grove.Display.Components`, used by all of them | Grove.Display.Components |
| ☐ | Content item option picker: an edit link per held item, no "view" link | a view link | Grove.Contents.OptionPicker |
| ☑ | The content picker field is the option picker over the content items source (one field type for picking); definitions and values converted by migration and recipe import | — | Grove.Contents.OptionPicker |
| ☐ | The content item source searches the SQL index only: the Lucene and Elasticsearch picker editors (and their `*.ContentPicker` features) are gone | a search-engine-backed content item source, through Queries with the caller's scope | Grove.Contents.OptionPicker, Grove.Search |
| ☐ | The option picker's editor and placeable component are admin-shell only (the client library declares no shell); member and site templates cannot place the picker | a shell-neutral home or a per-shell library when a member or site form needs it | Grove.Contents.OptionPicker |
| ☐ | A list child's editor has no list header or navigation back to the list | the parent list as a breadcrumb on the editor | Grove.Display.Lists |
| ☐ | Preview pane renders the Detail display as static HTML without the site theme's CSS and scripts; no preview at the PreviewPart URL in the full site theme; no separate live-preview window | preview in the site shell with its theme, at the item's URL, openable in its own window | Grove.Contents.Preview |
| ☐ | The culture picker is no longer injected into the site navbar | document: place `CulturePicker` through a template or a layer | Grove.Contents.Localization |
| ☐ | SEO custom meta tags are edited as JSON, not a table | a name/content table | Grove.Seo |
| ☐ | LiquidPart conversion sanitizes the converted bodies; references other than `Model.ContentItem` (a bare `ContentItem`) are not rewritten | rewrite the other forms; list sanitized bodies in the migration log | Grove.Display.Templates |
| ☐ | Liquid `remove_for` and the Razor `admin-for`/`edit-for`/`remove-for`/`create-for` tag-helper modes removed | document the removal (links come from `ContentAdminPaths`) | Grove.Contents |
| ☐ | DateTimeField: the local-to-UTC conversion the MVC driver did is not reproduced | the editor or the API converts in the site's time zone | Grove.Contents.Fields |
| ☐ | Attached-media file-handling errors from the media driver not ported | the API returns them as field errors | Grove.Media |
| ☐ | Only the content items API calls `ValidateAsync`; the recipes' and workflows' content-manager paths do not | **decide** whether every write validates | Grove.Contents.Management |
| ☐ | The Google Maps key in GeoPoint field settings reaches editors through the definition API | document for admins: a browser key, restricted by referrer | Grove.Data.Types.Spatial |
| ☐ | The generation-only `DisplayContentItem` route points at `Item/Display`, which has no action: display links for items without Autoroute may not generate | route it to the display endpoint | Grove.Contents |
| ☐ | The content item source's create links build one item per allowed creatable type to check the create permission; cost unmeasured | measure; check against the type definition if costly | Grove.Contents.OptionPicker |

Tracked elsewhere: `IUsersAdminListQueryService` ([backlog.md › Shells and themes](../backlog.md#shells-and-themes-shells-and-themesmd)).

## Access

| ☐ | Differs / missing | Target | Owner |
| --- | --- | --- | --- |
| ☐ | Recovery codes are shown once in place (MVC cached them 5 s on a separate page); `/ShowRecoveryCodes` opens the recovery section | keep; document | Grove.Access.Users |
| ☐ | SMS enrollment: the server no longer keeps the pending number; the client sends number and code | the server binds the code to the number it sent it to | Grove.Access.Users |
| ☐ | "Sign in another way" is a method dropdown | keep; document | Grove.Access.Users |
| ☐ | A failed external login shows only the first error (`error` query parameter) | all errors | Grove.Access.Users |
| ☐ | Other modules can no longer add display drivers to the registration and password forms; only the login-extension seam remains | **decide**: extend `ILoginExtensionProvider` sections to registration and password forms, or not | Grove.Access.Users |
| ☐ | Forgot password no longer tells a user with an unconfirmed email to verify first | keep (same answer for any account, deliberate); document | Grove.Access.Users |
| ☐ | `qrcodejs` still in `Grove.Access.Users/Assets/package.json` | remove | Grove.Access.Users |
| ☐ | The gate's always-allowed list for two-factor setup assumes the site shell's `/TwoFactor` needs only routing, localization, antiforgery and auth APIs at boot | verify live | Grove.Access |
| ☐ | The test host (`Grove.Tests` `Apis/Context/SiteStartup.cs`) maps the declared API groups with its own loop, without the antiforgery filter: antiforgery on the new endpoints is untested | **decide** how the test host reuses Grove.Infrastructure.Server's mapper without depending on the whole server | Grove.Infrastructure.Server, Grove.Tests |

Tracked elsewhere: the user audit settings group's property-filter test
([backlog.md › MVC retirement](../backlog.md#mvc-retirement-mvcviewsmd-mvcparitymd), Access API tests "left").

## Module pages

| ☐ | Differs / missing | Target | Owner |
| --- | --- | --- | --- |
| ☐ | Bulk delete gone on URL rules, sitemaps, shortcode templates and feature profiles | a selection column and bulk delete on `GroveDataGrid` | Grove.Display.Components |
| ☐ | URL rules: rule-summary shapes gone (drag-reorder: § Content editing) | a summary column | Grove.Routes |
| ☐ | Menu id `urlRewriting` became `routes`: tenant menu edits keyed by the old id no longer apply | part of the upgrade path below | Grove.Routes |
| ☐ | Sitemaps: per-source thumbnails became "Add {type}" buttons; list search is client-side | server-side search when lists grow | Grove.Seo.Sitemaps |
| ☐ | Shortcodes: categories editor became dropdown + add; no descriptor preview on create/edit | **decide** first: [shortcodes.md](../shortcodes.md) retires shortcodes into Liquid, yet the templates admin was ported | Grove.Shortcodes |
| ☐ | Feature profiles: the JSON-schema editor became a rule-list editor; the id is immutable after creation | keep; document | Grove.Infrastructure.Features |
| ☐ | Search: from/size paging; Liquid/Razor overrides of `Search-*` and `SearchFormPart` gone; highlights keep only `<em>` | document; overrides through the site theme's components | Grove.Search |
| ☐ | Setup: language switcher is a list of links; no password-strength meter or show/hide toggles; recipe picker is a select with tooltips; provider-dependent fields toggled by an inline script | strength meter and toggles in the static-SSR form | Grove.Infrastructure.Setup |
| ☐ | GraphiQL: `?culture=` dropped; framed, not in the admin chrome | pass the culture through | Grove.Data.Protocols.GraphQL |
| ☐ | AdminSettings' "Sitemap" tab is a placeholder overlapping the Routes page | remove, or fold into Routes | Grove.Infrastructure.Shells.Admin |
| ☐ | Upgrade path for existing tenants (dev resets App_Data; needed before any real upgrade): feature id `Grove.UrlRewriting` → `Grove.Routes`, the stored rules document's type name, menu id `urlRewriting` → `routes`, feature id `Grove.ContentPartLists` → `Grove.Contents.PartLists` | one migration per change | Grove.Routes, Grove.Contents |

Tracked elsewhere: search through Queries with the caller's scope (item-level filtering), required
before user-owned content is indexed — security
([backlog.md › MVC retirement](../backlog.md#mvc-retirement-mvcviewsmd-mvcparitymd),
[queries.md › Connections](../queries.md#connections-the-protocol-modules-and-the-data-layer));
shortcodes retiring into Liquid ([backlog.md › Small fixes](../backlog.md#small-fixes)).

## After MVC: behaviour the native system does not reproduce

| ☐ | Differs / missing | Target | Owner |
| --- | --- | --- | --- |
| ☐ | Tenant Liquid has no view tags or filters: `shape`, `zone`, `layout`, `render_*`, `a`, `helper`/`block`/`form`, `cache*`, `page_title`, `httpcontext_*`, `contentitem`, the resource tags (`script`, `style`, `meta`, `link`, `resources`, `scriptblock`, `styleblock`) and the filters `shape_new`, `shape_render`, `shape_stringify`, `shape_properties`, `append_version`, `resource_url`; a template that uses one fails to parse | tree templates cover the display; resources come from the head contribution seam; list which tags tree templates still need | Grove.Display.Liquid, Grove.Display.Templates |
| ☐ | `SiteSettings.PageTitleFormat` has no consumer (its default used `page_title`) | the document's title contributor applies it | Grove.Display.Management |
| ☐ | The shape-level output cache and the cache tag helpers are gone; only `DisplayTreeCache` caches | decide whether display tree caching needs per-node cache scopes | Grove.Data.Cache.Dynamic |
| ☐ | No Users list filter syntax (`IUsersAdminListFilterParser` with Roles' and Members' `class:` providers) | filtering on the Users API (query parameters) | Grove.Access.Users |
| ☐ | Feature enable/disable shows no notice; enabling Secure Media logs a warning instead | the features page shows the API's messages | Grove.Infrastructure.Features |
| ☐ | The Debug settings group (shape debug info) is gone | the coverage report (`api/grove/components/coverage`) is the diagnostic | — |
| ☐ | The ReCaptcha tag helper, shape and `[ValidateReCaptcha]` are gone; only the login-extension captcha section remains | captcha on other forms through the same extension seam | Grove.Security.ReCaptcha |
| ☐ | The rule editor adds conditions from a dropdown (the MVC modal had thumbnail cards); the Layers list shows no rule summary | a summary column | Grove.Display.Rules, Grove.Display.Layers |
| ☐ | A part with fields and no driver loses the `contentpart-{name}` wrapper class | the `ContentPart` component adds it | Grove.Display.Components |
| ☐ | Razor-only theme helpers are gone (`GetUserByIdAsync`, `GetUsersByIdsAsync`, `GetTaxonomyTermAsync`, `GetInheritedTermsAsync`, `QueryCategorizedContentItemsAsync`, `ShortcodesToHtmlAsync`) and the Liquid `Navbar()` function | the equivalents as Liquid filters where tree templates need them | Grove.Access.Users, Grove.Contents.Taxonomies, Grove.Shortcodes |
| ☐ | Validation bodies changed: the tenant API answers problem+json with camelCase keys (was ModelState); IconProviders' error key is `iconify.baseUrl`; automatic `[ApiController]` 400s are explicit checks; problem responses from Hooks and Registry carry no `traceId` | clients read problem details only | Grove.Infrastructure.Tenants, Grove.Infrastructure.Server |
| ☐ | Remote deployment import answers 400/500 problem details on a recipe failure (was 200 with a notifier message) | the remote client reports the problem | Grove.Deployment.Remote |
| ☐ | The POST `ExternalLogin` form validates no antiforgery field (nothing posts to it; `api/grove/auth/external-login` is the live entry); "registrations disabled" travels in the `error` query | delete the form entry | Grove.Access.Users |
| ☐ | `OpenIdProtocolApi.Map` reads the server settings synchronously at pipeline build (`IApiContributor.Map` is synchronous) | an async map hook | Grove.Infrastructure.Server.Abstractions, Grove.Access.OpenId |
| ☐ | `ILegacyRouteOwnership` keeps MVC wording; it means "a non-Blazor endpoint owns the path" | rename | Grove.Infrastructure.Server |
| ☐ | The resource manager (`ResourceDefinition`, `LinkEntry`) builds tags with the framework's `TagBuilder` | plain element builders | Grove.Display.Resources.Management |
| ☐ | MiniProfiler references `MiniProfiler.AspNetCore.Mvc` | `MiniProfiler.AspNetCore` | Grove.Diagnostics.MiniProfiler |
| ☐ | Unused leftovers: the HTML localizer (`PortableObjectHtmlLocalizer` and its factory), Contents.Management.Display's editor helpers (`ForEditor`, `GetEditorAlternates`, `ContentPartShapeViewModel`) | delete | Grove.Data.Localization, Grove.Contents.Management.Display |
| ☐ | Not verified live: the head contributors (analytics, tag manager, Meta pixel, Facebook SDK, MiniProfiler), the admin `/not-found` page, the coverage endpoint with zero uncovered live shapes, the functional tests' `api/grove/queries` challenge through the "Api" scheme | the live run | — |

## All-features run

| ☐ | Differs / missing | Target | Owner |
| --- | --- | --- | --- |
| ☐ | ClamAV enabled without a host: uploads are not scanned (inert by request) | **decide**: a production check that refuses startup | Grove.Security.Antivirus |
| ☐ | The loopback rate-limit exemption uses a per-process secret header: behind a load balancer a server's call to itself may reach another instance and be limited | a tenant-wide secret, or exempt by the loopback client's identity | Grove.Security.RateLimits |
| ☐ | One workflow refusing a stimulus aborts the stimulus for the other subscribers (the Samples flow logs 5 "refused" warnings on import) | set aside per subscriber, not per stimulus ([workflows.md › Still to build](../workflows.md#still-to-build), the event side) | Grove.Workflows |
| ☐ | Field warnings on Fruitful's "Post invoice" and "Post customer payment" | fix the flows | Fruitful |
| ☐ | `dev.sh` reset can fail removing bin/obj while a build server still writes (retry works) | shut the build servers down first | tooling |
