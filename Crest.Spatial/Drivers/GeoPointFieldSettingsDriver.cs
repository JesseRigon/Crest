using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Spatial.Fields;
using Crest.Spatial.Settings;

namespace Crest.Spatial.Drivers;

public sealed class GeoPointFieldSettingsDriver : ContentPartFieldDefinitionDisplayDriver<GeoPointField>
{
    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<GeoPointFieldSettings>("GeoPointFieldSettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<GeoPointFieldSettings>();

            model.Hint = settings.Hint;
            model.Required = settings.Required;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        var model = new GeoPointFieldSettings();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(model);

        return Edit(partFieldDefinition, context);
    }
}
