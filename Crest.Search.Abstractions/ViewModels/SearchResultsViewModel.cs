using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;
using Crest.DisplayManagement.Views;

namespace Crest.Search.ViewModels;

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
