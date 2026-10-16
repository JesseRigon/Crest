using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.SiteMenu.Models;

namespace Grove.Contents.SiteMenu.Handlers;

public class MenuContentHandler : ContentHandlerBase
{
    public override Task ActivatedAsync(ActivatedContentContext context)
    {
        // A menu carries its MenuFieldSet; its items are its MenuItems embedding.
        if (context.ContentItem.ContentType == "Menu")
        {
            context.ContentItem.Weld<MenuFieldSet>(new { Position = "3" });
        }

        return Task.CompletedTask;
    }
}
