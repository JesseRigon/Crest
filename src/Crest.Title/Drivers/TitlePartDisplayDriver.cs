using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Mvc.ModelBinding;
using Crest.Title.Models;
using Crest.Title.ViewModels;

namespace Crest.Title.Drivers;

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
