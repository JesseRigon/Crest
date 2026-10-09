using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Users.Models;

namespace Crest.Users.Drivers;

public sealed class RegisterUserLoginFormDisplayDriver : DisplayDriver<LoginForm>
{
    public override IDisplayResult Edit(LoginForm model, BuildEditorContext context)
    {
        return View("LoginFormRegisterUser", model).Location("Links:10");
    }
}
