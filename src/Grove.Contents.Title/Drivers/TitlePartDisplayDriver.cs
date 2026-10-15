using Microsoft.Extensions.Localization;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Display.Management.Views;
using Grove.Contents.Title.Models;
using Grove.Contents.Title.ViewModels;

namespace Grove.Contents.Title.Drivers;

public sealed class TitlePartDisplayDriver : ContentPartDisplayDriver<TitlePart>
{
    internal readonly IStringLocalizer S;

    public TitlePartDisplayDriver(IStringLocalizer<TitlePartDisplayDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Display(TitlePart titlePart, BuildPartDisplayContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<TitlePartSettings>();

        if (!settings.RenderTitle || string.IsNullOrWhiteSpace(titlePart.Title))
        {
            return null;
        }

        return Initialize<TitlePartViewModel, TitlePart>(GetDisplayShapeType(context), static (model, titlePart) =>
        {
            model.Title = titlePart.ContentItem.DisplayText;
            model.TitlePart = titlePart;
            model.ContentItem = titlePart.ContentItem;
        }, titlePart).Location(PlatformConstants.DisplayType.Detail, "Header")
        .Location(PlatformConstants.DisplayType.Summary, "Header");
    }
}
