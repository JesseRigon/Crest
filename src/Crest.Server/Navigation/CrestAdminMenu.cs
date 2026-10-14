using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Crest.Admin;
using Crest.Navigation;
using Crest.Settings;

namespace Crest.Navigation;

// Stock Crest admin menu providers use .Action(...), which MVC's
// IUrlHelper/LinkGenerator resolves against the tenant's real, configured
// AdminOptions.AdminUrlPrefix automatically - e.g. AdminUrlPrefix "backoffice"
// replaces the literal word "Admin" in every URL (see
// AdminAreaControllerRouteMapper upstream), it isn't layered on top of it. Design
// System/Icons have no MVC controller action to route to (Blazor-only pages), so
// .Url(...) is the only option - building it from the real AdminOptions.AdminUrlPrefix
// keeps these two links correct under a custom prefix, matching the same
// "{realAdminPrefix}/DesignSystem" shape stock links get. DesignSystem.razor/
// Icons.razor's own "@page" directives carry no "/Admin" segment either - Blazor's
// Router resolves them relative to <base href>, already rewritten to the real
// prefix by the admin document root (Crest.Server/Components/App.razor).
public sealed class CrestAdminMenu(IStringLocalizer<CrestAdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions) : AdminNavigationProvider
{
    private readonly IStringLocalizer S = stringLocalizer;

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');

        builder.Add(S["Design"], NavigationConstants.AdminMenuDesignPosition, design => design
            .AddClass("design")
            .Id("design")
            .Add(S["Design System"], S["Design System"].PrefixPosition(), designSystem => designSystem
                .AddClass("design-system")
                .AddClass("icon-class-@iconify:mdi:palette")
                .Id("design-system")
                .Url($"{adminPath}/DesignSystem")
                .Permission(SettingsPermissions.ManageSettings)
                .LocalNav()
            )
            .Add(S["Icons"], S["Icons"].PrefixPosition(), icons => icons
                .AddClass("icons")
                .AddClass("icon-class-@iconify:mdi:shape")
                .Id("icons")
                .Url($"{adminPath}/Design/Icons")
                .Permission(SettingsPermissions.ManageSettings)
                .LocalNav()
            ), priority: 1);

        // The permission model's editors (docs/permissions.md › Editors): Blazor-only pages.
        builder.Add(S["Access Control"], accessControl => accessControl
            .Add(S["Permission Sets"], S["Permission Sets"].PrefixPosition(), sets => sets
                .AddClass("permission-sets")
                .Id("permission-sets")
                .Url($"{adminPath}/PermissionSets")
                .Permission(Crest.Access.AccessPermissions.ReadPermissionSets)
                .LocalNav())
            .Add(S["Groups"], S["Groups"].PrefixPosition(), groups => groups
                .AddClass("access-groups")
                .Id("access-groups")
                .Url($"{adminPath}/Groups")
                .Permission(Crest.Access.AccessPermissions.ReadGroups)
                .LocalNav()));

        // The tenant's upload scanning choice (Crest.Media.Scanning, always enabled): a
        // Blazor-only page, so a .Url like the design entries above.
        builder.Add(S["Settings"], settings => settings
            .Add(S["Upload Scanning"], S["Upload Scanning"].PrefixPosition(), scanning => scanning
                .AddClass("upload-scanning")
                .Id("upload-scanning")
                .Url($"{adminPath}/Settings/uploadScanning")
                .Permission(SettingsPermissions.ManageSettings)
                .LocalNav()
            ));

        // The drive browser and the file system's settings (docs/media.md § 3, § 8): Blazor-only
        // pages. The browser carries no permission of its own: what it lists is what the
        // file-object API returns for the caller.
        builder.Add(S["Media"], media => media
            .Add(S["Files"], S["Files"].PrefixPosition("0"), files => files
                .AddClass("files")
                .AddClass("icon-class-@iconify:mdi:folder-multiple")
                .Id("files")
                .Url($"{adminPath}/Files")
                .LocalNav()
            ));

        builder.Add(S["Settings"], settings => settings
            .Add(S["Files"], S["Files"].PrefixPosition(), files => files
                .AddClass("file-settings")
                .Id("file-settings")
                .Url($"{adminPath}/Settings/files")
                .Permission(SettingsPermissions.ManageSettings)
                .LocalNav()
            ));

        // The Content Part Lists entry under Configuration moved to the Crest.ContentPartLists
        // module's own navigation provider - it only belongs in the menu when that
        // feature is enabled.
        return ValueTask.CompletedTask;
    }
}
