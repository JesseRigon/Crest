using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Menu.Models;

namespace Crest.Menu.ViewModels;

public class HtmlMenuItemPartEditViewModel
{
    public string Name { get; set; }

    public string Url { get; set; }

    public string Target { get; set; }

    public string Html { get; set; }

    [BindNever]
    public HtmlMenuItemPart MenuItemPart { get; set; }
}
