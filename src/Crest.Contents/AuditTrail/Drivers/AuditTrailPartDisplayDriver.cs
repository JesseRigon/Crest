using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.Contents.AuditTrail.Models;
using Crest.Contents.AuditTrail.Settings;
using Crest.Contents.AuditTrail.ViewModels;
using Crest.DisplayManagement.Views;

namespace Crest.Contents.AuditTrail.Drivers;

public sealed class AuditTrailPartDisplayDriver : ContentPartDisplayDriver<AuditTrailPart>
{
    public override IDisplayResult Edit(AuditTrailPart part, BuildPartEditorContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<AuditTrailPartSettings>();
        if (settings.ShowCommentInput)
        {
            return Initialize<AuditTrailPartViewModel>(GetEditorShapeType(context), model =>
            {
                if (part.ShowComment)
                {
                    model.Comment = part.Comment;
                }
            });
        }

        return null;
    }

    public override async Task<IDisplayResult> UpdateAsync(AuditTrailPart part, UpdatePartEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(part, Prefix);

        return Edit(part, context);
    }
}
