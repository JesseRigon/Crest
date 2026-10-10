using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.Settings;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.Handlers;

public class LocalizationSetContentPickerFieldHandler : ContentFieldHandler<LocalizationSetContentPickerField>
{
    protected readonly IStringLocalizer S;

    public LocalizationSetContentPickerFieldHandler(IStringLocalizer<LocalizationSetContentPickerFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, LocalizationSetContentPickerField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<LocalizationSetContentPickerFieldSettings>();

        if (settings.Required && field.LocalizationSets.Length == 0)
        {
            context.Fail(S["The {0} field is required.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.LocalizationSets));
        }

        if (!settings.Multiple && field.LocalizationSets.Length > 1)
        {
            context.Fail(S["The {0} field cannot contain multiple items.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.LocalizationSets));
        }

        return Task.CompletedTask;
    }
}
