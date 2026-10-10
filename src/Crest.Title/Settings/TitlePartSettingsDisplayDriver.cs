using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Liquid;
using Crest.Mvc.ModelBinding;
using Crest.Title.Models;
using Crest.Title.ViewModels;

namespace Crest.Title.Settings;

public sealed class TitlePartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<TitlePart>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    public TitlePartSettingsDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<TitlePartSettingsDisplayDriver> localizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = localizer;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<TitlePartSettingsViewModel>("TitlePartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<TitlePartSettings>();

            model.Options = settings.Options;
            model.Pattern = settings.Pattern;
            model.RenderTitle = settings.RenderTitle;
            model.TitlePartSettings = settings;
            model.Placeholder = settings.Placeholder;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new TitlePartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            m => m.Pattern,
            m => m.Options,
            m => m.RenderTitle,
            m => m.Placeholder);

        if (model.Options == TitlePartOptions.GeneratedHidden || model.Options == TitlePartOptions.GeneratedDisabled)
        {
            if (string.IsNullOrWhiteSpace(model.Pattern))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Pattern), S["A pattern is required when using the selected behavior option."]);
            }
            else if (!_liquidTemplateManager.Validate(model.Pattern, out var errors))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Pattern), S["The pattern doesn't contain a valid Liquid expression. Details: {0}", string.Join(' ', errors)]);
            }
        }

        context.Builder.WithSettings(new TitlePartSettings
        {
            Pattern = model.Pattern,
            Options = model.Options,
            RenderTitle = model.RenderTitle,
            Placeholder = model.Placeholder,
        });

        return Edit(contentTypePartDefinition, context);
    }
}
