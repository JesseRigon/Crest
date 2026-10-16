#nullable enable

using Grove.Data.Connectors;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Integrations.Twitter.Connections;

/// <summary>
/// The X API v2 connection: REST, OAuth 2.0 authorization code with PKCE on the app
/// credentials Sign in with X shares, posting as the tenant's account (an admin's grant) or as
/// the staff user's own (delegated). X publishes no OpenAPI document small enough to bundle, so
/// the operations the integration uses are declared.
/// </summary>
public static class XConnection
{
    public static readonly string[] Scopes = ["tweet.read", "tweet.write", "users.read", "offline.access"];

    public static IServiceCollection AddXConnection(this IServiceCollection services) =>
        services.AddConnection(TwitterConstants.ConnectionKey, connection => connection
            .DisplayName("X (Twitter)", "Posts and reads on X through the X API v2.")
            .Feature(TwitterConstants.Features.Twitter)
            .Protocol(ConnectorProtocols.Rest)
            .BaseUrl(TwitterConstants.Endpoints.Api)
            .Auth(new ConnectionAuth
            {
                Scheme = AuthSchemes.OAuth2AuthorizationCode,
                AuthorizeUrl = TwitterConstants.Endpoints.Authorize,
                TokenUrl = TwitterConstants.Endpoints.Token,
                RevokeUrl = TwitterConstants.Endpoints.Revoke,
                Scopes = Scopes,
                AppCredentialsKey = TwitterConstants.AppCredentialsKey,
                UsePkce = true,
                // A confidential X client authenticates at the token endpoint with HTTP Basic.
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["tokenEndpointAuth"] = "client_secret_basic",
                },
            })
            .AllowScopes(CredentialScope.Tenant, CredentialScope.User)
            .Policy(policy => policy with
            {
                // X's per-user post limit is low; stay well under it and never hammer on 429.
                RateLimit = new RateLimitPolicy(50, TimeSpan.FromMinutes(15)),
                Timeout = TimeSpan.FromSeconds(20),
            })
            .Operation(new ConnectionOperation
            {
                Id = TwitterConstants.Operations.CreatePost,
                DisplayName = "Create post",
                Description = "Posts on X as the connected account.",
                Method = "POST",
                Path = "/tweets",
                ContentType = "application/json",
                Parameters = [new OperationParameter("text", ParameterLocation.Body, Required: true, Schema: """{"type":"string","maxLength":280}""", Description: "The text to post.")],
                OutputSchema = """{"type":"object","properties":{"data":{"type":"object","properties":{"id":{"type":"string"},"text":{"type":"string"}}}}}""",
                Tags = ["posts"],
            })
            .Operation(new ConnectionOperation
            {
                Id = TwitterConstants.Operations.GetMe,
                DisplayName = "Get the connected account",
                Method = "GET",
                Path = "/users/me",
                IsSafeToRetry = true,
                OutputSchema = """{"type":"object","properties":{"data":{"type":"object","properties":{"id":{"type":"string"},"name":{"type":"string"},"username":{"type":"string"}}}}}""",
                Tags = ["users"],
            }));
}
