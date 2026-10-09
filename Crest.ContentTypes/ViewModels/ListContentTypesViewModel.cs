using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crest.ContentTypes.ViewModels;

public class ListContentTypesViewModel
{
    [BindNever]
    public IEnumerable<EditTypeViewModel> Types { get; set; }
}
