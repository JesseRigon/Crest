using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.Settings;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.Handlers;

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

