using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Menu.Models;

namespace Grove.Menu.ViewModels;

public class ContentMenuItemPartEditViewModel
{
    public string Name { get; set; }

    public bool CheckContentPermissions { get; set; }

    [BindNever]
    public ContentMenuItemPart MenuItemPart { get; set; }
}
