using System.Diagnostics;
using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Integrations.GitHub.Services;
using Grove.Integrations.GitHub.Settings;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.GitHub.Configuration;

/// <summary>Configures the GitHub handler: client id and secret from the GitHub app credentials, the rest from the sign-in settings.</summary>
internal sealed class GitHubAuthenticationOptionsConfiguration : IConfigureNamedOptions<GitHubAuthenticationOptions>
{
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public GitHubAuthenticationOptionsConfiguration(
        ISiteService siteService,
        IServiceProvider services,
        ILogger<GitHubAuthenticationOptionsConfiguration> logger)
    {
        _siteService = siteService;
        _services = services;
        _logger = logger;
    }

    public void Configure(string name, GitHubAuthenticationOptions options)
    {
        if (!string.Equals(name, GitHubAuthenticationDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        var app = AppCredentialsAccess.Read(_services, GitHubConstants.AppCredentialsKey, _logger);
        if (app is null)
        {
            return;
        }

        var settings = _siteService.GetSettings<GitHubAuthenticationSettings>() ?? new GitHubAuthenticationSettings();

        options.CallbackPath = new PathString("/signin-github");
        options.ClaimActions.MapJsonKey("name", "login");
        options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email", ClaimValueTypes.Email);
        options.ClaimActions.MapJsonKey("url", "url");

        options.ClientId = app.ClientId;
        options.ClientSecret = app.ClientSecret;

        if (!string.IsNullOrEmpty(settings.CallbackPath))
        {
            options.CallbackPath = settings.CallbackPath;
        }

        options.SaveTokens = settings.SaveTokens;
    }

    public void Configure(GitHubAuthenticationOptions options)
        => Debug.Fail("This infrastructure method shouldn't be called.");
}
