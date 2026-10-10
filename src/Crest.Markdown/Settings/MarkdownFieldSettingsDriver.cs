using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Markdown.Fields;
using Crest.Markdown.ViewModels;

namespace Crest.Markdown.Settings;

public sealed class MarkdownFieldSettingsDriver : ContentPartFieldDefinitionDisplayDriver<MarkdownField>
{
    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<MarkdownFieldSettingsViewModel>("MarkdownFieldSettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<MarkdownFieldSettings>();

            model.SanitizeHtml = settings.SanitizeHtml;
            model.RenderLiquid = settings.RenderLiquid;
            model.Hint = settings.Hint;
        }).Location("Content:20");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        var model = new MarkdownFieldSettingsViewModel();
        var settings = new MarkdownFieldSettings();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.SanitizeHtml = model.SanitizeHtml;
        settings.RenderLiquid = model.RenderLiquid;
        settings.Hint = model.Hint;

        context.Builder.WithSettings(settings);

        return Edit(partFieldDefinition, context);
    }
}
