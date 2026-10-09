using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;
using Crest.Taxonomies.Models;

namespace Crest.Taxonomies.ViewModels;

public class TaxonomyPartViewModel
{
    public string TaxonomyContentItemId => ContentItem.ContentItemId;

    [BindNever]
    public ContentItem ContentItem { get; set; }

    [BindNever]
    public TaxonomyPart TaxonomyPart { get; set; }
}
