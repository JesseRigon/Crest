using Grove.Security.ReCaptcha.Services;
using Grove.Access.Users.Events;

namespace Grove.Security.ReCaptcha.Users.Handlers;

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
