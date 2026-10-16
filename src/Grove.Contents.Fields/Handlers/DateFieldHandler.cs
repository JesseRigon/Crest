using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Fields.Handlers;

public class DateFieldHandler : ContentFieldHandler<DateField>
{
    protected readonly IStringLocalizer S;

    public DateFieldHandler(IStringLocalizer<DateFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, DateField field)
    {
        var settings = context.ContentFieldDefinition.GetSettings<DateFieldSettings>();

        if (settings.Required && !field.Value.HasValue)
        {
            context.Fail(S["A value is required for {0}.", context.ContentFieldDefinition.DisplayName()], nameof(field.Value));
        }

        return Task.CompletedTask;
    }
}
