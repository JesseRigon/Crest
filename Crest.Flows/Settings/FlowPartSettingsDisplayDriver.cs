using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Flows.Models;
using Crest.Flows.ViewModels;

namespace Crest.Flows.Settings;

public sealed class FlowPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<FlowPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public FlowPartSettingsDisplayDriver(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<FlowPartSettingsViewModel>("FlowPartSettings_Edit", async model =>
        {
            model.FlowPartSettings = contentTypePartDefinition.GetSettings<FlowPartSettings>();
            model.ContainedContentTypes = model.FlowPartSettings.ContainedContentTypes;
            model.CollapseContainedItems = model.FlowPartSettings.CollapseContainedItems;
            model.DefaultAlignment = model.FlowPartSettings.DefaultAlignment;
            model.ContentTypes = [];

            foreach (var contentTypeDefinition in (await _contentDefinitionManager.ListTypeDefinitionsAsync()).Where(t => t.GetStereotype() == "Widget"))
            {
                model.ContentTypes.Add(contentTypeDefinition.Name, contentTypeDefinition.DisplayName);
            }
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new FlowPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            m => m.ContainedContentTypes,
            m => m.CollapseContainedItems,
            m => m.DefaultAlignment);

        context.Builder.WithSettings(new FlowPartSettings
        {
            ContainedContentTypes = model.ContainedContentTypes,
            CollapseContainedItems = model.CollapseContainedItems,
            DefaultAlignment = model.DefaultAlignment,
        });

        return Edit(contentTypePartDefinition, context);
    }
}
