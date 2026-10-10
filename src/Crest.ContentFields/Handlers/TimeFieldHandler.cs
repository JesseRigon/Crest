using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.Settings;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.Handlers;

public class TimeFieldHandler : ContentFieldHandler<TimeField>
{
    protected readonly IStringLocalizer S;

    public TimeFieldHandler(IStringLocalizer<TimeFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, TimeField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<TimeFieldSettings>();

        if (settings.Required && !field.Value.HasValue)
        {
            context.Fail(S["A value is required for {0}.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.Value));
        }

        return Task.CompletedTask;
    }
}
