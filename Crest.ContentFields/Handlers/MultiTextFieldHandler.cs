using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.Settings;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.Handlers;

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
