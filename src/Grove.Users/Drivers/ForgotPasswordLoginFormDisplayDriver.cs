using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Settings;
using Grove.Users.Models;

namespace Grove.Users.Drivers;

public sealed class ForgotPasswordLoginFormDisplayDriver : DisplayDriver<LoginForm>
{
    private readonly ISiteService _siteService;

    public ForgotPasswordLoginFormDisplayDriver(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public override async Task<IDisplayResult> EditAsync(LoginForm model, BuildEditorContext context)
    {
        var settings = await _siteService.GetSettingsAsync<ResetPasswordSettings>();

        if (!settings.AllowResetPassword)
        {
            return null;
        }

        return View("LoginFormForgotPassword", model).Location("Links:5");
    }
}
