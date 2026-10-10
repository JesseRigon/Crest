using Microsoft.Extensions.Options;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.ReCaptcha.Configuration;
using Crest.Settings;

namespace Crest.ReCaptcha.Forms;

public sealed class ReCaptchaPartDisplayDriver : ContentPartDisplayDriver<ReCaptchaPart>
{
    private readonly IOptionsMonitor<ReCaptchaSettings> _settings;

    public ReCaptchaPartDisplayDriver(IOptionsMonitor<ReCaptchaSettings> options)
    {
        _settings = options;
    }

    public override IDisplayResult Display(ReCaptchaPart part, BuildPartDisplayContext context)
    {
        return Initialize<ReCaptchaPartViewModel>("ReCaptchaPart", async model =>
        {
            model.SettingsAreConfigured = _settings.CurrentValue.ConfigurationExists();
        }).Location(PlatformConstants.DisplayType.Detail, "Content");
    }

    public override IDisplayResult Edit(ReCaptchaPart part, BuildPartEditorContext context)
    {
        return Initialize<ReCaptchaPartViewModel>("ReCaptchaPart_Fields_Edit", async model =>
        {
            model.SettingsAreConfigured = _settings.CurrentValue.ConfigurationExists();
        });
    }
}
