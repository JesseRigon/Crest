using Microsoft.AspNetCore.Identity;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Users.Models;

namespace Crest.Users.Drivers;

public sealed class TwoFactorMethodLoginAuthenticationAppDisplayDriver : DisplayDriver<TwoFactorMethod>
{
    public override IDisplayResult Edit(TwoFactorMethod model, BuildEditorContext context)
    {
        return View("AuthenticatorAppValidation", model)
            .Location("Content")
            .OnGroup(TokenOptions.DefaultAuthenticatorProvider);
    }
}
