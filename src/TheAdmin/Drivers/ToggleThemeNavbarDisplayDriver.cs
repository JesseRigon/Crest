using Crest.Admin.Models;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Settings;

namespace Crest.Themes.TheAdmin.Drivers;

public sealed class ToggleThemeNavbarDisplayDriver : DisplayDriver<Navbar>
{
    private readonly ISiteService _siteService;

    public ToggleThemeNavbarDisplayDriver(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public override IDisplayResult Display(Navbar model, BuildDisplayContext context)
    {
        return View("ToggleTheme", model)
            .RenderWhen(static async (siteService) => (await siteService.GetSettingsAsync<AdminSettings>()).DisplayThemeToggler, _siteService)
            .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:10");
    }
}
