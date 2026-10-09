using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentPreview.Models;
using Crest.ContentPreview.ViewModels;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Liquid;

namespace Crest.ContentPreview.Settings;

public sealed class PreviewPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<PreviewPart>
{
    private readonly ILiquidTemplateManager _templateManager;

    internal readonly IStringLocalizer S;

    public PreviewPartSettingsDisplayDriver(
        ILiquidTemplateManager templateManager,
        IStringLocalizer<PreviewPartSettingsDisplayDriver> localizer)
    {
        _templateManager = templateManager;
        S = localizer;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<PreviewPartSettingsViewModel>("PreviewPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<PreviewPartSettings>();

            model.Pattern = settings.Pattern;
            model.PreviewPartSettings = settings;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new PreviewPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            m => m.Pattern);

        if (!string.IsNullOrEmpty(model.Pattern) && !_templateManager.Validate(model.Pattern, out var errors))
        {
            context.Updater.ModelState.AddModelError(nameof(model.Pattern), S["Pattern doesn't contain a valid Liquid expression. Details: {0}", string.Join(" ", errors)]);
        }
        else
        {
            context.Builder.WithSettings(new PreviewPartSettings
            {
                Pattern = model.Pattern,
            });
        }

        return Edit(contentTypePartDefinition, context);
    }
}
