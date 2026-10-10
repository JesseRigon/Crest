using Microsoft.AspNetCore.Identity;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Users.Models;

namespace Crest.Users.Drivers;

public sealed class TwoFactorMethodLoginEmailDisplayDriver : DisplayDriver<TwoFactorMethod>
{
    public override IDisplayResult Edit(TwoFactorMethod model, BuildEditorContext context)
    {
        return View("EmailAuthenticatorValidation", model)
            .Location("Content")
            .OnGroup(TokenOptions.DefaultEmailProvider);
    }
}
