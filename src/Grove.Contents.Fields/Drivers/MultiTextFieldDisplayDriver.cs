using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Fields.ViewModels;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;

namespace Grove.Contents.Fields.Fields;

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
            var settings = context.FieldDefinition.GetSettings<MultiTextFieldSettings>();

            model.Values = settings.Options.Where(o => field.Values?.Contains(o.Value) == true).Select(o => o.Value).ToArray();
            model.Field = field;
            model.FieldSet = context.ContentFieldSet;
            model.FieldDefinition = context.FieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
