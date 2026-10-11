using Crest.ContentManagement;

namespace Crest.Taxonomies.Models;

// This part is added automatically to all taxonomies.
public class TaxonomyPart : ContentPart
{
    public string TermContentType { get; set; }

    public List<ContentItem> Terms { get; set; } = [];
}
