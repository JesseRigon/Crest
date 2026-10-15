using Microsoft.Extensions.Localization;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Settings;
using Grove.ContentFields.ViewModels;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Views;
using Grove.Modules;
using Grove.Mvc.ModelBinding;

namespace Grove.ContentFields.Drivers;

public sealed class DateTimeFieldDisplayDriver : ContentFieldDisplayDriver<DateTimeField>
{
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public DateTimeFieldDisplayDriver(
        ILocalClock localClock,
        IStringLocalizer<DateTimeFieldDisplayDriver> localizer)
    {
        _localClock = localClock;
        S = localizer;
    }

    public override IDisplayResult Display(DateTimeField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayDateTimeFieldViewModel, DateTimeField, BuildFieldDisplayContext, ILocalClock>(GetDisplayShapeType(context), static async (model, field, context, localClock) =>
        {
            model.LocalDateTime = field.Value == null ? null : (await localClock.ConvertToLocalAsync(field.Value.Value)).DateTime;
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        }, field, context, _localClock)
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
