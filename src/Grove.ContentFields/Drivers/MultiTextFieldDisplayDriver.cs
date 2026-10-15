using Microsoft.Extensions.Localization;
using Grove.ContentFields.Settings;
using Grove.ContentFields.ViewModels;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Views;
using Grove.Mvc.ModelBinding;

namespace Grove.ContentFields.Fields;

public sealed class MultiTextFieldDisplayDriver : ContentFieldDisplayDriver<MultiTextField>
{
    internal readonly IStringLocalizer S;

    public MultiTextFieldDisplayDriver(IStringLocalizer<MultiTextFieldDisplayDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Display(MultiTextField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayMultiTextFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            var settings = context.PartFieldDefinition.GetSettings<MultiTextFieldSettings>();

            model.Values = settings.Options.Where(o => field.Values?.Contains(o.Value) == true).Select(o => o.Value).ToArray();
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    public override IDisplayResult Edit(MultiTextField field, BuildFieldEditorContext context)
    {
        return Initialize<EditMultiTextFieldViewModel>(GetEditorShapeType(context), model =>
        {
            if (context.IsNew)
            {
                var settings = context.PartFieldDefinition.GetSettings<MultiTextFieldSettings>();
                model.Values = settings.Options.Where(o => o.Default).Select(o => o.Value).ToArray();
            }
            else
            {
                model.Values = field.Values;
            }
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(MultiTextField field, UpdateFieldEditorContext context)
    {
        var viewModel = new EditMultiTextFieldViewModel();
        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        field.Values = viewModel.Values;

        var settings = context.PartFieldDefinition.GetSettings<MultiTextFieldSettings>();
        if (settings.Required && viewModel.Values.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(field.Values), S["A value is required for {0}.", context.PartFieldDefinition.DisplayName()]);
        }

        return Edit(field, context);
    }
}
