using Microsoft.Extensions.Options;
using Crest.Admin;
using Microsoft.Extensions.Localization;
using Crest.Navigation;

namespace Crest.OpenId;

public sealed class ValidationAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    // The native admin pages (step 7: the MVC OpenId screens are retired) are addressed by
    // their route under the tenant's admin prefix, not by an MVC action.
    private readonly string _adminPath;

    public ValidationAdminMenu(IStringLocalizer<ValidationAdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions)
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
                            .Add(S["Token Validation"], S["Token Validation"].PrefixPosition(), validation => validation
                                .Url(_adminPath + "/OpenId/ValidationConfiguration")
                                .Permission(OpenIdPermissions.ManageValidationSettings)
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
                    .Add(S["Token Validation"], S["Token Validation"].PrefixPosition(), validation => validation
                        .Url(_adminPath + "/OpenId/ValidationConfiguration")
                        .Permission(OpenIdPermissions.ManageValidationSettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
