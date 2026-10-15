using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Menu.Models;

namespace Grove.Menu.ViewModels;

public class LinkMenuItemPartEditViewModel
{
    public string Name { get; set; }

    public string Url { get; set; }

    public string Target { get; set; }

    [BindNever]
    public LinkMenuItemPart MenuItemPart { get; set; }
}
