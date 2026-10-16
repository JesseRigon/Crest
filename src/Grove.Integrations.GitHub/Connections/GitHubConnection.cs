#nullable enable

using Grove.Data.Connectors;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Integrations.GitHub.Connections;

/// <summary>
/// The GitHub REST API: OAuth 2.0 authorization code on the app credentials GitHub sign-in
/// shares, as the tenant's grant or a staff user's own. GitHub's OpenAPI description is far
/// larger than a connection's response cap, so the operations the integration uses are declared.
/// </summary>
public static class GitHubConnection
{
    public static IServiceCollection AddGitHubConnection(this IServiceCollection services) =>
        services.AddConnection(GitHubConstants.ConnectionKey, connection => connection
            .DisplayName("GitHub", "The GitHub REST API.")
            .Feature(GitHubConstants.Features.GitHub)
            .Protocol(ConnectorProtocols.Rest)
            .BaseUrl("https://api.github.com")
            .OAuth2AuthorizationCode(
                "https://github.com/login/oauth/authorize",
                "https://github.com/login/oauth/access_token",
                ["read:user", "user:email", "repo"],
                appCredentialsKey: GitHubConstants.AppCredentialsKey)
            .AllowScopes(CredentialScope.Tenant, CredentialScope.User)
            .Policy(policy => policy with { RateLimit = new RateLimitPolicy(5000, TimeSpan.FromHours(1)) })
            .Operation(new ConnectionOperation
            {
                Id = GitHubConstants.Operations.GetAuthenticatedUser,
                DisplayName = "Get the connected user",
                Method = "GET",
                Path = "/user",
                IsSafeToRetry = true,
                Tags = ["users"],
            })
            .Operation(new ConnectionOperation
            {
                Id = GitHubConstants.Operations.ListRepositories,
                DisplayName = "List the connected user's repositories",
                Method = "GET",
                Path = "/user/repos",
                IsSafeToRetry = true,
                Parameters =
                [
                    new OperationParameter("visibility", ParameterLocation.Query, Description: "all, public or private."),
                    new OperationParameter("sort", ParameterLocation.Query, Description: "created, updated, pushed or full_name."),
                ],
                Paging = new OperationPaging(PagingStyle.Link, Parameter: "page", SizeParameter: "per_page"),
                Tags = ["repos"],
            }));
}
