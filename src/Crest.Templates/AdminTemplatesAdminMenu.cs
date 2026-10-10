using Microsoft.Extensions.Localization;
using Crest.Navigation;

namespace Crest.Templates;

public sealed class AdminTemplatesAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public AdminTemplatesAdminMenu(IStringLocalizer<AdminTemplatesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Design"], design => design
                .Add(S["Admin Templates"], S["Admin Templates"].PrefixPosition(), import => import
                    .Action("Admin", "Template", "Crest.Templates")
                    .Permission(AdminTemplatesPermissions.ManageAdminTemplates)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
