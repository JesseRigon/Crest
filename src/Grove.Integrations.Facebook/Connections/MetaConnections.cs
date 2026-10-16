#nullable enable

using Grove.Data.Connectors;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Integrations.Facebook.Connections;

/// <summary>
/// Meta's connections. The Graph API runs on OAuth 2.0 with the Meta app credentials Meta Login
/// shares, as the tenant's grant or a staff user's own. The Conversions API runs on the
/// dataset's access token. Meta publishes no OpenAPI document for the Graph API, so the
/// operations the integration uses are declared.
/// </summary>
public static class MetaConnections
{
    private const string GraphBaseUrl = "https://graph.facebook.com";

    public static IServiceCollection AddGraphConnection(this IServiceCollection services) =>
        services.AddConnection(FacebookConstants.GraphConnectionKey, connection => connection
            .DisplayName("Meta Graph API", "Facebook and Instagram data through the Graph API.")
            .Feature(FacebookConstants.Features.Core)
            .Protocol(ConnectorProtocols.Rest)
            .BaseUrl($"{GraphBaseUrl}/{FacebookConstants.GraphApiVersion}")
            .OAuth2AuthorizationCode(
                $"https://www.facebook.com/{FacebookConstants.GraphApiVersion}/dialog/oauth",
                $"{GraphBaseUrl}/{FacebookConstants.GraphApiVersion}/oauth/access_token",
                ["public_profile", "email"],
                appCredentialsKey: FacebookConstants.AppCredentialsKey)
            .AllowScopes(CredentialScope.Tenant, CredentialScope.User)
            .Policy(policy => policy with { RateLimit = new RateLimitPolicy(200, TimeSpan.FromHours(1)) })
            .Operation(new ConnectionOperation
            {
                Id = FacebookConstants.Operations.GetMe,
                DisplayName = "Get the connected account",
                Method = "GET",
                Path = "/me",
                IsSafeToRetry = true,
                Parameters = [new OperationParameter("fields", ParameterLocation.Query, Description: "Comma-separated fields, e.g. id,name,email.")],
                Tags = ["users"],
            }));

    public static IServiceCollection AddConversionsConnection(this IServiceCollection services) =>
        services.AddConnection(FacebookConstants.ConversionsConnectionKey, connection => connection
            .DisplayName("Meta Conversions API", "Server-side events for the Meta Pixel's dataset. The credential is the dataset's access token from the Events Manager.")
            .Feature(FacebookConstants.Features.Pixel)
            .Protocol(ConnectorProtocols.Rest)
            .BaseUrl(GraphBaseUrl)
            .ApiKey("access_token", ApiKeyLocation.Query)
            .Policy(policy => policy with { Timeout = TimeSpan.FromSeconds(15) })
            .Operation(new ConnectionOperation
            {
                Id = FacebookConstants.Operations.SendEvents,
                DisplayName = "Send conversion events",
                Description = "Sends server-side events to the pixel's dataset (POST /{pixelId}/events).",
                Method = "POST",
                Path = $"/{FacebookConstants.GraphApiVersion}/{{pixelId}}/events",
                ContentType = "application/json",
                Parameters =
                [
                    new OperationParameter("pixelId", ParameterLocation.Path, Required: true),
                    new OperationParameter("data", ParameterLocation.Body, Required: true, Schema: """{"type":"array"}"""),
                    new OperationParameter("test_event_code", ParameterLocation.Body),
                ],
                Tags = ["conversions"],
            }));
}
