using Crest.ContentFields.Fields;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Modules;

namespace Crest.ContentFields.Settings;

[RequireFeatures("Crest.ContentLocalization")]
public sealed class LocalizationSetContentPickerFieldSettingsDriver : ContentPartFieldDefinitionDisplayDriver<LocalizationSetContentPickerField>
{
    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<LocalizationSetContentPickerFieldSettings>("LocalizationSetContentPickerFieldSettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<LocalizationSetContentPickerFieldSettings>();

            model.Hint = settings.Hint;
            model.Required = settings.Required;
            model.Multiple = settings.Multiple;
            model.DisplayedContentTypes = settings.DisplayedContentTypes;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        var model = new LocalizationSetContentPickerFieldSettings();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(model);

        return Edit(partFieldDefinition, context);
    }
}
