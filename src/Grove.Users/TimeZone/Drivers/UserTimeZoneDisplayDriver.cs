using Grove.DisplayManagement.Entities;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Users.Models;
using Grove.Users.TimeZone.Models;
using Grove.Users.TimeZone.ViewModels;

namespace Grove.Users.TimeZone.Drivers;

public sealed class UserTimeZoneDisplayDriver : SectionDisplayDriver<User, UserTimeZone>
{
    public override IDisplayResult Edit(User user, UserTimeZone userTimeZone, BuildEditorContext context)
    {
        return Initialize<UserTimeZoneViewModel>("UserTimeZone_Edit", model =>
        {
            model.TimeZoneId = userTimeZone.TimeZoneId;
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(User user, UserTimeZone userTimeZone, UpdateEditorContext context)
    {
        var model = new UserTimeZoneViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        userTimeZone.TimeZoneId = model.TimeZoneId;

        return await EditAsync(user, userTimeZone, context);
    }
}
