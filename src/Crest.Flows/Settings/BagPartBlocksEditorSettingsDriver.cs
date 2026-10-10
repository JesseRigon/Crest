using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Flows.Models;
using Crest.Flows.ViewModels;

namespace Crest.Flows.Settings;

public sealed class BagPartBlocksEditorSettingsDriver : ContentTypePartDefinitionDisplayDriver<BagPart>
{
    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<BagPartBlocksEditorSettingsViewModel>("BagPartBlocksEditorSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<BagPartBlocksEditorSettings>();
            model.AddButtonText = settings.AddButtonText;
            model.ModalTitleText = settings.ModalTitleText;
        }).Location("Editor");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        if (contentTypePartDefinition.Editor() == "Blocks")
        {
            var model = new BagPartBlocksEditorSettingsViewModel();

            await context.Updater.TryUpdateModelAsync(model, Prefix,
                m => m.AddButtonText,
                m => m.ModalTitleText);

            context.Builder.WithSettings(new BagPartBlocksEditorSettings
            {
                AddButtonText = model.AddButtonText,
                ModalTitleText = model.ModalTitleText,
            });
        }

        return Edit(contentTypePartDefinition, context);
    }
}
