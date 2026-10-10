using Crest.ContentFields.Fields;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;

namespace Crest.ContentFields.Drivers;

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
