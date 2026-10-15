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

    public override IDisplayResult Edit(TitlePart titlePart, BuildPartEditorContext context)
    {
        return Initialize<TitlePartViewModel>(GetEditorShapeType(context), model =>
        {
            model.Title = titlePart.ContentItem.DisplayText;
            model.TitlePart = titlePart;
            model.ContentItem = titlePart.ContentItem;
            model.Settings = context.TypePartDefinition.GetSettings<TitlePartSettings>();
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(TitlePart model, UpdatePartEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, t => t.Title);
        var settings = context.TypePartDefinition.GetSettings<TitlePartSettings>();

        if (settings.Options == TitlePartOptions.EditableRequired && string.IsNullOrWhiteSpace(model.Title))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Title), S["A value is required for Title."]);
        }
        else
        {
            model.ContentItem.DisplayText = model.Title;
        }

        return Edit(model, context);
    }
}
