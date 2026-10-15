using Grove.Access;
using Grove.ReCaptcha.Configuration;
using Microsoft.Extensions.Options;

namespace Grove.ReCaptcha.Users;

/// <summary>
/// The captcha on the sign-in forms, while ReCaptcha is configured: a section the form renders
/// (the <c>CaptchaChallenge</c> component) whose token it sends as the header the login,
/// registration and password recovery events verify.
/// </summary>
public sealed class ReCaptchaLoginExtensions(IOptionsMonitor<ReCaptchaSettings> settings) : ILoginExtensionProvider
{
    public Task<IEnumerable<LoginExtension>> GetExtensionsAsync(LoginExtensionContext context)
    {
        if (!settings.CurrentValue.ConfigurationExists())
        {
            return Task.FromResult<IEnumerable<LoginExtension>>([]);
        }

        return Task.FromResult<IEnumerable<LoginExtension>>(
        [
            new LoginExtension(
                "recaptcha",
                LoginExtensionKind.Section,
                "Captcha",
                Component: "CaptchaChallenge",
                Header: Constants.ReCaptchaServerResponseHeaderName),
        ]);
    }
}
