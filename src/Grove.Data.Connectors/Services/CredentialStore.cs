using System.Security.Cryptography;
using System.Text.Json;
using Grove.Data.Connectors.Models;
using Grove.Documents;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Data.Connectors.Services;

/// <summary>
/// The sealed credential store: values and tokens sealed with the tenant's data protection
/// (purpose <see cref="ConnectorsConstants.CredentialsProtectionPurpose"/>), one document per
/// tenant. Reading back a value that no longer opens is an explicit
/// <see cref="CredentialUnreadableException"/>. Every write raises the tenant's
/// <see cref="IConnectionChangeHandler"/>s with the connection (or app credentials) key.
/// </summary>
public sealed class CredentialStore(IDocumentManager<ConnectorCredentialsDocument> documents, IDataProtectionProvider dataProtection, IServiceProvider? services = null) : ICredentialStore, IAppCredentialsStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private IDataProtector Protector => dataProtection.CreateProtector(ConnectorsConstants.CredentialsProtectionPurpose);

    public async Task<IReadOnlyList<CredentialInfo>> ListAsync(string? connectionKey = null, CredentialOwner? owner = null, CancellationToken cancellationToken = default)
    {
        var document = await documents.GetOrCreateImmutableAsync();
        return document.Credentials.Values
            .Where(c => connectionKey is null || string.Equals(c.ConnectionKey, connectionKey, StringComparison.OrdinalIgnoreCase))
            .Where(c => owner is null || c.Owner == owner.Value)
            .Select(Info)
            .OrderBy(c => c.ConnectionKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<CredentialInfo?> FindAsync(string connectionKey, CredentialOwner owner, CancellationToken cancellationToken = default)
    {
        var document = await documents.GetOrCreateImmutableAsync();
        return document.Credentials.TryGetValue(ConnectorCredentialsDocument.KeyOf(connectionKey, owner), out var stored) ? Info(stored) : null;
    }

    public async Task<ConnectionCredential?> RevealAsync(string connectionKey, CredentialOwner owner, CancellationToken cancellationToken = default)
    {
        var document = await documents.GetOrCreateImmutableAsync();
        if (!document.Credentials.TryGetValue(ConnectorCredentialsDocument.KeyOf(connectionKey, owner), out var stored))
        {
            return null;
        }

        try
        {
            return new ConnectionCredential
            {
                Values = OpenValues(stored.ProtectedValues),
                Tokens = string.IsNullOrEmpty(stored.ProtectedTokens) ? null : JsonSerializer.Deserialize<OAuthTokenSet>(Protector.Unprotect(stored.ProtectedTokens), Json),
            };
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            throw new CredentialUnreadableException(connectionKey, owner, ex);
        }
    }

    public async Task<CredentialInfo> SaveAsync(string connectionKey, CredentialOwner owner, ConnectionCredential credential, string? label = null, bool merge = true, CancellationToken cancellationToken = default)
    {
        var document = await documents.GetOrCreateMutableAsync();
        var key = ConnectorCredentialsDocument.KeyOf(connectionKey, owner);
        var now = DateTime.UtcNow;
        if (!document.Credentials.TryGetValue(key, out var stored))
        {
            stored = new StoredCredential { ConnectionKey = connectionKey, Scope = owner.Scope.Value, OwnerId = owner.OwnerId, CreatedUtc = now };
            document.Credentials[key] = stored;
            merge = false;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (merge && !string.IsNullOrEmpty(stored.ProtectedValues))
        {
            try
            {
                foreach (var (name, value) in OpenValues(stored.ProtectedValues))
                {
                    values[name] = value;
                }
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException)
            {
                // The old values no longer open: the new ones replace them.
            }
        }

        foreach (var (name, value) in credential.Values)
        {
            if (string.IsNullOrEmpty(value))
            {
                if (!merge)
                {
                    values.Remove(name);
                }

                continue;
            }

            values[name] = value;
        }

        stored.ProtectedValues = values.Count == 0 ? null : Protector.Protect(JsonSerializer.Serialize(values, Json));
        stored.ValueNames = values.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (credential.Tokens is { } tokens)
        {
            Seal(stored, tokens);
        }
        else if (!merge)
        {
            stored.ProtectedTokens = null;
            stored.ExpiresUtc = null;
            stored.GrantedScopes = [];
        }

        if (label is not null)
        {
            stored.Label = label.Length == 0 ? null : label;
        }

        stored.UpdatedUtc = now;
        await documents.UpdateAsync(document);
        await ChangedAsync(connectionKey, cancellationToken);
        return Info(stored);
    }

    public async Task StoreTokensAsync(string connectionKey, CredentialOwner owner, OAuthTokenSet tokens, CancellationToken cancellationToken = default)
    {
        var document = await documents.GetOrCreateMutableAsync();
        var key = ConnectorCredentialsDocument.KeyOf(connectionKey, owner);
        var now = DateTime.UtcNow;
        if (!document.Credentials.TryGetValue(key, out var stored))
        {
            stored = new StoredCredential { ConnectionKey = connectionKey, Scope = owner.Scope.Value, OwnerId = owner.OwnerId, CreatedUtc = now };
            document.Credentials[key] = stored;
        }

        Seal(stored, tokens);
        stored.UpdatedUtc = now;
        await documents.UpdateAsync(document);
        await ChangedAsync(connectionKey, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string connectionKey, CredentialOwner owner, CancellationToken cancellationToken = default)
    {
        var document = await documents.GetOrCreateMutableAsync();
        if (!document.Credentials.Remove(ConnectorCredentialsDocument.KeyOf(connectionKey, owner)))
        {
            return false;
        }

        await documents.UpdateAsync(document);
        await ChangedAsync(connectionKey, cancellationToken);
        return true;
    }

    /// <summary>Deletes every credential of a connection (the connection was deleted).</summary>
    public async Task DeleteAllAsync(string connectionKey)
    {
        var document = await documents.GetOrCreateMutableAsync();
        var keys = document.Credentials.Where(c => string.Equals(c.Value.ConnectionKey, connectionKey, StringComparison.OrdinalIgnoreCase)).Select(c => c.Key).ToList();
        if (keys.Count == 0)
        {
            return;
        }

        foreach (var key in keys)
        {
            document.Credentials.Remove(key);
        }

        await documents.UpdateAsync(document);
    }

    // App credentials: a vendor's client id and secret, shared by its sign-in provider and its connection.

    public async Task<IReadOnlyList<(string Key, string ClientId, bool HasSecret, DateTime UpdatedUtc)>> ListAppCredentialsAsync()
    {
        var document = await documents.GetOrCreateImmutableAsync();
        return document.AppCredentials.Values.OrderBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
            .Select(a => (a.Key, a.ClientId, !string.IsNullOrEmpty(a.ProtectedSecret), a.UpdatedUtc)).ToArray();
    }

    public async Task<AppCredentials?> FindAppCredentialsAsync(string key)
    {
        var document = await documents.GetOrCreateImmutableAsync();
        if (!document.AppCredentials.TryGetValue(key, out var stored))
        {
            return null;
        }

        try
        {
            return new AppCredentials(stored.ClientId, string.IsNullOrEmpty(stored.ProtectedSecret) ? null : Protector.Unprotect(stored.ProtectedSecret));
        }
        catch (CryptographicException ex)
        {
            throw new CredentialUnreadableException(key, CredentialOwner.Tenant, ex);
        }
    }

    /// <summary>Stores app credentials; a null secret keeps the stored one, an empty one clears it.</summary>
    public async Task SaveAppCredentialsAsync(string key, string clientId, string? clientSecret)
    {
        var document = await documents.GetOrCreateMutableAsync();
        if (!document.AppCredentials.TryGetValue(key, out var stored))
        {
            stored = new StoredAppCredentials { Key = key };
            document.AppCredentials[key] = stored;
        }

        stored.ClientId = clientId;
        if (clientSecret is not null)
        {
            stored.ProtectedSecret = clientSecret.Length == 0 ? null : Protector.Protect(clientSecret);
        }

        stored.UpdatedUtc = DateTime.UtcNow;
        await documents.UpdateAsync(document);
        await ChangedAsync(key, CancellationToken.None);
    }

    public async Task<bool> DeleteAppCredentialsAsync(string key)
    {
        var document = await documents.GetOrCreateMutableAsync();
        if (!document.AppCredentials.Remove(key))
        {
            return false;
        }

        await documents.UpdateAsync(document);
        await ChangedAsync(key, CancellationToken.None);
        return true;
    }

    Task IAppCredentialsStore.SaveAsync(string key, AppCredentials credentials, CancellationToken cancellationToken) =>
        SaveAppCredentialsAsync(key, credentials.ClientId, credentials.ClientSecret);

    Task<bool> IAppCredentialsStore.DeleteAsync(string key, CancellationToken cancellationToken) => DeleteAppCredentialsAsync(key);

    /// <summary>Tells the change handlers (sign-in handlers release the shell on it). Resolved late: the registry reads this store.</summary>
    private async Task ChangedAsync(string key, CancellationToken cancellationToken)
    {
        if (services is null)
        {
            return;
        }

        var handlers = services.GetServices<IConnectionChangeHandler>().ToArray();
        if (handlers.Length == 0)
        {
            return;
        }

        var definition = await services.GetRequiredService<IConnectionRegistry>().FindAsync(key, cancellationToken);
        foreach (var handler in handlers)
        {
            await handler.ConnectionChangedAsync(key, definition, cancellationToken);
        }
    }

    private void Seal(StoredCredential stored, OAuthTokenSet tokens)
    {
        stored.ProtectedTokens = Protector.Protect(JsonSerializer.Serialize(tokens, Json));
        stored.ExpiresUtc = tokens.ExpiresUtc;
        stored.GrantedScopes = tokens.Scopes?.ToList() ?? stored.GrantedScopes;
    }

    private Dictionary<string, string> OpenValues(string? protectedValues) =>
        string.IsNullOrEmpty(protectedValues)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(JsonSerializer.Deserialize<Dictionary<string, string>>(Protector.Unprotect(protectedValues), Json) ?? [], StringComparer.OrdinalIgnoreCase);

    private CredentialInfo Info(StoredCredential stored)
    {
        var status = CredentialStatus.Ready;
        try
        {
            if (!string.IsNullOrEmpty(stored.ProtectedValues))
            {
                Protector.Unprotect(stored.ProtectedValues);
            }
        }
        catch (CryptographicException)
        {
            status = CredentialStatus.Unreadable;
        }

        return new CredentialInfo
        {
            ConnectionKey = stored.ConnectionKey,
            Owner = stored.Owner,
            Label = stored.Label,
            AccountName = stored.AccountName,
            ValueNames = stored.ValueNames,
            GrantedScopes = stored.GrantedScopes,
            CreatedUtc = stored.CreatedUtc,
            UpdatedUtc = stored.UpdatedUtc,
            ExpiresUtc = stored.ExpiresUtc,
            Status = status,
        };
    }
}
