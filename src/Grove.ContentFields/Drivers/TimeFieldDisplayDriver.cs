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

public sealed class TimeFieldDisplayDriver : ContentFieldDisplayDriver<TimeField>
{
    internal readonly IStringLocalizer S;

    public TimeFieldDisplayDriver(IStringLocalizer<TimeFieldDisplayDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Display(TimeField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayTimeFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
