using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Themes.Models;

namespace Crest.Themes.Drivers;

public sealed class ThemeEntryDisplayDriver : DisplayDriver<ThemeEntry>
{
    public override Task<IDisplayResult> DisplayAsync(ThemeEntry model, BuildDisplayContext context)
    {
        var results = new List<ShapeResult>()
        {
            View("ThemeEntry_SummaryAdmin__Thumbnail", model).Location(PlatformConstants.DisplayType.SummaryAdmin, "Thumbnail:5"),
            View("ThemeEntry_SummaryAdmin__Title", model).Location(PlatformConstants.DisplayType.SummaryAdmin, "Header:5"),
            View("ThemeEntry_SummaryAdmin__Descriptions", model).Location(PlatformConstants.DisplayType.SummaryAdmin, "Content:5"),
            View("ThemeEntry_SummaryAdmin__Attributes", model).Location(PlatformConstants.DisplayType.SummaryAdmin, "Tags:5"),
        };

        if (model.IsCurrent)
        {
            results.Add(View("ThemeEntry_SummaryAdmin__Current", model).Location(PlatformConstants.DisplayType.SummaryAdmin, "FooterStart:5"));
        }
        else
        {
            results.AddRange([
                View("ThemeEntry_SummaryAdmin__ButtonsMakeCurrent", model).Location(PlatformConstants.DisplayType.SummaryAdmin, "FooterStart:5"),
                View("ThemeEntry_SummaryAdmin__ButtonsToggleState", model).Location(PlatformConstants.DisplayType.SummaryAdmin, "FooterEnd:5")
            ]);
        }

        return CombineAsync(results);
    }
}
