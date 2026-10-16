using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Integrations.Google.Authentication.Settings;
using Grove.Integrations.Google.Services;

namespace Grove.Integrations.Google.Authentication.Configuration;

/// <summary>
/// Registers Google sign-in when the Google app credentials are set, and configures the handler
/// with them (client id and secret from the app credentials, the rest from the sign-in settings).
/// </summary>
public class GoogleOptionsConfiguration :
    IConfigureOptions<AuthenticationOptions>,
    IConfigureNamedOptions<GoogleOptions>
{
    private readonly GoogleAuthenticationSettings _googleAuthenticationSettings;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public GoogleOptionsConfiguration(
        IOptions<GoogleAuthenticationSettings> googleAuthenticationSettings,
        IServiceProvider services,
        ILogger<GoogleOptionsConfiguration> logger)
    {
        _googleAuthenticationSettings = googleAuthenticationSettings.Value;
        _services = services;
        _logger = logger;
    }

    public void Configure(AuthenticationOptions options)
    {
        var app = AppCredentialsAccess.Read(_services, GoogleConstants.AppCredentialsKey, _logger);
        if (string.IsNullOrWhiteSpace(app?.ClientId) || string.IsNullOrWhiteSpace(app.ClientSecret))
        {
            _logger.LogWarning("The Google login provider is enabled but the Google app credentials are not set (Connections).");

            return;
        }

        options.AddScheme(GoogleDefaults.AuthenticationScheme, builder =>
        {
            builder.DisplayName = "Google";
            builder.HandlerType = typeof(GoogleHandler);
        });
    }

    public void Configure(string name, GoogleOptions options)
    {
        if (!string.Equals(name, GoogleDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        var app = AppCredentialsAccess.Read(_services, GoogleConstants.AppCredentialsKey, _logger);
        if (app is null)
        {
            return;
        }

        options.ClientId = app.ClientId;
        options.ClientSecret = app.ClientSecret;

        if (!string.IsNullOrEmpty(_googleAuthenticationSettings?.CallbackPath))
        {
            options.CallbackPath = _googleAuthenticationSettings.CallbackPath;
        }

        options.SaveTokens = _googleAuthenticationSettings?.SaveTokens ?? false;
    }

    public void Configure(GoogleOptions options) => Debug.Fail("This infrastructure method shouldn't be called.");
}
