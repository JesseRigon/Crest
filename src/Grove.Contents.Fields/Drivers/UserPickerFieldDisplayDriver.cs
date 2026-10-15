using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Fields.ViewModels;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;
using Grove.Access.Users.Indexes;
using Grove.Access.Users.Models;
using YesSql;
using YesSql.Services;

namespace Grove.Contents.Fields.Drivers;

public sealed class UserPickerFieldDisplayDriver : ContentFieldDisplayDriver<UserPickerField>
{
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    public UserPickerFieldDisplayDriver(
        ISession session,
        IStringLocalizer<UserPickerFieldDisplayDriver> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(UserPickerField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayUserPickerFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
