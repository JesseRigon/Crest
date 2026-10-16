using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Fields.ViewModels;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;

namespace Grove.Contents.Fields.Drivers;

public sealed class DateFieldDisplayDriver : ContentFieldDisplayDriver<DateField>
{
    internal readonly IStringLocalizer S;

    public DateFieldDisplayDriver(IStringLocalizer<DateFieldDisplayDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Display(DateField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayDateFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.FieldSet = context.ContentFieldSet;
            model.FieldDefinition = context.FieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
