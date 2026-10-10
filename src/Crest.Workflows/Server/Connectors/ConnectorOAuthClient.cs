using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Crest.Workflows.Connectors;

/// <summary>The OAuth2 authorization-code exchanges: build the authorize URL, trade the code for tokens, refresh them.</summary>
public sealed class ConnectorOAuthClient(ConnectorHttpClient http, WorkflowConnectionService connections)
{
    public static string BuildAuthorizeUrl(WorkflowConnection connection, string redirectUri, string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = connection.Setting(WorkflowConnectionSettingKeys.ClientId),
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
        };
        if (connection.Setting(WorkflowConnectionSettingKeys.Scope) is { } scope)
        {
            query["scope"] = scope;
        }

        return QueryHelpers.AddQueryString(connection.Setting(WorkflowConnectionSettingKeys.AuthorizeUrl)!, query);
    }

    public Task<(WorkflowConnectionTokens? Tokens, string? Error)> ExchangeCodeAsync(WorkflowConnection connection, string code, string redirectUri, CancellationToken cancellationToken) =>
        RequestAsync(connection, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
        }, cancellationToken);

    public Task<(WorkflowConnectionTokens? Tokens, string? Error)> RefreshAsync(WorkflowConnection connection, string refreshToken, CancellationToken cancellationToken) =>
        RequestAsync(connection, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        }, cancellationToken);

    private async Task<(WorkflowConnectionTokens? Tokens, string? Error)> RequestAsync(WorkflowConnection connection, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        form["client_id"] = connection.Setting(WorkflowConnectionSettingKeys.ClientId) ?? string.Empty;
        form["client_secret"] = connections.RevealSecret(connection) ?? string.Empty;

        using var response = await http.Client.PostAsync(connection.Setting(WorkflowConnectionSettingKeys.TokenUrl), new FormUrlEncodedContent(form), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (null, $"Token endpoint answered HTTP {(int)response.StatusCode}.");
        }

        try
        {
            using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var root = json.RootElement;
            var access = root.TryGetProperty("access_token", out var a) ? a.GetString() : null;
            if (string.IsNullOrEmpty(access))
            {
                return (null, "Token endpoint returned no access_token.");
            }

            var refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : form.GetValueOrDefault("refresh_token");
            var expiresIn = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var seconds) ? seconds : 3600;
            return (new WorkflowConnectionTokens(access, refresh, DateTime.UtcNow.AddSeconds(Math.Max(0, expiresIn - 30))), null);
        }
        catch (JsonException)
        {
            return (null, "Token endpoint returned no JSON.");
        }
    }
}
