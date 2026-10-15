using Microsoft.AspNetCore.Identity;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Users.Models;

namespace Grove.Users.Drivers;

public sealed class TwoFactorMethodLoginAuthenticationAppDisplayDriver : DisplayDriver<TwoFactorMethod>
{
    public override IDisplayResult Edit(TwoFactorMethod model, BuildEditorContext context)
    {
        return View("AuthenticatorAppValidation", model)
            .Location("Content")
            .OnGroup(TokenOptions.DefaultAuthenticatorProvider);
    }
}
