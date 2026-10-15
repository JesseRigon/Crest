using Grove.Admin.Models;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;

namespace Grove.Admin.Drivers;

public sealed class VisitSiteNavbarDisplayDriver : DisplayDriver<Navbar>
{
    public override IDisplayResult Display(Navbar model, BuildDisplayContext context)
    {
        return View("VisitSiteNavbarItem", model)
            .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:20");
    }
}
