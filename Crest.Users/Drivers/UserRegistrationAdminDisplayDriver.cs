using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Users.Models;
using Crest.Users.ViewModels;

namespace Crest.Users.Drivers;

public sealed class UserRegistrationAdminDisplayDriver : DisplayDriver<User>
{
    public override Task<IDisplayResult> DisplayAsync(User user, BuildDisplayContext context)
    {
        return CombineAsync(
            Initialize<SummaryAdminUserViewModel>("UserSendConfirmationActionsMenu", model => model.User = user)
            .Location(PlatformConstants.DisplayType.SummaryAdmin, "ActionsMenu:15")
        );
    }
}
