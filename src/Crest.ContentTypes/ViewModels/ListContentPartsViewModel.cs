using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crest.ContentTypes.ViewModels;

public class ListContentPartsViewModel
{
    [BindNever]
    public IEnumerable<EditPartViewModel> Parts { get; set; }
}
