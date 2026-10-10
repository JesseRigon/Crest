using Crest.Admin.Models;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.Users.Drivers;

public sealed class UserMenuNavbarDisplayDriver : DisplayDriver<Navbar>
{
    public override IDisplayResult Display(Navbar model, BuildDisplayContext context)
    {
        return View("NavbarUserMenu", model)
            .Location(PlatformConstants.DisplayType.Detail, "Content:after")
            .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:after");
    }
}
