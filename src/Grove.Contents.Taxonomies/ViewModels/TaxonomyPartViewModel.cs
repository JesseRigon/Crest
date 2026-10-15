using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Contents.Management;
using Grove.Contents.Taxonomies.Models;

namespace Grove.Contents.Taxonomies.ViewModels;

public class TaxonomyPartViewModel
{
    public string TaxonomyContentItemId => ContentItem.ContentItemId;

    /// <summary>The term tree with each term's link, for the display component.</summary>
    public IReadOnlyList<TaxonomyTermNode> Terms => TaxonomyLinks.Tree(TaxonomyPart?.Terms ?? []);

    [BindNever]
    public ContentItem ContentItem { get; set; }

    [BindNever]
    public TaxonomyPart TaxonomyPart { get; set; }
}
