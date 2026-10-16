#nullable enable

using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Integrations.Twitter.Services;
using Grove.Integrations.Twitter.Signin.Services;
using Grove.Integrations.Twitter.Signin.Settings;

namespace Grove.Integrations.Twitter.Signin.Configuration;

/// <summary>
/// Registers Sign in with X when the X app credentials are set, and configures the handler with
/// them (client id and secret from the app credentials, the rest from the sign-in settings).
/// </summary>
public sealed class TwitterOptionsConfiguration :
    IConfigureOptions<AuthenticationOptions>,
    IConfigureNamedOptions<XAuthenticationOptions>
{
    private readonly IServiceProvider _services;
    private readonly ITwitterSigninService _twitterSigninService;
    private readonly string _tenantPrefix;
    private readonly ILogger _logger;

    public TwitterOptionsConfiguration(
        IServiceProvider services,
        ITwitterSigninService twitterSigninService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<TwitterOptionsConfiguration> logger)
    {
        _services = services;
        _twitterSigninService = twitterSigninService;
        _logger = logger;

        var pathBase = httpContextAccessor.HttpContext?.Request.PathBase ?? PathString.Empty;
        _tenantPrefix = pathBase.HasValue ? pathBase : "/";
    }

    public void Configure(AuthenticationOptions options)
    {
        var app = AppCredentialsAccess.Read(_services, TwitterConstants.AppCredentialsKey, _logger);
        if (string.IsNullOrWhiteSpace(app?.ClientId) || string.IsNullOrWhiteSpace(app.ClientSecret))
        {
            _logger.LogWarning("Sign in with X is enabled but the X app credentials are not set (Connections).");

            return;
        }

        options.AddScheme(TwitterConstants.AuthenticationScheme, builder =>
        {
            builder.DisplayName = "X (Twitter)";
            builder.HandlerType = typeof(XAuthenticationHandler);
        });
    }

    public void Configure(string? name, XAuthenticationOptions options)
    {
        if (!string.Equals(name, TwitterConstants.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        var app = AppCredentialsAccess.Read(_services, TwitterConstants.AppCredentialsKey, _logger);
        if (app is null)
        {
            return;
        }

        options.ClientId = app.ClientId;
        options.ClientSecret = app.ClientSecret ?? string.Empty;

        var settings = _twitterSigninService.GetSettingsAsync().GetAwaiter().GetResult() ?? new TwitterSigninSettings();
        if (!string.IsNullOrEmpty(settings.CallbackPath))
        {
            options.CallbackPath = settings.CallbackPath;
        }

        options.SignInScheme = "Identity.External";
        options.CorrelationCookie.Path = _tenantPrefix;
        options.SaveTokens = settings.SaveTokens;
    }

    public void Configure(XAuthenticationOptions options) => Debug.Fail("This infrastructure method shouldn't be called.");
}
