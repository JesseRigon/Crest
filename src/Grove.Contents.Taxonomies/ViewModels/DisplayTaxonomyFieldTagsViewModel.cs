using Grove.Contents.Taxonomies.Fields;

namespace Grove.Contents.Taxonomies.ViewModels;

public class DisplayTaxonomyFieldTagsViewModel : DisplayTaxonomyFieldViewModel
{
    public string[] TagNames => Field.GetTagNames();
}
