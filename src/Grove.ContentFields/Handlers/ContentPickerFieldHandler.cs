using Microsoft.Extensions.Localization;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Settings;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.Handlers;

public class ContentPickerFieldHandler : ContentFieldHandler<ContentPickerField>
{
    protected readonly IStringLocalizer S;

    public ContentPickerFieldHandler(IStringLocalizer<ContentPickerFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, ContentPickerField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<ContentPickerFieldSettings>();

        if (settings.Required && field.ContentItemIds.Length == 0)
        {
            context.Fail(S["The {0} field is required.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.ContentItemIds));
        }

        if (!settings.Multiple && field.ContentItemIds.Length > 1)
        {
            context.Fail(S["The {0} field cannot contain multiple items.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.ContentItemIds));
        }

        return Task.CompletedTask;
    }
}

