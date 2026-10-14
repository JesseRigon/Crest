using Microsoft.Extensions.Options;
using Crest.Admin;
using Microsoft.Extensions.Localization;
using Crest.Navigation;

namespace Crest.OpenId;

public sealed class ServerAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    // The native admin page (step 7: the MVC OpenId screens are retired) is addressed by its
    // route under the tenant's admin prefix, not by an MVC action.
    private readonly string _adminPath;

    public ServerAdminMenu(IStringLocalizer<ClientAdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions)
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
                       .Add(S["Settings"], S["Settings"].PrefixPosition(), settings => settings
                            .Add(S["Authorization Server"], S["Authorization Server"].PrefixPosition(), server => server
                                .Url(_adminPath + "/OpenId/ServerConfiguration")
                                .Permission(OpenIdPermissions.ManageServerSettings)
                                .LocalNav()
                            )
                       )
                   )
               );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Settings"], settings => settings
                .Add(S["OpenID Connect"], S["OpenID Connect"].PrefixPosition(), openId => openId
                    .AddClass("openid")
                    .Id("openid")
                    .Add(S["Authorization Server"], S["Authorization Server"].PrefixPosition(), server => server
                        .Url(_adminPath + "/OpenId/ServerConfiguration")
                        .Permission(OpenIdPermissions.ManageServerSettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
