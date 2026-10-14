using Microsoft.Extensions.Localization;
using Crest.Contents;
using Crest.Admin;
using Crest.Navigation;
using Microsoft.Extensions.Options;

namespace Crest.ContentTypes;

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
                .Add(S["Content"], content => content
                    .Add(S["Content Definition"], S["Content Definition"].PrefixPosition("9"), contentDefinition => contentDefinition
                        .Add(S["Content Types"], S["Content Types"].PrefixPosition("1"), contentTypes => contentTypes
                            .Url($"{_adminPath}/ContentTypes/List")
                            .Permission(ContentTypesPermissions.ViewContentTypes)
                            .LocalNav()
                        )
                        .Add(S["Content Parts"], S["Content Parts"].PrefixPosition("2"), contentParts => contentParts
                            .Url($"{_adminPath}/ContentTypes/ListParts")
                            .Permission(ContentTypesPermissions.ViewContentTypes)
                            .LocalNav()
                        )
                    )
                );
        }

        builder
            .Add(S["Design"], content => content
                .Add(S["Content Definition"], S["Content Definition"].PrefixPosition(), contentDefinition => contentDefinition
                    .Add(S["Content Types"], S["Content Types"].PrefixPosition("1"), contentTypes => contentTypes
                        .Url($"{_adminPath}/ContentTypes/List")
                        .Permission(ContentTypesPermissions.ViewContentTypes)
                        .LocalNav()
                    )
                    .Add(S["Content Parts"], S["Content Parts"].PrefixPosition("2"), contentParts => contentParts
                        .Url($"{_adminPath}/ContentTypes/ListParts")
                        .Permission(ContentTypesPermissions.ViewContentTypes)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
