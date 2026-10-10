using Crest.ContentFields.Fields;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.ContentFields.Settings;

public sealed class DateFieldSettingsDriver : ContentPartFieldDefinitionDisplayDriver<DateField>
{
    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<DateFieldSettings>("DateFieldSettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<DateFieldSettings>();

            model.Hint = settings.Hint;
            model.Required = settings.Required;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        var model = new DateFieldSettings();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(model);

        return Edit(partFieldDefinition, context);
    }
}
