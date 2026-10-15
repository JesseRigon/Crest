using Grove.Admin.Models;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;

namespace Grove.Users.Drivers;

public sealed class UserMenuNavbarDisplayDriver : DisplayDriver<Navbar>
{
    public override IDisplayResult Display(Navbar model, BuildDisplayContext context)
    {
        return View("NavbarUserMenu", model)
            .Location(PlatformConstants.DisplayType.Detail, "Content:after")
            .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:after");
    }
}
