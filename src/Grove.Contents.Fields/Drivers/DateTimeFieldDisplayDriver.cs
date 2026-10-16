using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Fields.ViewModels;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;
using Grove.Modules;

namespace Grove.Contents.Fields.Drivers;

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
            model.FieldSet = context.ContentFieldSet;
            model.FieldDefinition = context.FieldDefinition;
        }, field, context, _localClock)
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
