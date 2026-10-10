using Crest.ContentFields.Fields;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.ContentFields.Settings;

public sealed class BooleanFieldSettingsDriver : ContentPartFieldDefinitionDisplayDriver<BooleanField>
{
    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<BooleanFieldSettings>("BooleanFieldSettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<BooleanFieldSettings>();

            model.Hint = settings.Hint;
            model.Label = settings.Label;
            model.DefaultValue = settings.DefaultValue;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        var model = new BooleanFieldSettings();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(model);

        return Edit(partFieldDefinition, context);
    }
}
