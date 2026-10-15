using Microsoft.AspNetCore.Identity;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Users.Models;

namespace Grove.Users.Drivers;

public sealed class TwoFactorMethodLoginSmsDisplayDriver : DisplayDriver<TwoFactorMethod>
{
    public override IDisplayResult Edit(TwoFactorMethod model, BuildEditorContext context)
    {
        return View("SmsAuthenticatorValidation", model)
            .Location("Content")
            .OnGroup(TokenOptions.DefaultPhoneProvider);
    }
}
