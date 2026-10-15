using System.Globalization;
using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Fields.ViewModels;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;

namespace Grove.Contents.Fields.Drivers;

public sealed class NumericFieldDisplayDriver : ContentFieldDisplayDriver<NumericField>
{
    internal readonly IStringLocalizer S;

    public NumericFieldDisplayDriver(IStringLocalizer<NumericFieldDisplayDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Display(NumericField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayNumericFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
