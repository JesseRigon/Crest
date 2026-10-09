using Microsoft.AspNetCore.Identity;
using Crest.ReCaptcha.Services;
using Crest.Users;
using Crest.Users.Events;

namespace Crest.ReCaptcha.Users.Handlers;

public sealed class RegistrationFormEventHandler : RegistrationFormEventsBase
{
    private readonly ReCaptchaService _reCaptchaService;
    private readonly SignInManager<IUser> _signInManager;

    public RegistrationFormEventHandler(
        ReCaptchaService reCaptchaService,
        SignInManager<IUser> signInManager)
    {
        _reCaptchaService = reCaptchaService;
        _signInManager = signInManager;
    }

    public override async Task RegistrationValidationAsync(Action<string, string> reportError)
    {
        // When logging in via an external provider, authentication security is already handled by the provider.
        // Therefore, using a CAPTCHA is unnecessary and impractical, as users wouldn't be able to complete it anyway.
        if (await _signInManager.GetExternalLoginInfoAsync() != null)
        {
            return;
        }

        await _reCaptchaService.ValidateCaptchaAsync(reportError);
    }
}
