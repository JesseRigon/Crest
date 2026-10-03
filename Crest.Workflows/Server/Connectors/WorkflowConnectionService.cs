using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Documents;

namespace Crest.Workflows.Connectors;

/// <summary>The tenant's connections: validation, sealing of secrets, and the API shape.</summary>
public sealed partial class WorkflowConnectionService(IDocumentManager<WorkflowConnectionsDocument> documents, IDataProtectionProvider dataProtection)
{
    private const string ProtectorPurpose = "Crest.Workflows.Connections";
    private IDataProtector Protector => dataProtection.CreateProtector(ProtectorPurpose);

    public async Task<IReadOnlyList<WorkflowConnection>> ListAsync() =>
        (await documents.GetOrCreateImmutableAsync()).Connections.Values.OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase).ToArray();

    public async Task<WorkflowConnection?> FindAsync(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : (await documents.GetOrCreateImmutableAsync()).Connections.GetValueOrDefault(key.Trim());

    public string? RevealSecret(WorkflowConnection connection) =>
        string.IsNullOrEmpty(connection.ProtectedSecret) ? null : Protector.Unprotect(connection.ProtectedSecret);

    /// <summary>Creates (<paramref name="existingKey"/> null) or updates a connection; returns the errors, if any.</summary>
    public async Task<(WorkflowConnection? Connection, IReadOnlyList<string> Errors)> SaveAsync(string? existingKey, WorkflowConnectionSaveRequest request, WorkflowConnectorDescriptor? connector)
    {
        var document = await documents.GetOrCreateMutableAsync();
        var errors = new List<string>();

        WorkflowConnection connection;
        if (existingKey is null)
        {
            var key = request.Key?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!KeyPattern().IsMatch(key))
            {
                errors.Add("Key must be 1-64 lowercase letters, digits or dashes, starting with a letter or digit.");
            }
            else if (document.Connections.ContainsKey(key))
            {
                errors.Add($"A connection '{key}' already exists.");
            }

            connection = new WorkflowConnection { Key = key, ConnectorKey = connector?.Key, BaseUrl = connector?.BaseUrl, AuthKind = connector?.AuthKind ?? WorkflowConnectorAuthKinds.None };
        }
        else if (!document.Connections.TryGetValue(existingKey, out var found))
        {
            return (null, [$"No connection '{existingKey}'."]);
        }
        else
        {
            connection = found;
        }

        if (request.DisplayName is not null) connection.DisplayName = request.DisplayName.Trim();
        if (request.BaseUrl is not null) connection.BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl.Trim();
        if (request.AuthKind is not null) connection.AuthKind = request.AuthKind.Trim().ToLowerInvariant();
        if (request.Settings is not null) connection.Settings = new Dictionary<string, string>(request.Settings, StringComparer.OrdinalIgnoreCase);
        if (request.RetryCount is not null) connection.RetryCount = request.RetryCount.Value;
        if (request.RateLimitPerMinute is not null) connection.RateLimitPerMinute = request.RateLimitPerMinute.Value <= 0 ? null : request.RateLimitPerMinute.Value;
        if (request.TimeoutSeconds is not null) connection.TimeoutSeconds = request.TimeoutSeconds.Value;
        if (request.Secret is not null) connection.ProtectedSecret = request.Secret.Length == 0 ? null : Protector.Protect(request.Secret);
        if (string.IsNullOrWhiteSpace(connection.DisplayName)) connection.DisplayName = connector?.DisplayName ?? connection.Key;

        Validate(connection, errors);
        if (errors.Count > 0)
        {
            return (null, errors);
        }

        connection.UpdatedUtc = DateTime.UtcNow;
        document.Connections[connection.Key] = connection;
        await documents.UpdateAsync(document);
        return (connection, []);
    }

    public async Task<bool> DeleteAsync(string key)
    {
        var document = await documents.GetOrCreateMutableAsync();
        if (!document.Connections.Remove(key))
        {
            return false;
        }

        await documents.UpdateAsync(document);
        return true;
    }

    public WorkflowConnectionModel ToModel(WorkflowConnection c)
    {
        var tokens = ReadTokens(c);
        return new(c.Key, c.DisplayName, c.ConnectorKey, c.BaseUrl, c.AuthKind, c.Settings, !string.IsNullOrEmpty(c.ProtectedSecret), c.RetryCount, c.RateLimitPerMinute, c.TimeoutSeconds, c.UpdatedUtc,
            Authorized: tokens is not null, TokenExpiresUtc: tokens?.ExpiresUtc, Operations: ReadOperations(c));
    }

    /// <summary>The operations an OpenAPI import stored on the connection, if any.</summary>
    public static IReadOnlyList<WorkflowConnectionOperation>? ReadOperations(WorkflowConnection c)
    {
        var json = c.Setting(WorkflowConnectionSettingKeys.Operations);
        if (json is null)
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<WorkflowConnectionOperation>>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public WorkflowConnectionTokens? ReadTokens(WorkflowConnection c)
    {
        if (string.IsNullOrEmpty(c.ProtectedTokens))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<WorkflowConnectionTokens>(Protector.Unprotect(c.ProtectedTokens));
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    /// <summary>Seals an authorization-code connection's tokens; null clears them (re-authorization needed).</summary>
    public async Task StoreTokensAsync(string key, WorkflowConnectionTokens? tokens)
    {
        var document = await documents.GetOrCreateMutableAsync();
        if (!document.Connections.TryGetValue(key, out var connection))
        {
            return;
        }

        connection.ProtectedTokens = tokens is null ? null : Protector.Protect(System.Text.Json.JsonSerializer.Serialize(tokens));
        await documents.UpdateAsync(document);
    }

    private static void Validate(WorkflowConnection c, List<string> errors)
    {
        if (!WorkflowConnectorAuthKinds.All.Contains(c.AuthKind))
        {
            errors.Add($"Unknown auth kind '{c.AuthKind}'. One of: {string.Join(", ", WorkflowConnectorAuthKinds.All)}.");
        }

        // An inbound-only (webhook) connection needs no base URL; every outgoing one does.
        if (c.AuthKind != WorkflowConnectorAuthKinds.Hmac || c.BaseUrl is not null)
        {
            if (!IsHttpUrl(c.BaseUrl))
            {
                errors.Add("Base URL must be an absolute http or https URL.");
            }
        }

        if (c.AuthKind is WorkflowConnectorAuthKinds.OAuth2ClientCredentials or WorkflowConnectorAuthKinds.OAuth2AuthorizationCode)
        {
            if (!IsHttpUrl(c.Setting(WorkflowConnectionSettingKeys.TokenUrl))) errors.Add("OAuth2 needs setting 'tokenUrl' (an absolute http or https URL).");
            if (c.Setting(WorkflowConnectionSettingKeys.ClientId) is null) errors.Add("OAuth2 needs setting 'clientId'.");
        }

        if (c.AuthKind == WorkflowConnectorAuthKinds.OAuth2AuthorizationCode && !IsHttpUrl(c.Setting(WorkflowConnectionSettingKeys.AuthorizeUrl)))
        {
            errors.Add("OAuth2 authorization code needs setting 'authorizeUrl' (an absolute http or https URL).");
        }

        if (c.AuthKind == WorkflowConnectorAuthKinds.Basic && c.Setting(WorkflowConnectionSettingKeys.Username) is null)
        {
            errors.Add("Basic auth needs setting 'username'.");
        }

        if (c.AuthKind is not WorkflowConnectorAuthKinds.None && string.IsNullOrEmpty(c.ProtectedSecret))
        {
            errors.Add($"Auth kind '{c.AuthKind}' needs a secret.");
        }

        if (c.RetryCount is < 0 or > 5) errors.Add("Retry count must be 0-5.");
        if (c.TimeoutSeconds is < 1 or > 120) errors.Add("Timeout must be 1-120 seconds.");
    }

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && string.IsNullOrEmpty(uri.UserInfo);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex KeyPattern();
}
