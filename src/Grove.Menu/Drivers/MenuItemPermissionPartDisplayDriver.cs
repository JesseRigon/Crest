using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.DisplayManagement.Views;
using Grove.Menu.Models;
using Grove.Menu.ViewModels;
using Grove.Security.Permissions;

namespace Grove.Menu.Drivers;

public sealed class MenuItemPermissionPartDisplayDriver : ContentPartDisplayDriver<MenuItemPermissionPart>
{
    private readonly IPermissionService _permissionService;

    public MenuItemPermissionPartDisplayDriver(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public override IDisplayResult Edit(MenuItemPermissionPart part, BuildPartEditorContext context)
    {
        return Initialize<MenuItemPermissionViewModel>("MenuItemPermissionPart_Edit", async model =>
        {
            var selectedPermissions = await _permissionService.FindByNamesAsync(part.PermissionNames);

            model.SelectedItems = selectedPermissions
                .Select(p => new PermissionViewModel
                {
                    Name = p.Name,
                    DisplayText = p.Description,
                }).ToArray();

            var permissions = await _permissionService.GetPermissionsAsync();

            model.AllItems = permissions
                .Select(p => new PermissionViewModel
                {
                    Name = p.Name,
                    DisplayText = p.Description,
                }).ToArray();
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(MenuItemPermissionPart part, UpdatePartEditorContext context)
    {
        var model = new MenuItemPermissionViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix,
            x => x.SelectedPermissionNames);

        var selectedPermissions = model.SelectedPermissionNames == null
            ? []
            : model.SelectedPermissionNames.Split(',', StringSplitOptions.RemoveEmptyEntries);

        var permissions = await _permissionService.FindByNamesAsync(selectedPermissions);

        part.PermissionNames = permissions.Select(x => x.Name).ToArray();

        return Edit(part, context);
    }
}
