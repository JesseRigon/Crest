using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.Settings;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Views;
using Crest.Mvc.ModelBinding;

namespace Crest.ContentFields.Drivers;

public sealed class TextFieldDisplayDriver : ContentFieldDisplayDriver<TextField>
{
    internal readonly IStringLocalizer S;

    public TextFieldDisplayDriver(IStringLocalizer<TextFieldDisplayDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Display(TextField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayTextFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    public override IDisplayResult Edit(TextField field, BuildFieldEditorContext context)
    {
        return Initialize<EditTextFieldViewModel>(GetEditorShapeType(context), model =>
        {
            var settings = context.PartFieldDefinition.GetSettings<TextFieldSettings>();
            model.Text = context.IsNew && field.Text == null ? settings.DefaultValue : field.Text;
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(TextField field, UpdateFieldEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(field, Prefix, f => f.Text);
        var settings = context.PartFieldDefinition.GetSettings<TextFieldSettings>();

        if (settings.Required && string.IsNullOrWhiteSpace(field.Text))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(field.Text), S["A value is required for {0}.", context.PartFieldDefinition.DisplayName()]);
        }

        var length = field.Text?.Length ?? 0;

        if (settings.MinLength.HasValue && length > 0 && length < settings.MinLength)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(field.Text), S["{0} must be at least {1} characters long.", context.PartFieldDefinition.DisplayName(), settings.MinLength]);
        }

        if (settings.MaxLength.HasValue && length > settings.MaxLength)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(field.Text), S["{0} can't be longer than {1} characters.", context.PartFieldDefinition.DisplayName(), settings.MaxLength]);
        }

        return Edit(field, context);
    }
}
