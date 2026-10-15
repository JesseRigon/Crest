using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.SiteMenu.Models;

namespace Grove.Contents.SiteMenu.Handlers;

public class MenuContentHandler : ContentHandlerBase
{
    public override Task ActivatedAsync(ActivatedContentContext context)
    {
        // When a Menu is created, we add a MenuPart to it
        if (context.ContentItem.ContentType == "Menu")
        {
            context.ContentItem.Weld<MenuPart>(new { Position = "3" });
            context.ContentItem.Weld<MenuItemsListPart>(new { Position = "4" });
        }

        return Task.CompletedTask;
    }
}
