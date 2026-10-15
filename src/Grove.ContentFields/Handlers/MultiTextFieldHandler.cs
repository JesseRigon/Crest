using Microsoft.Extensions.Localization;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Settings;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.Handlers;

public class MultiTextFieldHandler : ContentFieldHandler<MultiTextField>
{
    protected readonly IStringLocalizer S;

    public MultiTextFieldHandler(IStringLocalizer<MultiTextFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, MultiTextField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<MultiTextFieldSettings>();

        if (settings.Required && field.Values.Length == 0)
        {
            context.Fail(S["A value is required for {0}.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.Values));
        }

        return Task.CompletedTask;
    }
}
