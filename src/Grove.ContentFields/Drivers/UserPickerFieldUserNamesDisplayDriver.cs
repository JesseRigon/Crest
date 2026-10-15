using Grove.ContentFields.Fields;
using Grove.ContentFields.ViewModels;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.DisplayManagement.Views;

namespace Grove.ContentFields.Drivers;

public sealed class UserPickerFieldUserNamesDisplayDriver : ContentFieldDisplayDriver<UserPickerField>
{
    public override IDisplayResult Display(UserPickerField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayUserPickerFieldUserNamesViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
