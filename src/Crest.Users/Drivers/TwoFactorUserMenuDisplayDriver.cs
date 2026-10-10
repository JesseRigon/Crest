using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Users.Models;

namespace Crest.Users.Drivers;

public sealed class TwoFactorUserMenuDisplayDriver : DisplayDriver<UserMenu>
{
    public override IDisplayResult Display(UserMenu model, BuildDisplayContext context)
    {
        return View("UserMenuItems__TwoFactor", model)
            .Location(PlatformConstants.DisplayType.Detail, "Content:15")
            .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:15")
            .Differentiator("TwoFactor");
    }
}
