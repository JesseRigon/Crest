using Crest.Access;
using Crest.Admin;
using Crest.Contents;
using Crest.Indexing;
using Crest.Indexing;
using Crest.Localization;
using Crest.Media;
using Crest.Recipes;
using Crest.Roles;
using Crest.Security;
using Crest.Security.Permissions;
using Crest.Settings;
using Crest.Users;

namespace Crest.Services;

/// <summary>
/// Admin routes (templates as in <see cref="Crest.Access.IRoutePermissionProvider"/>, relative to
/// the admin path) whose pages must run in the browser: the admin document for them is
/// rendered InteractiveWebAssembly instead of InteractiveAuto, as the login shell is. For
/// a page that hosts a client-only component library - one whose services and HTTP
/// clients exist only in the WASM client (Crest.Components.Modules.ICrestClientModule),
/// not in a server circuit.
/// </summary>
public interface IWebAssemblyRouteProvider
{
    IEnumerable<string> GetWebAssemblyRoutes();
}

/// <summary>The core admin pages' routes, registered with the Access page gate.</summary>
public sealed class RoutePermissionProvider : IRoutePermissionProvider
{
    public IEnumerable<RoutePermission> GetRoutes() =>
    [
        // "" is the bare AdminPath request itself (e.g. "/backoffice" with no further
        // segment - adminRemainder is an empty string, not "/"). That's Home.razor's
        // own "@page "/"" route, which immediately redirects an authenticated user to
        // Dashboard - so it needs the same AccessAdminPanel gate as Dashboard itself,
        // just under Matches' own empty-template-matches-empty-path shape.
        new("", AdminPermissions.AccessAdminPanel.Name),
        new("/Dashboard", AdminPermissions.AccessAdminPanel.Name),
        new("/Features", Crest.Features.FeaturesPermissions.ManageFeatures.Name),
        new("/Themes", Crest.Themes.Permissions.ApplyTheme.Name),
        new("/AdminMenus", Crest.AdminMenu.AdminMenuPermissions.ManageAdminMenu.Name),
        new("/AdminMenu", Crest.AdminMenu.AdminMenuPermissions.ManageAdminMenu.Name),
        new("/AdminMenu/List", Crest.AdminMenu.AdminMenuPermissions.ManageAdminMenu.Name),
        new("/Menus", Crest.Menu.Permissions.ManageMenu.Name),
        new("/Menu", Crest.Menu.Permissions.ManageMenu.Name),
        new("/Menu/List", Crest.Menu.Permissions.ManageMenu.Name),
        new("/Contents/ContentItems/Menu", Crest.Menu.Permissions.ManageMenu.Name),
        new("/Contents/ContentItems", Crest.Contents.CommonPermissions.ListContent.Name),
        new("/Contents/ContentTypes/{ContentType}/Create", Crest.Contents.CommonPermissions.EditContent.Name),
        new("/Contents/ContentItems/{ContentItemId}/Edit", Crest.Contents.CommonPermissions.EditContent.Name),
        new("/Contents/ContentItems/{ContentItemId}/Display", Crest.Contents.CommonPermissions.ViewContent.Name),
        new("/ContentTypes/List", ContentTypesPermissions.ViewContentTypes.Name),
        new("/ContentTypes/ContentPartLists", Crest.Permissions.ContentPartListPermissions.ViewContentPartLists.Name),
        new("/ContentTypes/ListParts", ContentTypesPermissions.ViewContentTypes.Name),
        new("/Users/Index", UsersPermissions.ListUsers.Name),
        new("/Users/Create", UsersPermissions.EditUsers.Name),
        new("/Users/Edit/{Id}", UsersPermissions.EditUsers.Name),
        new("/Tenants", Crest.Tenants.Permissions.ManageTenants.Name),
        new("/Tenants/Create", Crest.Tenants.Permissions.ManageTenants.Name),
        new("/Tenants/Edit/{name}", Crest.Tenants.Permissions.ManageTenants.Name),
        // The permission model's editors (docs/permissions.md › Who manages the permission model).
        new("/Roles/Index", Crest.Access.AccessPermissions.ReadRoles.Name),
        new("/PermissionSets", Crest.Access.AccessPermissions.ReadPermissionSets.Name),
        new("/Groups", Crest.Access.AccessPermissions.ReadGroups.Name),
        new("/Media", MediaPermissions.ManageMedia.Name),
        new("/Media/Options", MediaPermissions.ViewMediaOptions.Name),
        new("/MediaProfiles", MediaPermissions.ManageMediaProfiles.Name),
        new("/indexing", IndexingPermissions.ManageIndexes.Name),
        new("/Queries/Index", Crest.Queries.Permissions.ManageQueries.Name),
        new("/Queries/Sql/Query", Crest.Queries.QueriesPermissions.ManageSqlQueries.Name),
        new("/Settings/debugging", SettingsPermissions.ManageDebuggingSettings.Name),
        // Step 7 native pages (docs/blazor-display.md); permission names as the owning modules declare them.
        new("/Settings/email", "ManageEmailSettings"),
        new("/Email/Test", "ManageEmailSettings"),
        new("/BackgroundTasks", "ManageBackgroundTasks"),
        new("/notifications", "ManageNotifications"),
        // Custom settings: the API checks each type's own permission (ManageCustomSettings_{type}).
        new("/CustomSettings", AdminPermissions.AccessAdminPanel.Name),
        new("/CustomSettings/{Type}", AdminPermissions.AccessAdminPanel.Name),
        // Deployments (Pages/Deployment*.razor): the stock MVC URLs, so the menu lands on them.
        new("/DeploymentPlan/Index", Crest.Deployment.DeploymentPermissions.ManageDeploymentPlan.Name),
        new("/DeploymentPlan/Display/{Id}", Crest.Deployment.DeploymentPermissions.ManageDeploymentPlan.Name),
        new("/DeploymentPlan/Import/Index", Crest.Deployment.DeploymentPermissions.Import.Name),
        new("/DeploymentPlan/Import/Json", Crest.Deployment.DeploymentPermissions.Import.Name),
        new("/Deployment/RemoteInstance/Index", Crest.Deployment.DeploymentPermissions.ManageRemoteInstances.Name),
        new("/Deployment/RemoteClient/Index", Crest.Deployment.DeploymentPermissions.ManageRemoteClients.Name),
        new("/Recipes", RecipePermissions.ManageRecipes.Name),
        new("/Templates", Crest.Templates.Permissions.ManageTemplates.Name),
        new("/Layers", Crest.Layers.Permissions.ManageLayers.Name),
        new("/Placements", Crest.Placements.Permissions.ManagePlacements.Name),
        new("/Settings/general", SettingsPermissions.ManageSettings.Name),
        new("/Settings/admin", AdminPermissions.ManageAdminSettings.Name),
        new("/Settings/localization", LocalizationPermissions.ManageCultures.Name),
        // The Crest translations editor (Pages/Translations.razor) shadows the stock
        // DataLocalization URLs so the existing admin menu link and the Localization page's
        // "Edit translations" button land on it instead of the legacy frame. View-level gate
        // only - the API enforces per-culture edit rights on top.
        new("/DataLocalization", Crest.Localization.Data.DataLocalizationPermissions.ViewDynamicTranslations.Name),
        new("/DataLocalization/Index", Crest.Localization.Data.DataLocalizationPermissions.ViewDynamicTranslations.Name),
        new("/Settings/userLogin", UsersPermissions.ManageUsers.Name),
        new("/Settings/userRegistration", UsersPermissions.ManageUsers.Name),
        new("/Settings/userResetPassword", UsersPermissions.ManageUsers.Name),
        new("/Settings/userChangeEmail", UsersPermissions.ManageUsers.Name),
        new("/Settings/uploadScanning", SettingsPermissions.ManageSettings.Name),
        new("/Settings/files", SettingsPermissions.ManageSettings.Name),
        // The drive browser: the file-object API decides what is listed (docs/media.md § 8).
        new("/Files", AdminPermissions.AccessAdminPanel.Name),
        new("/Files/user-drive/{DriveId}", AdminPermissions.AccessAdminPanel.Name),
        new("/Settings/SecurityHeaders", SecurityPermissions.ManageSecurityHeadersSettings.Name),
        new("/Settings", SecurityPermissions.ManageSecurityHeadersSettings.Name),
        new("/Design/Icons", SettingsPermissions.ManageSettings.Name),
        new("/Icons", SettingsPermissions.ManageSettings.Name),
        new("/DesignSystem", SettingsPermissions.ManageSettings.Name),
    ];
}

