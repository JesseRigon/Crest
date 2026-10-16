using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Integrations.Facebook.Login.Services;
using Grove.Integrations.Facebook.Login.Settings;
using Grove.Integrations.Facebook.Services;

namespace Grove.Integrations.Facebook.Login.Configuration;

/// <summary>
/// Registers Meta Login when the Meta app credentials are set, and configures the handler with
/// them (app id and secret from the app credentials, the rest from the login settings).
/// </summary>
public class FacebookLoginConfiguration :
    IConfigureOptions<AuthenticationOptions>,
    IConfigureNamedOptions<FacebookOptions>
{
    private readonly IServiceProvider _services;
    private readonly IFacebookLoginService _loginService;
    private readonly ILogger _logger;

    public FacebookLoginConfiguration(
        IServiceProvider services,
        IFacebookLoginService loginService,
        ILogger<FacebookLoginConfiguration> logger)
    {
        _services = services;
        _loginService = loginService;
        _logger = logger;
    }

    public void Configure(AuthenticationOptions options)
    {
        var app = AppCredentialsAccess.Read(_services, FacebookConstants.AppCredentialsKey, _logger);
        if (string.IsNullOrWhiteSpace(app?.ClientId) || string.IsNullOrWhiteSpace(app.ClientSecret))
        {
            _logger.LogWarning("The Facebook login provider is enabled but the Meta app credentials are not set (Connections).");

            return;
        }

        if (GetFacebookLoginSettingsAsync().GetAwaiter().GetResult() is null)
        {
            return;
        }

        options.AddScheme(FacebookDefaults.AuthenticationScheme, builder =>
        {
            builder.DisplayName = "Facebook";
            builder.HandlerType = typeof(FacebookHandler);
        });
    }

    public void Configure(string name, FacebookOptions options)
    {
        if (!string.Equals(name, FacebookDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        var loginSettings = GetFacebookLoginSettingsAsync().GetAwaiter().GetResult();
        var app = AppCredentialsAccess.Read(_services, FacebookConstants.AppCredentialsKey, _logger);
        if (loginSettings is null || app is null)
        {
            return;
        }

        options.AppId = app.ClientId;
        options.AppSecret = app.ClientSecret;

        if (!string.IsNullOrEmpty(loginSettings.CallbackPath))
        {
            options.CallbackPath = loginSettings.CallbackPath;
        }

        options.SaveTokens = loginSettings.SaveTokens;
    }

    public void Configure(FacebookOptions options) => Debug.Fail("This infrastructure method shouldn't be called.");

    private async Task<FacebookLoginSettings> GetFacebookLoginSettingsAsync()
    {
        var settings = await _loginService.GetSettingsAsync();
        if ((await _loginService.ValidateSettingsAsync(settings)).Any(result => result != ValidationResult.Success))
        {
            _logger.LogWarning("The Facebook Login module is not correctly configured.");

            return null;
        }

        return settings;
    }
}
