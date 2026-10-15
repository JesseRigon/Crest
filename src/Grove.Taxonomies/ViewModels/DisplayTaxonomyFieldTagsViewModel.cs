using Grove.Taxonomies.Fields;

namespace Grove.Taxonomies.ViewModels;

public class DisplayTaxonomyFieldTagsViewModel : DisplayTaxonomyFieldViewModel
{
    public string[] TagNames => Field.GetTagNames();
}
