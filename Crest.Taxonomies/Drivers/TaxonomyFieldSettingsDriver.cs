using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Taxonomies.Fields;
using Crest.Taxonomies.Settings;

namespace Crest.Taxonomies.Drivers;

public sealed class TaxonomyFieldSettingsDriver : ContentPartFieldDefinitionDisplayDriver<TaxonomyField>
{
    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<TaxonomyFieldSettings>("TaxonomyFieldSettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<TaxonomyFieldSettings>();

            model.Hint = settings.Hint;
            model.Required = settings.Required;
            model.TaxonomyContentItemId = settings.TaxonomyContentItemId;
            model.Unique = settings.Unique;
            model.LeavesOnly = settings.LeavesOnly;
            model.Open = settings.Open;
            model.Placeholder = settings.Placeholder;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        var model = new TaxonomyFieldSettings();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(model);

        return await EditAsync(partFieldDefinition, context);
    }
}
