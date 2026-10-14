using Microsoft.Extensions.Options;
using Crest.Admin;
using Microsoft.Extensions.Localization;
using Crest.Navigation;

namespace Crest.OpenId;

public sealed class ManagementAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    // The native admin pages (step 7: the MVC OpenId screens are retired) are addressed by
    // their route under the tenant's admin prefix, not by an MVC action.
    private readonly string _adminPath;

    public ManagementAdminMenu(IStringLocalizer<ManagementAdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions)
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
                .Add(S["OpenID Connect"], S["OpenID Connect"].PrefixPosition(), openId => openId
                    .AddClass("openid")
                    .Id("openid")
                    .Add(S["Management"], S["Management"].PrefixPosition(), management => management
                        .Add(S["Applications"], S["Applications"].PrefixPosition(), applications => applications
                            .Url(_adminPath + "/OpenId/Application")
                            .Permission(OpenIdPermissions.ManageApplications)
                            .LocalNav()
                        )
                        .Add(S["Scopes"], S["Scopes"].PrefixPosition(), applications => applications
                            .Url(_adminPath + "/OpenId/Scope")
                            .Permission(OpenIdPermissions.ManageScopes)
                            .LocalNav()
                        )
                    )
                )
            );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Access Control"], accessControl => accessControl
                .Add(S["OpenID Connect"], S["OpenID Connect"].PrefixPosition(), openId => openId
                    .AddClass("openid")
                    .Id("openid")
                    .Add(S["Applications"], S["Applications"].PrefixPosition(), applications => applications
                        .Url(_adminPath + "/OpenId/Application")
                        .Permission(OpenIdPermissions.ManageApplications)
                        .LocalNav()
                    )
                    .Add(S["Scopes"], S["Scopes"].PrefixPosition(), applications => applications
                        .Url(_adminPath + "/OpenId/Scope")
                        .Permission(OpenIdPermissions.ManageScopes)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
