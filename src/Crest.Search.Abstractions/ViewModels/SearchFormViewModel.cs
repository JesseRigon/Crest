using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.DisplayManagement.Views;

namespace Crest.Search.ViewModels;

public class SearchFormViewModel : ShapeViewModel
{
    public SearchFormViewModel()
        : base("Search__Form")
    {
    }

    public SearchFormViewModel(string shapeType)
        : base(shapeType)
    {
    }

    public string Terms { get; set; }

    public string Index { get; set; }

    [BindNever]
    public string Placeholder { get; set; }
}
