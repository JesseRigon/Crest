using Microsoft.Extensions.Localization;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Settings;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.Handlers;

public class UserPickerFieldHandler : ContentFieldHandler<UserPickerField>
{
    protected readonly IStringLocalizer S;

    public UserPickerFieldHandler(IStringLocalizer<UserPickerFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, UserPickerField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<UserPickerFieldSettings>();

        if (settings.Required && field.UserIds.Length == 0)
        {
            context.Fail(S["The {0} field is required.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.UserIds));
        }

        if (!settings.Multiple && field.UserIds.Length > 1)
        {
            context.Fail(S["The {0} field cannot contain multiple items.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.UserIds));
        }

        return Task.CompletedTask;
    }
}
