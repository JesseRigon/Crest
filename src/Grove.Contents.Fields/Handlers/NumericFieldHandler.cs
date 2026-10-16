using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Fields.Handlers;

public class NumericFieldHandler : ContentFieldHandler<NumericField>
{
    protected readonly IStringLocalizer S;

    public NumericFieldHandler(IStringLocalizer<NumericFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, NumericField field)
    {
        var settings = context.ContentFieldDefinition.GetSettings<NumericFieldSettings>();

        if (settings.Required && !field.Value.HasValue)
        {
            context.Fail(S["A value is required for {0}.", context.ContentFieldDefinition.DisplayName()], nameof(field.Value));
        }

        if (field.Value.HasValue)
        {
            if (settings.Minimum.HasValue && field.Value.Value < settings.Minimum.Value)
            {
                context.Fail(S["The value must be greater than {0}.", settings.Minimum.Value], nameof(field.Value));
            }

            if (settings.Maximum.HasValue && field.Value.Value > settings.Maximum.Value)
            {
                context.Fail(S["The value must be less than {0}.", settings.Maximum.Value], nameof(field.Value));
            }

            // Check the number of decimals.
            if (Math.Round(field.Value.Value, settings.Scale) != field.Value.Value)
            {
                if (settings.Scale == 0)
                {
                    context.Fail(S["The {0} field must be an integer.", context.ContentFieldDefinition.DisplayName()], nameof(field.Value));
                }
                else
                {
                    context.Fail(S["Invalid number of digits for {0}, max allowed: {1}.", context.ContentFieldDefinition.DisplayName(), settings.Scale], nameof(field.Value));
                }
            }
        }

        return Task.CompletedTask;
    }
}
