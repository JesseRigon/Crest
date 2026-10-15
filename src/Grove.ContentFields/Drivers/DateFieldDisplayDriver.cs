using Microsoft.Extensions.Localization;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Settings;
using Grove.ContentFields.ViewModels;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Views;
using Grove.Mvc.ModelBinding;

namespace Grove.ContentFields.Drivers;

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
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
