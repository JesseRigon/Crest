using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Integrations.GitHub.Services;

namespace Grove.Integrations.GitHub.Configuration;

/// <summary>Registers GitHub sign-in when the GitHub app credentials are set.</summary>
internal sealed class AuthenticationOptionsConfiguration : IConfigureOptions<AuthenticationOptions>
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public AuthenticationOptionsConfiguration(IServiceProvider services, ILogger<AuthenticationOptionsConfiguration> logger)
    {
        _services = services;
        _logger = logger;
    }

    public void Configure(AuthenticationOptions options)
    {
        var app = AppCredentialsAccess.Read(_services, GitHubConstants.AppCredentialsKey, _logger);
        if (string.IsNullOrWhiteSpace(app?.ClientId) || string.IsNullOrWhiteSpace(app.ClientSecret))
        {
            _logger.LogWarning("The GitHub login provider is enabled but the GitHub app credentials are not set (Connections).");

            return;
        }

        options.AddScheme<GitHubAuthenticationHandler>(GitHubAuthenticationDefaults.AuthenticationScheme, GitHubAuthenticationDefaults.DisplayName);
    }
}
