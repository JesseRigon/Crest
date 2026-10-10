using Crest.ContentFields.Fields;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.ContentFields.Settings;

public sealed class NumericFieldSettingsDriver : ContentPartFieldDefinitionDisplayDriver<NumericField>
{
    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<NumericFieldSettings>("NumericFieldSettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<NumericFieldSettings>();

            model.Hint = settings.Hint;
            model.Required = settings.Required;
            model.Scale = settings.Scale;
            model.Minimum = settings.Minimum;
            model.Maximum = settings.Maximum;
            model.Placeholder = settings.Placeholder;
            model.DefaultValue = settings.DefaultValue;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        var model = new NumericFieldSettings();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(model);

        return Edit(partFieldDefinition, context);
    }
}
