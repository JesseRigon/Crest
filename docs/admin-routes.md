# Admin route inventory

This list is generated from the current resolved Orchard admin navigation for the enabled features in the host. A route is marked `native` only when it has a dedicated, functional Crest `.razor` page backed by Orchard services. Pending routes continue to use Orchard's existing admin UI until their native implementation is complete.

| Menu | Route | Status |
| --- | --- | --- |
| New > Organization | `/Admin/Contents/ContentTypes/Organization/Create` | native document editor |
| New > Person | `/Admin/Contents/ContentTypes/Person/Create` | native document editor |
| New > Taxonomy | `/Admin/Contents/ContentTypes/Taxonomy/Create` | native document editor |
| Content > Admin Menus | `/Admin/AdminMenu/List` | native |
| Content > Site Menus | `/Admin/Contents/ContentItems/Menu` | native |
| Content > Content Definition > Content Types | `/Admin/ContentTypes/List` | native browse |
| Content > Content Definition > Content Parts | `/Admin/ContentTypes/ListParts` | native browse |
| Content > Content Items | `/Admin/Contents/ContentItems` | native list/actions/document editor |
| Media > Profiles | `/Admin/MediaProfiles` | native list/editor |
| Media > Library | `/Admin/Media` | native browse/upload |
| Media > Options | `/Admin/Media/Options` | native read-only configuration |
| Design > Themes | `/Admin/Themes` | native |
| Design > Design System | `/Admin/DesignSystem` | native |
| Design > Templates | `/Admin/Templates` | native: Liquid tree templates, drafts, publish, preview (blazor-display.md step 4) |
| Design > Placements | `/Admin/Placements` | native: placement rules per shape type (blazor-display.md step 6) |
| Design > Layers | `/Admin/Layers` | native: layer rules and the components they place into zone keys (widgets retired, step 6) |
| Design > Shortcodes | `/Admin/Shortcodes` | standard Orchard UI (deferred) |
| Design > Icons | `/Admin/Design/Icons` | native |
| Settings > General | `/Admin/Settings/general` | native |
| Settings > Admin | `/Admin/Settings/admin` | native |
| Settings > Access Control > Roles | `/Admin/Roles/Index` | native browse |
| Settings > Access Control > Users | `/Admin/Users/Index` | native list/create/edit/actions |
| Settings > Security > Security Headers | `/Admin/Settings/SecurityHeaders` | native settings editor |
| Settings > Security > User Login | `/Admin/Settings/userLogin` | native settings editor |
| Settings > Features | `/Admin/Features` | native |
| Settings > Recipes | `/Admin/Recipes` | native list/execute |
| Settings > Localization > Cultures | `/Admin/Settings/localization` | native settings editor |
| Settings > Search > Indexes | `/Admin/indexing` | native list/rebuild |
| Settings > Search > Queries > All Queries | `/Admin/Queries/Index` | native list/editor (provider metadata as JSON) |
| Settings > Search > Queries > Run SQL Query | `/Admin/Queries/Sql/Query` | native: SELECT-only runner through the query pipeline (`SqlQueryRunner`, shared with the MVC screen) |
| Settings > Deployments > Debugging | `/Admin/Settings/debugging` | native settings editor |
| Settings > Deployments > JSON Import | `/Admin/DeploymentPlan/Import/Json` | native |
| Settings > Deployments > Package Import | `/Admin/DeploymentPlan/Import/Index` | native upload (through the upload pipeline) |
| Settings > Deployments > Remote Clients | `/Admin/Deployment/RemoteClient/Index` | native list/editor (API key write-only) |
| Settings > Deployments > Remote Instances | `/Admin/Deployment/RemoteInstance/Index` | native list/editor (API key write-only) |
| Settings > Deployments > Plans | `/Admin/DeploymentPlan/Index`, `/Admin/DeploymentPlan/Display/{id}` | native list, steps (settings edited as the stored JSON), reorder, download, send to a remote instance |

The Deployments pages and the SQL runner read and write through `api/crest/deployment`,
`api/crest/settings/debugging` and `api/crest/queries/sql` (Crest.Server); package building
and import (`DeploymentPackages`) and the SQL run (`SqlQueryRunner`) are shared with the stock
MVC controllers, which stay until the MVC admin is retired. A deployment step's settings are
edited as the JSON the plan stores, so a step type a module adds needs no component; the
stock per-step editors (display drivers) remain only on the MVC screens. Remote instance and
client API keys are write-only in the native pages: an edit keeps the stored key unless a
new one is typed.
