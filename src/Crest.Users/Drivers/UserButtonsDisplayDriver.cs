using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Users.Models;

namespace Crest.Users.Drivers;

public sealed class UserButtonsDisplayDriver : DisplayDriver<User>
{
    public override IDisplayResult Edit(User model, BuildEditorContext context)
    {
        return Dynamic("UserSaveButtons_Edit").Location("Actions");
    }
}
