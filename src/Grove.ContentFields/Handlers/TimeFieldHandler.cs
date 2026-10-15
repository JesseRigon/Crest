using Microsoft.Extensions.Localization;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Settings;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.Handlers;

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
