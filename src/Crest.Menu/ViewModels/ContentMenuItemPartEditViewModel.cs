using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Menu.Models;

namespace Crest.Menu.ViewModels;

public class ContentMenuItemPartEditViewModel
{
    public string Name { get; set; }

    public bool CheckContentPermissions { get; set; }

    [BindNever]
    public ContentMenuItemPart MenuItemPart { get; set; }
}
