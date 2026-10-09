using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement.Metadata.Models;
using Crest.Menu.Models;

namespace Crest.Menu.ViewModels;

public class MenuPartEditViewModel
{
    public string Hierarchy { get; set; }

    [BindNever]
    public MenuPart MenuPart { get; set; }

    [BindNever]
    public IEnumerable<ContentTypeDefinition> MenuItemContentTypes { get; set; }
}
