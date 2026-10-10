using Microsoft.Extensions.Localization;
using Crest.Alias.Models;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Liquid;

namespace Crest.Alias.Settings;

public sealed class AliasPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<AliasPart>
{
    private readonly ILiquidTemplateManager _templateManager;

    internal readonly IStringLocalizer S;

    public AliasPartSettingsDisplayDriver(
        ILiquidTemplateManager templateManager,
        IStringLocalizer<AliasPartSettingsDisplayDriver> localizer)
    {
        _templateManager = templateManager;
        S = localizer;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<AliasPartSettingsViewModel>("AliasPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<AliasPartSettings>();

            model.Pattern = settings.Pattern;
            model.Options = settings.Options;
            model.AliasPartSettings = settings;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new AliasPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            m => m.Pattern,
            m => m.Options);

        if (!string.IsNullOrEmpty(model.Pattern) && !_templateManager.Validate(model.Pattern, out var errors))
        {
            context.Updater.ModelState.AddModelError(nameof(model.Pattern), S["Pattern doesn't contain a valid Liquid expression. Details: {0}", string.Join(" ", errors)]);
        }
        else
        {
            context.Builder.WithSettings(new AliasPartSettings
            {
                Pattern = model.Pattern,
                Options = model.Options,
            });
        }

        return Edit(contentTypePartDefinition, context);
    }
}
