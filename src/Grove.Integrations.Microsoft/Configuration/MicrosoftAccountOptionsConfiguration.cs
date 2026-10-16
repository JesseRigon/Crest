using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Integrations.Microsoft.Services;
using Grove.Integrations.Microsoft.Settings;

namespace Grove.Integrations.Microsoft.Configuration;

/// <summary>
/// Registers Microsoft account sign-in when the Microsoft app credentials are set, and configures
/// the handler with them (client id and secret from the app credentials, the rest from the settings).
/// </summary>
public class MicrosoftAccountOptionsConfiguration :
    IConfigureOptions<AuthenticationOptions>,
    IConfigureNamedOptions<MicrosoftAccountOptions>
{
    private readonly MicrosoftAccountSettings _microsoftAccountSettings;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public MicrosoftAccountOptionsConfiguration(
        IOptions<MicrosoftAccountSettings> microsoftAccountSettings,
        IServiceProvider services,
        ILogger<MicrosoftAccountOptionsConfiguration> logger)
    {
        _microsoftAccountSettings = microsoftAccountSettings.Value;
        _services = services;
        _logger = logger;
    }

    public void Configure(AuthenticationOptions options)
    {
        var app = AppCredentialsAccess.Read(_services, MicrosoftAuthenticationConstants.AppCredentialsKey, _logger);
        if (string.IsNullOrWhiteSpace(app?.ClientId) || string.IsNullOrWhiteSpace(app.ClientSecret))
        {
            _logger.LogWarning("The Microsoft login provider is enabled but the Microsoft app credentials are not set (Connections).");

            return;
        }

        options.AddScheme(MicrosoftAccountDefaults.AuthenticationScheme, builder =>
        {
            builder.DisplayName = "Microsoft Account";
            builder.HandlerType = typeof(MicrosoftAccountHandler);
        });
    }

    public void Configure(string name, MicrosoftAccountOptions options)
    {
        if (!string.Equals(name, MicrosoftAccountDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        var app = AppCredentialsAccess.Read(_services, MicrosoftAuthenticationConstants.AppCredentialsKey, _logger);
        if (app is null)
        {
            return;
        }

        options.ClientId = app.ClientId;
        options.ClientSecret = app.ClientSecret;

        if (!string.IsNullOrEmpty(_microsoftAccountSettings?.CallbackPath))
        {
            options.CallbackPath = _microsoftAccountSettings.CallbackPath;
        }

        options.SaveTokens = _microsoftAccountSettings?.SaveTokens ?? false;
    }

    public void Configure(MicrosoftAccountOptions options) => Debug.Fail("This infrastructure method shouldn't be called.");
}
