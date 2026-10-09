using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.Settings;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.Handlers;

public class DateFieldHandler : ContentFieldHandler<DateField>
{
    protected readonly IStringLocalizer S;

    public DateFieldHandler(IStringLocalizer<DateFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, DateField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<DateFieldSettings>();

        if (settings.Required && !field.Value.HasValue)
        {
            context.Fail(S["A value is required for {0}.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.Value));
        }

        return Task.CompletedTask;
    }
}
