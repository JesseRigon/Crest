using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.DisplayManagement.Views;
using Grove.Mvc.ModelBinding;
using Grove.Taxonomies.Models;
using Grove.Taxonomies.ViewModels;

namespace Grove.Taxonomies.Drivers;

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
