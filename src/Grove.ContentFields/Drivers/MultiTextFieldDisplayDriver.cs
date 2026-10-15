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
}
