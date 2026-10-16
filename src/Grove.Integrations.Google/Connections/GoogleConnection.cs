#nullable enable

using Grove.Data.Connectors;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Integrations.Google.Connections;

/// <summary>
/// The Google APIs (<c>www.googleapis.com</c>): OAuth 2.0 authorization code with PKCE on the
/// OAuth client Google sign-in shares, as the tenant's grant or a staff user's own. Google
/// describes its APIs with discovery documents, not OpenAPI, so the operations the integration
/// uses are declared; a tenant adds scopes by defining a connection of its own.
/// </summary>
public static class GoogleConnection
{
    public static IServiceCollection AddGoogleConnection(this IServiceCollection services) =>
        services.AddConnection(GoogleConstants.ConnectionKey, connection => connection
            .DisplayName("Google", "Google APIs on the tenant's Google OAuth client.")
            .Feature(GoogleConstants.Features.Google)
            .Protocol(ConnectorProtocols.Rest)
            .BaseUrl("https://www.googleapis.com")
            .Auth(new ConnectionAuth
            {
                Scheme = AuthSchemes.OAuth2AuthorizationCode,
                AuthorizeUrl = "https://accounts.google.com/o/oauth2/v2/auth",
                TokenUrl = "https://oauth2.googleapis.com/token",
                RevokeUrl = "https://oauth2.googleapis.com/revoke",
                Scopes = ["openid", "email", "profile"],
                AppCredentialsKey = GoogleConstants.AppCredentialsKey,
                UsePkce = true,
                // Google hands out a refresh token only for offline access, and again only on consent.
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["authorize:access_type"] = "offline",
                    ["authorize:prompt"] = "consent",
                },
            })
            .AllowScopes(CredentialScope.Tenant, CredentialScope.User)
            .Operation(new ConnectionOperation
            {
                Id = GoogleConstants.Operations.GetUserInfo,
                DisplayName = "Get the connected account",
                Method = "GET",
                Path = "/oauth2/v3/userinfo",
                IsSafeToRetry = true,
                Tags = ["users"],
            }));
}
