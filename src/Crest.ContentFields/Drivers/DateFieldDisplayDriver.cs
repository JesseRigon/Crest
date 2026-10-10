using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.Settings;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Views;
using Crest.Mvc.ModelBinding;

namespace Crest.ContentFields.Drivers;

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

    public override IDisplayResult Edit(DateField field, BuildFieldEditorContext context)
    {
        return Initialize<EditDateFieldViewModel>(GetEditorShapeType(context), model =>
        {
            model.Value = field.Value;
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(DateField field, UpdateFieldEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(field, Prefix, f => f.Value);
        var settings = context.PartFieldDefinition.GetSettings<DateFieldSettings>();

        if (settings.Required && field.Value == null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(field.Value), S["A value is required for {0}.", context.PartFieldDefinition.DisplayName()]);
        }

        return Edit(field, context);
    }
}
