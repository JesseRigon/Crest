using Crest.ContentFields.Fields;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.ContentFields.Settings;

public sealed class HtmlFieldSettingsDriver : ContentPartFieldDefinitionDisplayDriver<HtmlField>
{
    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<HtmlFieldSettingsViewModel>("HtmlFieldSettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<HtmlFieldSettings>();

            model.SanitizeHtml = settings.SanitizeHtml;
            model.RenderLiquid = settings.RenderLiquid;
            model.Hint = settings.Hint;
        }).Location("Content:20");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        var model = new HtmlFieldSettingsViewModel();
        var settings = new HtmlFieldSettings();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.SanitizeHtml = model.SanitizeHtml;
        settings.RenderLiquid = model.RenderLiquid;
        settings.Hint = model.Hint;

        context.Builder.WithSettings(settings);

        return Edit(partFieldDefinition, context);
    }
}
