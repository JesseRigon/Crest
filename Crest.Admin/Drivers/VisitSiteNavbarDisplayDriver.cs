using Crest.Admin.Models;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.Admin.Drivers;

public sealed class VisitSiteNavbarDisplayDriver : DisplayDriver<Navbar>
{
    public override IDisplayResult Display(Navbar model, BuildDisplayContext context)
    {
        return View("VisitSiteNavbarItem", model)
            .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:20");
    }
}
