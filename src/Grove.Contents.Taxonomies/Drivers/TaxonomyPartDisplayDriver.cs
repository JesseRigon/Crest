using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Display.Management.Views;
using Grove.Contents.Taxonomies.Models;
using Grove.Contents.Taxonomies.ViewModels;

namespace Grove.Contents.Taxonomies.Drivers;

public sealed class TaxonomyPartDisplayDriver : ContentPartDisplayDriver<TaxonomyPart>
{
    public override IDisplayResult Display(TaxonomyPart part, BuildPartDisplayContext context)
    {
        var hasItems = part.Terms.Count > 0;

        return Initialize<TaxonomyPartViewModel>(hasItems ? "TaxonomyPart" : "TaxonomyPart_Empty", m =>
        {
            m.ContentItem = part.ContentItem;
            m.TaxonomyPart = part;
        }).Location(PlatformConstants.DisplayType.Detail, "Content");
    }
}
