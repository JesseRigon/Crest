using Grove.ReCaptcha.Services;
using Grove.Users.Events;

namespace Grove.ReCaptcha.Users.Handlers;

public sealed class PasswordRecoveryFormEventEventHandler : PasswordRecoveryFormEvents
{
    private readonly ReCaptchaService _reCaptchaService;

    public PasswordRecoveryFormEventEventHandler(ReCaptchaService reCaptchaService)
    {
        _reCaptchaService = reCaptchaService;
    }

    public override Task RecoveringPasswordAsync(Action<string, string> reportError)
        => _reCaptchaService.ValidateCaptchaAsync(reportError);

    public override Task ResettingPasswordAsync(Action<string, string> reportError)
        => _reCaptchaService.ValidateCaptchaAsync(reportError);
}
