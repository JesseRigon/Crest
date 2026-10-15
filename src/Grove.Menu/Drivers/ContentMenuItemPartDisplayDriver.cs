using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.DisplayManagement.Views;
using Grove.Menu.Models;
using Grove.Menu.ViewModels;

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

    public override IDisplayResult Edit(ContentMenuItemPart part, BuildPartEditorContext context)
    {
        return Initialize<ContentMenuItemPartEditViewModel>("ContentMenuItemPart_Edit", model =>
        {
            model.Name = part.ContentItem.DisplayText;
            model.CheckContentPermissions = part.CheckContentPermissions;
            model.MenuItemPart = part;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentMenuItemPart part, UpdatePartEditorContext context)
    {
        var model = new ContentMenuItemPartEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.ContentItem.DisplayText = model.Name;
        part.CheckContentPermissions = model.CheckContentPermissions;

        return Edit(part, context);
    }
}
