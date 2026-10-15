# Admin route inventory

This list is generated from the current resolved Orchard admin navigation for the enabled features in the host. A route is marked `native` only when it has a dedicated, functional Grove `.razor` page backed by Orchard services. Pending routes continue to use Orchard's existing admin UI until their native implementation is complete.

| Menu | Route | Status |
| --- | --- | --- |
| New > Organization | `/Admin/Contents/ContentTypes/Organization/Create` | native document editor |
| New > Person | `/Admin/Contents/ContentTypes/Person/Create` | native document editor |
| New > Taxonomy | `/Admin/Contents/ContentTypes/Taxonomy/Create` | native document editor |
| Content > Admin Menus | `/Admin/AdminMenu/List` | native (MVC admin deleted) |
| Content > Site Menus | `/Admin/Contents/ContentItems/Menu` | native |
| Content > Content Definition > Content Types | `/Admin/ContentTypes/List` | native: create/delete types, attach/detach/reorder parts, type and part-on-type settings as JSON (MVC editor deleted) |
| Content > Content Definition > Content Parts | `/Admin/ContentTypes/ListParts` | native: create/delete parts, add/retype/rename/reorder/remove fields, part and field settings as JSON (the field/part settings drivers and their views deleted) |
| Content > Content Items | `/Admin/Contents/ContentItems` | native list/actions/document editor; save draft, discard draft, clone and the versions list on the tree editor |
| Media > Profiles | `/Admin/MediaProfiles` | native list/editor (MVC admin deleted) |
| Media > Library | `/Admin/Media` | native browse/upload (MVC library deleted; the media field editors keep the media gallery app) |
| Media > Options | `/Admin/Media/Options` | native read-only configuration, cache and storage provider options (MVC cache and options screens deleted) |
| Design > Themes | `/Admin/Themes` | native (MVC admin deleted) |
| Design > Design System | `/Admin/DesignSystem` | native |
| Design > Templates | `/Admin/Templates` | native: Liquid tree templates, drafts, publish, preview (blazor-display.md step 4) |
| Design > Placements | `/Admin/Placements` | native: placement rules per shape type (blazor-display.md step 6) |
| Design > Layers | `/Admin/Layers` | native: layer rules and the components they place into zone keys (widgets retired, step 6) |
| Design > Shortcodes | `/Admin/Shortcodes` | native list/editor of the shortcode templates, Liquid in `CodeEditor` (MVC admin deleted) |
| Design > Icons | `/Admin/Design/Icons` | native |
| Settings > General | `/Admin/Settings/general` | native |
| Settings > Admin | `/Admin/Settings/admin` | native |
| Settings > Access Control > Roles | `/Admin/Roles/Index` | native list/create/edit/clone/delete (MVC admin deleted) |
| Settings > Access Control > Users | `/Admin/Users/Index` | native list/create/edit/actions; unlock, confirm email, send verification, external sign-ins (MVC admin deleted) |
| Settings > Security > Security Headers | `/Admin/Settings/SecurityHeaders` | native settings editor |
| Settings > Security > User Login | `/Admin/Settings/userLogin` | native settings editor |
| Settings > Features | `/Admin/Features` | native (MVC admin deleted) |
| Settings > Recipes | `/Admin/Recipes` | native list/execute (MVC admin deleted) |
| Settings > Localization > Cultures | `/Admin/Settings/localization` | native settings editor |
| Settings > Search > Indexes | `/Admin/indexing` | native: create (per provider kind), edit as JSON, rebuild, reset, synchronize, delete/force-delete (MVC admin, the providers' profile drivers and the Lucene/Elasticsearch query screens deleted) |
| Settings > Search > Queries > All Queries | `/Admin/Queries/Index` | native list/editor (provider metadata as JSON; MVC admin and the query editor drivers deleted) |
| Settings > Search > Queries > Run SQL Query | `/Admin/Queries/Sql/Query` | native: SELECT-only runner through the query pipeline (`SqlQueryRunner`) |
| Settings > Communication > Email | `/Admin/Settings/email` | native: default provider, SMTP (password write-only), send a test (MVC settings drivers deleted) |
| Tools > Testing > Email Test | `/Admin/Email/Test` | native (same page) |
| Settings > Users > Registration, Reset password, Change email | `/Admin/Settings/userRegistration`, `userResetPassword`, `userChangeEmail` | native settings editors (with external sign-up rules) |
| Settings > Custom settings types | `/Admin/CustomSettings/{Type}` | native, on the content editor tree |
| Audit Trail > Events | `/Admin/Audit`, `/Admin/Audit/Records/{id}` | native list and record (MVC list deleted) |
| Settings > Localization > Dynamic Translations | `/Admin/DataLocalization` | native editor (MVC editor and statistics deleted) |
| Settings > Security > User Audit Trail | `/Admin/Users/AuditTrail/Index` | standard Orchard UI: the user-snapshot redactor settings |
| Audit Trail > Settings | `/Admin/Settings/AuditTrail` (`/Admin/Audit/Settings`) | native: events recorded, client IP, retention |
| Settings > Background Tasks | `/Admin/BackgroundTasks` | native: enable/disable, schedule and lock settings, run now |
| Notifications | `/Admin/notifications` | native full list: read/unread, delete, mark all read |
| Settings > Deployments > JSON Import | `/Admin/DeploymentPlan/Import/Json` | native |
| Settings > Deployments > Package Import | `/Admin/DeploymentPlan/Import/Index` | native upload (through the upload pipeline) |
| Settings > Deployments > Remote Clients | `/Admin/Deployment/RemoteClient/Index` | native list/editor (API key write-only) |
| Settings > Deployments > Remote Instances | `/Admin/Deployment/RemoteInstance/Index` | native list/editor (API key write-only) |
| Settings > Deployments > Plans | `/Admin/DeploymentPlan/Index`, `/Admin/DeploymentPlan/Display/{id}` | native list, steps (settings edited as the stored JSON), reorder, download, send to a remote instance |
| Settings > Tenants | `/Admin/Tenants`, `/Admin/Tenants/Create`, `/Admin/Tenants/Edit/{name}` | native list/actions, create, edit, set up in place or through the tenant's setup link |
| Security > OpenID Connect > Applications | `/Admin/OpenId/Application`, `…/Create`, `…/Edit/{id}` | native list/editor (client secret write-only; roles, scopes, flows and endpoints; only the flows the server allows are offered) |
| Security > OpenID Connect > Scopes | `/Admin/OpenId/Scope` (`…/Create`, `…/Edit/{id}` open the editor) | native list/editor |
| Security > OpenID Connect > Server | `/Admin/OpenId/ServerConfiguration` | native structured form (token format, authority, certificates picked from the available ones, endpoints on/off at their fixed paths, flows, token options); saving reloads the tenant |
| Security > OpenID Connect > Token Validation | `/Admin/OpenId/ValidationConfiguration` | native structured form (tenant or authority, metadata address, audience); saving reloads the tenant |
| Tools > Routes | `/Admin/Routes` | native: the page registry's routes, the home and content routes, the rewrite and redirect rules (Grove.Routes; the MVC URL rewriting screens deleted) |
| Tools > Search Engine Optimization > Sitemaps | `/Admin/Sitemaps/List`, `/Admin/Sitemaps/Display/{id}` | native list/editor and the sitemap's sources (MVC admin deleted) |
| Tools > Search Engine Optimization > Sitemap Indexes | `/Admin/SitemapIndexes/List` | native list/editor |
| Tools > Search Engine Optimization > Sitemaps Cache | `/Admin/SitemapsCache/List` | native: purge one or all |
| Tools > Testing > SMS Test | `/Admin/Sms/Test` | native |
| Tools > GraphiQL | `/Admin/GraphQL` | native frame over the module's static GraphiQL page |
| Settings > Multi-Tenancy > Feature Profiles | `/Admin/TenantFeatureProfiles` | native list/editor (Default tenant) |

The Deployments pages and the SQL runner read and write through `api/grove/deployment`,
and `api/grove/queries/sql` (Grove.Data.Queries' `QueriesApi`); package building
and import (`DeploymentPackages`) and the SQL run (`SqlQueryRunner`) are Grove.Deployment's and
Grove.Data.Queries' services; the MVC plan, import, step, remote client and remote instance screens
are deleted, and so are the MVC deployment-target executors: the content list adds items to a
plan, downloads them or exports them to a target through `ContentDeploymentApi`. A deployment step's settings are
edited as the JSON the plan stores, so a step type a module adds needs no component. Remote instance and
client API keys are write-only in the native pages: an edit keeps the stored key unless a
new one is typed.

Tenant create, edit and setup go through `api/grove/tenants` (`editor`, `POST`, `PUT {name}`,
`POST {name}/setup`, `GET {name}/setup-url`), whose rules are `Grove.Infrastructure.Tenants`'
`TenantAdministration`, shared with the stock `api/tenants` setup, so validation, the host's
database presets and patterns, and "an initialized tenant's database is never shown or
changed" hold on both. There are no MVC Tenants screens; the feature profiles page reads and
writes through `api/grove/tenants/feature-profiles`. The OpenID Connect administration goes through
`api/grove/openid/{applications,scopes,server,validation}` (Grove.Access.OpenId, each controller in its
own feature), and the menus link the native routes under the admin prefix; there are no MVC
OpenId admin screens. Both OpenId settings forms are structured, not JSON: the server's settings
are a fixed, small set. The protocol endpoints (`OpenIdProtocolApi`) stay;
their consent and logout confirmation are site pages
([shells-and-themes.md](shells-and-themes.md#account-and-protocol-pages)).

Each page declares its own menu placement in the page registry
([admin-menu.md](admin-menu.md) › The page registry).
