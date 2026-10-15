using Microsoft.AspNetCore.Identity;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Users.Models;

namespace Grove.Users.Drivers;

public sealed class ExternalAuthenticationUserMenuDisplayDriver : DisplayDriver<UserMenu>
{
    private readonly SignInManager<IUser> _signInManager;

    public ExternalAuthenticationUserMenuDisplayDriver(SignInManager<IUser> signInManager)
    {
        _signInManager = signInManager;
    }

    public override IDisplayResult Display(UserMenu model, BuildDisplayContext context)
    {
        return View("UserMenuItems__ExternalLogins", model)
            .RenderWhen(static async (signInManager) => (await signInManager.GetExternalAuthenticationSchemesAsync()).Any(), _signInManager)
            .Location(PlatformConstants.DisplayType.Detail, "Content:10")
            .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:10")
            .Differentiator("ExternalLogins");
    }
}
