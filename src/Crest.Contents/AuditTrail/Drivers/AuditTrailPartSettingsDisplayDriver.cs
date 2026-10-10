using Crest.ContentManagement.Metadata.Models;
using Crest.Contents.AuditTrail.Models;
using Crest.Contents.AuditTrail.Settings;
using Crest.Contents.AuditTrail.ViewModels;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.Contents.AuditTrail.Drivers;

public sealed class AuditTrailPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver
{
    public override IDisplayResult Edit(ContentTypePartDefinition model, BuildEditorContext context)
    {
        if (!string.Equals(nameof(AuditTrailPart), model.PartDefinition.Name, StringComparison.Ordinal))
        {
            return null;
        }

        return Initialize<AuditTrailPartSettingsViewModel>("AuditTrailPartSettings_Edit", viewModel =>
        {
            var settings = model.GetSettings<AuditTrailPartSettings>();
            viewModel.ShowCommentInput = settings.ShowCommentInput;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition model, UpdateTypePartEditorContext context)
    {
        if (!string.Equals(nameof(AuditTrailPart), model.PartDefinition.Name, StringComparison.Ordinal))
        {
            return null;
        }

        var viewModel = new AuditTrailPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix, m => m.ShowCommentInput);

        context.Builder.WithSettings(new AuditTrailPartSettings
        {
            ShowCommentInput = viewModel.ShowCommentInput,
        });

        return Edit(model, context);
    }
}
