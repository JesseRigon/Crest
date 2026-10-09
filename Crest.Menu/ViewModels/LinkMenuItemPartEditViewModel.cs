using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Menu.Models;

namespace Crest.Menu.ViewModels;

public class LinkMenuItemPartEditViewModel
{
    public string Name { get; set; }

    public string Url { get; set; }

    public string Target { get; set; }

    [BindNever]
    public LinkMenuItemPart MenuItemPart { get; set; }
}
