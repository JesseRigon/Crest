using Crest.Taxonomies.Fields;

namespace Crest.Taxonomies.ViewModels;

public class DisplayTaxonomyFieldTagsViewModel : DisplayTaxonomyFieldViewModel
{
    public string[] TagNames => Field.GetTagNames();
}
