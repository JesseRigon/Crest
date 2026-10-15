using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Users.Models;
using Grove.Users.ViewModels;

namespace Grove.Users.Drivers;

public sealed class UserTwoFactorDisplayDriver : DisplayDriver<User>
{
    public override Task<IDisplayResult> DisplayAsync(User user, BuildDisplayContext context)
    {
        return CombineAsync(
            Initialize<SummaryAdminUserViewModel>("UserTwoFactorActionsMenu", model => model.User = user)
            .Location(PlatformConstants.DisplayType.SummaryAdmin, "ActionsMenu:10")
        );
    }
}
