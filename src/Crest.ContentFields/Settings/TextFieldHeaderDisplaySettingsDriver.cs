using Crest.ContentFields.Fields;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.ContentFields.Settings;

public sealed class TextFieldHeaderDisplaySettingsDriver : ContentPartFieldDefinitionDisplayDriver<TextField>
{
    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<HeaderSettingsViewModel>("TextFieldHeaderDisplaySettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<TextFieldHeaderDisplaySettings>();

            model.Level = settings.Level;
        }).Location("DisplayMode");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        if (partFieldDefinition.DisplayMode() == "Header")
        {
            var model = new HeaderSettingsViewModel();
            var settings = new TextFieldHeaderDisplaySettings();

            await context.Updater.TryUpdateModelAsync(model, Prefix);

            settings.Level = model.Level;

            context.Builder.WithSettings(settings);
        }

        return Edit(partFieldDefinition, context);
    }
}
