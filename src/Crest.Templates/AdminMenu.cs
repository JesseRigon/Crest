using Crest.Admin;
using Crest.Navigation;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Crest.Templates;

/// <summary>Design › Templates: the admin page that edits templates (a Blazor page, so a URL).</summary>
public sealed class AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions) : AdminNavigationProvider
{
    private readonly IStringLocalizer S = stringLocalizer;

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');
        builder.Add(S["Design"], design => design
            .Add(S["Templates"], S["Templates"].PrefixPosition(), templates => templates
                .AddClass("icon-class-@iconify:mdi:file-code")
                .Id("templates")
                .Url($"{adminPath}/Templates")
                .Permission(Permissions.ManageTemplates)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
