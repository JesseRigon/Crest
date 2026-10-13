using Crest.Admin;
using Crest.Navigation;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Crest.Placements;

/// <summary>Design › Placements: the admin page that edits the tenant's placement rules (a Blazor page, so a URL).</summary>
public sealed class AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions) : AdminNavigationProvider
{
    private readonly IStringLocalizer S = stringLocalizer;

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');
        builder.Add(S["Design"], design => design
            .Add(S["Placements"], S["Placements"].PrefixPosition(), placements => placements
                .AddClass("icon-class-@iconify:mdi:view-dashboard-edit")
                .Id("placements")
                .Url($"{adminPath}/Placements")
                .Permission(Permissions.ManagePlacements)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
