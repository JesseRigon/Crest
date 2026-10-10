using System.Globalization;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.ReCaptcha.Configuration;
using Crest.Settings;
using Crest.Users.Models;

namespace Crest.ReCaptcha.Drivers;

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
