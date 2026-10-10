using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.Settings;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.Handlers;

public class DateTimeFieldHandler : ContentFieldHandler<DateTimeField>
{
    protected readonly IStringLocalizer S;

    public DateTimeFieldHandler(IStringLocalizer<DateTimeFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, DateTimeField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<DateTimeFieldSettings>();

        if (settings.Required && !field.Value.HasValue)
        {
            context.Fail(S["A value is required for {0}.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.Value));
        }

        return Task.CompletedTask;
    }
}
