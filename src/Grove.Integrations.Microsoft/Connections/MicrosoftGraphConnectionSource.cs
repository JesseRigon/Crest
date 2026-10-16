#nullable enable

using Grove.Data.Connectors;
using Grove.Infrastructure.Settings;
using Grove.Integrations.Microsoft.Settings;

namespace Grove.Integrations.Microsoft.Connections;

/// <summary>
/// Declares the Microsoft Graph connection: REST over <c>graph.microsoft.com/v1.0</c>, OAuth 2.0
/// authorization code with PKCE on the Microsoft app credentials the sign-in providers share, as
/// the tenant's grant or a staff user's own. The authority is the Entra ID tenant from the Entra
/// ID settings (<c>common</c> without one), so the connection is computed rather than fixed; its
/// version follows the authority. Graph's OpenAPI description is far larger than a connection's
/// response cap, so the operations the integration uses are declared.
/// </summary>
public sealed class MicrosoftGraphConnectionSource(ISiteService siteService) : IConnectionSource
{
    public static readonly string[] Scopes = ["openid", "profile", "offline_access", "User.Read", "Mail.Send", "Mail.ReadWrite"];

    public ConnectionOrigin Origin => ConnectionOrigin.Declared;

    public async Task<IEnumerable<ConnectionDefinition>> GetConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await siteService.GetSettingsAsync<AzureADSettings>();
        var tenant = string.IsNullOrWhiteSpace(settings?.TenantId) ? "common" : settings.TenantId.Trim();

        return [Build(tenant)];
    }

    public static ConnectionDefinition Build(string tenant)
    {
        var authority = $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/v2.0";
        var definition = new ConnectionBuilder(MicrosoftAuthenticationConstants.GraphConnectionKey)
            .DisplayName("Microsoft Graph", "Microsoft 365 and Entra ID data through Microsoft Graph.")
            .Feature(MicrosoftAuthenticationConstants.Features.Graph)
            .Protocol(ConnectorProtocols.Rest)
            .BaseUrl("https://graph.microsoft.com/v1.0")
            .Setting("tenant", tenant)
            .OAuth2AuthorizationCode(
                $"{authority}/authorize",
                $"{authority}/token",
                Scopes,
                appCredentialsKey: MicrosoftAuthenticationConstants.AppCredentialsKey)
            .AllowScopes(CredentialScope.Tenant, CredentialScope.User)
            .Operation(new ConnectionOperation
            {
                Id = MicrosoftAuthenticationConstants.GraphOperations.GetMe,
                DisplayName = "Get the connected user",
                Method = "GET",
                Path = "/me",
                IsSafeToRetry = true,
                Tags = ["users"],
            })
            .Operation(new ConnectionOperation
            {
                Id = MicrosoftAuthenticationConstants.GraphOperations.ListUsers,
                DisplayName = "List users",
                Method = "GET",
                Path = "/users",
                IsSafeToRetry = true,
                Parameters =
                [
                    new OperationParameter("$select", ParameterLocation.Query),
                    new OperationParameter("$filter", ParameterLocation.Query),
                ],
                Paging = new OperationPaging(PagingStyle.Link, SizeParameter: "$top", NextPath: "@odata.nextLink"),
                Tags = ["users"],
            })
            .Operation(new ConnectionOperation
            {
                Id = MicrosoftAuthenticationConstants.GraphOperations.SendMail,
                DisplayName = "Send mail",
                Description = "Sends a message as the connected user (POST /me/sendMail).",
                Method = "POST",
                Path = "/me/sendMail",
                ContentType = "application/json",
                Parameters =
                [
                    new OperationParameter("message", ParameterLocation.Body, Required: true, Schema: """{"type":"object"}"""),
                    new OperationParameter("saveToSentItems", ParameterLocation.Body, Schema: """{"type":"boolean"}"""),
                ],
                Tags = ["mail"],
            })
            .Operation(new ConnectionOperation
            {
                Id = MicrosoftAuthenticationConstants.GraphOperations.ListMessages,
                DisplayName = "List messages",
                Description = "A mail folder's messages (GET /me/mailFolders/{folder}/messages).",
                Method = "GET",
                Path = "/me/mailFolders/{folder}/messages",
                IsSafeToRetry = true,
                Parameters =
                [
                    new OperationParameter("folder", ParameterLocation.Path, Required: true),
                    new OperationParameter("$select", ParameterLocation.Query),
                    new OperationParameter("$filter", ParameterLocation.Query),
                    new OperationParameter("$orderby", ParameterLocation.Query),
                    new OperationParameter("$top", ParameterLocation.Query),
                ],
                Paging = new OperationPaging(PagingStyle.Link, SizeParameter: "$top", NextPath: "@odata.nextLink"),
                Tags = ["mail"],
            })
            .Operation(new ConnectionOperation
            {
                Id = MicrosoftAuthenticationConstants.GraphOperations.GetMessageMime,
                DisplayName = "Get a message's MIME",
                Description = "The raw MIME of a message (GET /me/messages/{id}/$value).",
                Method = "GET",
                Path = "/me/messages/{id}/$value",
                IsSafeToRetry = true,
                Parameters = [new OperationParameter("id", ParameterLocation.Path, Required: true)],
                Tags = ["mail"],
            })
            .Operation(new ConnectionOperation
            {
                Id = MicrosoftAuthenticationConstants.GraphOperations.DeleteMessage,
                DisplayName = "Delete a message",
                Description = "Moves a message to Deleted Items (DELETE /me/messages/{id}).",
                Method = "DELETE",
                Path = "/me/messages/{id}",
                IsSafeToRetry = false,
                Parameters = [new OperationParameter("id", ParameterLocation.Path, Required: true)],
                Tags = ["mail"],
            })
            .Build();

        // The authority is part of the definition: a new tenant id is a new version.
        return new ConnectionDefinition
        {
            Key = definition.Key,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            Protocol = definition.Protocol,
            Origin = definition.Origin,
            FeatureId = definition.FeatureId,
            BaseUrl = definition.BaseUrl,
            Endpoints = definition.Endpoints,
            Schema = definition.Schema,
            Auth = definition.Auth,
            AllowedScopes = definition.AllowedScopes,
            Policy = definition.Policy,
            Operations = definition.Operations,
            Settings = definition.Settings,
            CallerRoles = definition.CallerRoles,
            Version = $"{definition.Version}:{tenant}",
        };
    }
}
