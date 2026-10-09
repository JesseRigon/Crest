using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.Settings;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Views;
using Crest.Modules;
using Crest.Mvc.ModelBinding;


namespace Crest.ContentFields.Drivers;

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

    public override IDisplayResult Edit(DateTimeField field, BuildFieldEditorContext context)
    {
        return Initialize<EditDateTimeFieldViewModel>(GetEditorShapeType(context), async model =>
        {
            model.LocalDateTime = field.Value == null ? null : (await _localClock.ConvertToLocalAsync(field.Value.Value)).DateTime;
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(DateTimeField field, UpdateFieldEditorContext context)
    {
        var model = new EditDateTimeFieldViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, f => f.LocalDateTime);
        var settings = context.PartFieldDefinition.GetSettings<DateTimeFieldSettings>();

        if (settings.Required && model.LocalDateTime == null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.LocalDateTime), S["A value is required for {0}.", context.PartFieldDefinition.DisplayName()]);
        }
        else
        {
            if (model.LocalDateTime == null)
            {
                field.Value = null;
            }
            else
            {
                field.Value = await _localClock.ConvertToUtcAsync(model.LocalDateTime.Value);
            }
        }

        return Edit(field, context);
    }
}
