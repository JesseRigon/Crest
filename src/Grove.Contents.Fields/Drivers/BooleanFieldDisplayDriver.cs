using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Fields.ViewModels;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Display.Management.Views;

namespace Grove.Contents.Fields.Drivers;

public sealed class BooleanFieldDisplayDriver : ContentFieldDisplayDriver<BooleanField>
{
    public override IDisplayResult Display(BooleanField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayBooleanFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.FieldSet = context.ContentFieldSet;
            model.FieldDefinition = context.FieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
