using System.Globalization;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.ReCaptcha.Configuration;
using Grove.Settings;
using Grove.Users.Models;

namespace Grove.ReCaptcha.Drivers;

public sealed class RegisterUserFormDisplayDriver : DisplayDriver<RegisterUserForm>
{
    private readonly IOptionsMonitor<ReCaptchaSettings> _settings;

    public RegisterUserFormDisplayDriver(IOptionsMonitor<ReCaptchaSettings> options)
    {
        _settings = options;
    }

    public override async Task<IDisplayResult> EditAsync(RegisterUserForm model, BuildEditorContext context)
    {
        if (!_settings.CurrentValue.ConfigurationExists())
        {
            return null;
        }

        return Dynamic("ReCaptcha", (m) =>
        {
            m.language = CultureInfo.CurrentUICulture.Name;
        }).Location("Content:after");
    }
}
