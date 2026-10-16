using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Display.Management.Views;
using Grove.Contents.Taxonomies.Models;
using Grove.Contents.Taxonomies.ViewModels;

namespace Grove.Contents.Taxonomies.Drivers;

public sealed class TaxonomyFieldSetDisplayDriver : ContentFieldSetDisplayDriver<TaxonomyFieldSet>
{
    public override IDisplayResult Display(TaxonomyFieldSet part, BuildFieldSetDisplayContext context)
    {
        var hasItems = TaxonomyFieldSet.Terms(part.ContentItem).Count > 0;

        return Initialize<TaxonomyFieldSetViewModel>(hasItems ? "Taxonomy" : "Taxonomy_Empty", m =>
        {
            m.ContentItem = part.ContentItem;
            m.TaxonomyFieldSet = part;
        }).Location(PlatformConstants.DisplayType.Detail, "Content");
    }
}
