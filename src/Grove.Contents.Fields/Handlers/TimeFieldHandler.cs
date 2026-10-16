using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Fields.Handlers;

public class TimeFieldHandler : ContentFieldHandler<TimeField>
{
    protected readonly IStringLocalizer S;

    public TimeFieldHandler(IStringLocalizer<TimeFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, TimeField field)
    {
        var settings = context.ContentFieldDefinition.GetSettings<TimeFieldSettings>();

        if (settings.Required && !field.Value.HasValue)
        {
            context.Fail(S["A value is required for {0}.", context.ContentFieldDefinition.DisplayName()], nameof(field.Value));
        }

        return Task.CompletedTask;
    }
}
