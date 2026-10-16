using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Grove.Integrations.Microsoft.Services;
using Grove.Integrations.Microsoft.Settings;
using MicrosoftIdentityDefaults = Microsoft.Identity.Web.Constants;


namespace Grove.Integrations.Microsoft.Configuration;

public class AzureADOptionsConfiguration :
    IConfigureOptions<AuthenticationOptions>,
    IConfigureNamedOptions<PolicySchemeOptions>,
    IConfigureNamedOptions<MicrosoftIdentityOptions>
{
    public const string AzureAdOpenIdConnectScheme = MicrosoftIdentityDefaults.AzureAd + OpenIdConnectDefaults.AuthenticationScheme;

    private readonly AzureADSettings _azureADSettings;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public AzureADOptionsConfiguration(IOptions<AzureADSettings> azureADSettings, IServiceProvider services, ILogger<AzureADOptionsConfiguration> logger)
    {
        _azureADSettings = azureADSettings.Value;
        _services = services;
        _logger = logger;
    }

    public void Configure(AuthenticationOptions options)
    {
        var settings = _azureADSettings;
        if (settings == null)
        {
            return;
        }

        var app = AppCredentialsAccess.Read(_services, MicrosoftAuthenticationConstants.AppCredentialsKey, _logger);
        if (string.IsNullOrWhiteSpace(app?.ClientId) || string.IsNullOrWhiteSpace(settings.TenantId))
        {
            _logger.LogWarning("The AzureAD login provider is enabled but not configured (the Microsoft app credentials on Connections, the tenant id in its settings).");

            return;
        }

        // Register the OpenID Connect client handler in the authentication handlers collection.
        options.AddScheme(Constants.AzureAd, builder =>
        {
            builder.DisplayName = settings.DisplayName;
            builder.HandlerType = typeof(PolicySchemeHandler);
        });

        options.AddScheme(AzureAdOpenIdConnectScheme, builder =>
        {
            builder.HandlerType = typeof(OpenIdConnectHandler);
        });
    }

    public void Configure(string name, MicrosoftIdentityOptions options)
    {
        if (!string.Equals(name, MicrosoftIdentityDefaults.AzureAd, StringComparison.Ordinal))
        {
            return;
        }

        var loginSettings = _azureADSettings;
        if (loginSettings == null)
        {
            return;
        }

        var app = AppCredentialsAccess.Read(_services, MicrosoftAuthenticationConstants.AppCredentialsKey, _logger);
        options.ClientId = app?.ClientId;
        options.ClientSecret = app?.ClientSecret;
        options.TenantId = loginSettings.TenantId;
        options.Instance = "https://login.microsoftonline.com/";

        if (!string.IsNullOrEmpty(loginSettings.CallbackPath))
        {
            options.CallbackPath = loginSettings.CallbackPath;
        }
    }

    public void Configure(MicrosoftIdentityOptions options) => Debug.Fail("This infrastructure method shouldn't be called.");

    public void Configure(string name, PolicySchemeOptions options)
    {
        if (!string.Equals(name, MicrosoftIdentityDefaults.AzureAd, StringComparison.Ordinal))
        {
            return;
        }

        options.ForwardDefault = "Identity.External";
        options.ForwardChallenge = AzureAdOpenIdConnectScheme;
    }
    public void Configure(PolicySchemeOptions options) => Debug.Fail("This infrastructure method shouldn't be called.");
}
