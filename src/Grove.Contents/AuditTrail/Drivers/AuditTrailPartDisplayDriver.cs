using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.Contents.AuditTrail.Models;
using Grove.Contents.AuditTrail.Settings;
using Grove.Contents.AuditTrail.ViewModels;
using Grove.DisplayManagement.Views;

namespace Grove.Contents.AuditTrail.Drivers;

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
