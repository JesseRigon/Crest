using Microsoft.Extensions.Localization;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Settings;
using Grove.ContentFields.ViewModels;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Views;
using Grove.Mvc.ModelBinding;
using Grove.Users.Indexes;
using Grove.Users.Models;
using YesSql;
using YesSql.Services;

namespace Grove.ContentFields.Drivers;

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
