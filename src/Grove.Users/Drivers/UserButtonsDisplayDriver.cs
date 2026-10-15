using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Users.Models;

namespace Grove.Users.Drivers;

public sealed class UserButtonsDisplayDriver : DisplayDriver<User>
{
    public override IDisplayResult Edit(User model, BuildEditorContext context)
    {
        return Dynamic("UserSaveButtons_Edit").Location("Actions");
    }
}
