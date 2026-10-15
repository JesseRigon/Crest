using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Fields.ViewModels;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;

namespace Grove.Contents.Fields.Drivers;

public sealed class YoutubeFieldDisplayDriver : ContentFieldDisplayDriver<YoutubeField>
{
    internal readonly IStringLocalizer S;

    public YoutubeFieldDisplayDriver(IStringLocalizer<YoutubeFieldDisplayDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Display(YoutubeField field, BuildFieldDisplayContext context)
    {
        return Initialize<YoutubeFieldDisplayViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
