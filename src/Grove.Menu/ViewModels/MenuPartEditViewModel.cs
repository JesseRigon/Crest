using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentManagement.Metadata.Models;
using Grove.Menu.Models;

namespace Grove.Menu.ViewModels;

public class MenuPartEditViewModel
{
    public string Hierarchy { get; set; }

    [BindNever]
    public MenuPart MenuPart { get; set; }

    [BindNever]
    public IEnumerable<ContentTypeDefinition> MenuItemContentTypes { get; set; }
}
