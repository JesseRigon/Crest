using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;
using Grove.Fields;
using Grove.Infrastructure.Settings;
using Microsoft.Extensions.Localization;

namespace Grove.Contents.OptionPicker.Handlers;

/// <summary>An option picker field's own rules on save: a required field holds a value, a single
/// one holds at most one.</summary>
public sealed class OptionPickerFieldHandler(IStringLocalizer<OptionPickerFieldHandler> S) : ContentFieldHandler<OptionPickerField>
{
    public override Task ValidatingAsync(ValidateContentFieldContext context, OptionPickerField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<OptionPickerFieldSettings>();

        if (settings.Required && field.SelectedIds.Length == 0)
        {
            context.Fail(S["The {0} field is required.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.SelectedIds));
        }

        if (!settings.Multiple && field.SelectedIds.Length > 1)
        {
            context.Fail(S["The {0} field cannot contain multiple items.", context.ContentPartFieldDefinition.DisplayName()], nameof(field.SelectedIds));
        }

        return Task.CompletedTask;
    }
}
