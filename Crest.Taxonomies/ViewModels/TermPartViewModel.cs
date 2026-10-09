using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;

namespace Crest.Taxonomies.ViewModels;

public class TermPartViewModel
{
    public string TermContentItemId => ContentItem.ContentItemId;
    public string TaxonomyContentItemId { get; set; }
    public IEnumerable<ContentItem> ContentItems { get; set; }
    public dynamic Pager { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }
}
