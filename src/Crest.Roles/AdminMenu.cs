using Microsoft.Extensions.Localization;
using Crest.Admin;
using Crest.Navigation;
using Microsoft.Extensions.Options;

namespace Crest.Roles;

public sealed class AdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    private readonly string _adminPath;

    public AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions)
    {
        S = stringLocalizer;
        _adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (NavigationHelper.UseLegacyFormat())
        {
            builder
                .Add(S["Security"], security => security
                    .Add(S["Roles"], S["Roles"].PrefixPosition(), roles => roles
                        .AddClass("roles")
                        .Id("roles")
                        .Url($"{_adminPath}/Roles/Index")
                        .Permission(RolesPermissions.ViewRoles)
                        .LocalNav()
                    )
                );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Access Control"], accessControl => accessControl
                .Add(S["Roles"], S["Roles"].PrefixPosition(), roles => roles
                    .AddClass("roles")
                    .Id("roles")
                    .Url($"{_adminPath}/Roles/Index")
                    .Permission(RolesPermissions.ViewRoles)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
