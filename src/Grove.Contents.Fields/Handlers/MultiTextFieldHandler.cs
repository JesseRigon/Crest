using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Fields.Handlers;

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
