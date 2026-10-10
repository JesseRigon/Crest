using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Settings;
using Crest.Users.Models;

namespace Crest.Users.Drivers;

public sealed class ChangeEmailUserMenuDisplayDriver : DisplayDriver<UserMenu>
{
    private readonly ISiteService _siteService;

    public ChangeEmailUserMenuDisplayDriver(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public override IDisplayResult Display(UserMenu model, BuildDisplayContext context)
    {
        return View("UserMenuItems__ChangeEmail", model)
            .RenderWhen(static async (siteService) => (await siteService.GetSettingsAsync<ChangeEmailSettings>()).AllowChangeEmail, _siteService)
            .Location(PlatformConstants.DisplayType.Detail, "Content:20")
            .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:20")
            .Differentiator("ChangeEmail");
    }
}
