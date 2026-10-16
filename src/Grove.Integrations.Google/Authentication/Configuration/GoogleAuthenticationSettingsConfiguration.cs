using Microsoft.Extensions.Options;
using Grove.Integrations.Google.Authentication.Settings;

namespace Grove.Integrations.Google.Authentication.Services;

public sealed class GoogleAuthenticationSettingsConfiguration : IConfigureOptions<GoogleAuthenticationSettings>
{
    private readonly GoogleAuthenticationService _googleAuthenticationService;

    public GoogleAuthenticationSettingsConfiguration(GoogleAuthenticationService googleAuthenticationService)
    {
        _googleAuthenticationService = googleAuthenticationService;
    }

    public void Configure(GoogleAuthenticationSettings options)
    {
        var settings = _googleAuthenticationService.GetSettingsAsync().GetAwaiter().GetResult();

        options.CallbackPath = settings.CallbackPath;
        options.SaveTokens = settings.SaveTokens;
    }
}
