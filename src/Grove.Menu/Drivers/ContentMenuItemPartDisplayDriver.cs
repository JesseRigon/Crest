using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.DisplayManagement.Views;
using Grove.Menu.Models;

namespace Grove.Menu.Drivers;

public sealed class ContentMenuItemPartDisplayDriver : ContentPartDisplayDriver<ContentMenuItemPart>
{
    public override IDisplayResult Display(ContentMenuItemPart part, BuildPartDisplayContext context)
    {
        return Combine(
            Dynamic("ContentMenuItemPart_Admin", static (shape, part) =>
            {
                shape.MenuItemPart = part;
            }, part)
            .Location("Admin", "Content:10"),
            Dynamic("ContentMenuItemPart_Thumbnail", static (shape, part) =>
            {
                shape.MenuItemPart = part;
            }, part)
            .Location("Thumbnail", "Content:10")
        );
    }
}
