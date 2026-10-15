using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Users.Models;

namespace Grove.Users.Drivers;

public sealed class RegisterUserLoginFormDisplayDriver : DisplayDriver<LoginForm>
{
    public override IDisplayResult Edit(LoginForm model, BuildEditorContext context)
    {
        return View("LoginFormRegisterUser", model).Location("Links:10");
    }
}
