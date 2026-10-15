using Microsoft.Extensions.Localization;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.DisplayManagement.Views;
using Grove.Mvc.ModelBinding;
using Grove.Title.Models;
using Grove.Title.ViewModels;

namespace Grove.Title.Drivers;

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
