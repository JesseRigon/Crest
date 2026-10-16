using Microsoft.Extensions.Localization;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Display.Management.Views;
using Grove.Contents.Title.Models;
using Grove.Contents.Title.ViewModels;

namespace Grove.Contents.Title.Drivers;

public sealed class TitleFieldSetDisplayDriver : ContentFieldSetDisplayDriver<TitleFieldSet>
{
    internal readonly IStringLocalizer S;

    public TitleFieldSetDisplayDriver(IStringLocalizer<TitleFieldSetDisplayDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Display(TitleFieldSet titleFieldSet, BuildFieldSetDisplayContext context)
    {
        var settings = context.TypeFieldSetDefinition.GetSettings<TitleFieldSetSettings>();

        if (!settings.RenderTitle || string.IsNullOrWhiteSpace(titleFieldSet.Title))
        {
            return null;
        }

        return Initialize<TitleFieldSetViewModel, TitleFieldSet>(GetDisplayShapeType(context), static (model, titleFieldSet) =>
        {
            model.Title = titleFieldSet.ContentItem.DisplayText;
            model.TitleFieldSet = titleFieldSet;
            model.ContentItem = titleFieldSet.ContentItem;
        }, titleFieldSet).Location(PlatformConstants.DisplayType.Detail, "Header")
        .Location(PlatformConstants.DisplayType.Summary, "Header");
    }
}
