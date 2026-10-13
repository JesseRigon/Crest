using Crest.Admin;
using Crest.Navigation;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Crest.Layers;

/// <summary>Design › Layers: the admin page that edits the layers and what they place (a Blazor page, so a URL).</summary>
public sealed class AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions) : AdminNavigationProvider
{
    private readonly IStringLocalizer S = stringLocalizer;

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');
        builder.Add(S["Design"], design => design
            .Add(S["Layers"], S["Layers"].PrefixPosition(), layers => layers
                .AddClass("icon-class-@iconify:mdi:layers")
                .Id("layers")
                .Url($"{adminPath}/Layers")
                .Permission(Permissions.ManageLayers)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
