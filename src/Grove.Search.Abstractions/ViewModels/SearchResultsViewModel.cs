using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentManagement;
using Grove.DisplayManagement.Views;

namespace Grove.Search.ViewModels;

public class SearchResultsViewModel : ShapeViewModel
{
    public SearchResultsViewModel()
        : base("Search__Results")
    {
    }

    public SearchResultsViewModel(string shapeType)
        : base(shapeType)
    {
    }

    public string Index { get; set; }

    [BindNever]
    public IEnumerable<ContentItem> ContentItems { get; set; }

    [BindNever]
    public Dictionary<string, IReadOnlyDictionary<string, IReadOnlyCollection<string>>> Highlights { get; set; }
}
