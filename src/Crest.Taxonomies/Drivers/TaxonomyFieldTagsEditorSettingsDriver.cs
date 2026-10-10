using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Taxonomies.Fields;
using Crest.Taxonomies.Settings;

namespace Crest.Taxonomies.Drivers;

public sealed class TaxonomyFieldTagsEditorSettingsDriver : ContentPartFieldDefinitionDisplayDriver<TaxonomyField>
{
    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<TaxonomyFieldTagsEditorSettings>("TaxonomyFieldTagsEditorSettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<TaxonomyFieldTagsEditorSettings>();

            model.Open = settings.Open;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        if (partFieldDefinition.Editor() == "Tags")
        {
            var model = new TaxonomyFieldTagsEditorSettings();

            await context.Updater.TryUpdateModelAsync(model, Prefix);

            context.Builder.WithSettings(model);
        }

        return Edit(partFieldDefinition, context);
    }
}
