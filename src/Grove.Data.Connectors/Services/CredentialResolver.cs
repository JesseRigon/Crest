using Grove.Access;

namespace Grove.Data.Connectors.Services;

/// <summary>The tenant scope: one credential per connection, managed with <c>ManageConnections</c>; any caller that may call uses it.</summary>
public sealed class TenantScopeHandler : ICredentialScopeHandler
{
    public CredentialScope Scope => CredentialScope.Tenant;

    public CredentialOwner? OwnerFor(CallerContext caller) => ConnectorCallers.MayCall(caller) ? CredentialOwner.Tenant : null;

    public bool CanManage(CallerContext caller, CredentialOwner owner) =>
        owner.Scope == CredentialScope.Tenant && ConnectorCallers.IsStaffUser(caller) && Manages(caller);

    internal static bool Manages(CallerContext caller) =>
        caller.IsSuperUser || caller.Permissions.Contains(ConnectorsPermissions.ManageConnections.Name);
}

/// <summary>
/// The user scope: a staff user's own token or OAuth grant. Only an authenticated admin-shell
/// caller has an owner in it; a member, a site visitor, an application or a system actor never
/// does. The user manages their own; a connection manager may revoke anyone's (never read it).
/// </summary>
public sealed class UserScopeHandler : ICredentialScopeHandler
{
    public CredentialScope Scope => CredentialScope.User;

    public CredentialOwner? OwnerFor(CallerContext caller) =>
        ConnectorCallers.IsStaffUser(caller) && caller.UserId is { } userId ? CredentialOwner.ForUser(userId) : null;

    public bool CanManage(CallerContext caller, CredentialOwner owner)
    {
        if (owner.Scope != CredentialScope.User || !ConnectorCallers.IsStaffUser(caller))
        {
            return false;
        }

        if (string.Equals(owner.OwnerId, caller.UserId, StringComparison.Ordinal))
        {
            return caller.IsSuperUser
                || caller.Permissions.Contains(ConnectorsPermissions.ConnectOwnAccounts.Name)
                || caller.Permissions.Contains(ConnectorsPermissions.ManageConnections.Name);
        }

        return TenantScopeHandler.Manages(caller);
    }
}

/// <summary>
/// Picks the credential a call runs with: the requested scope (or the connection's default),
/// the caller's owner in it, the stored credential; a per-user call falls back to the tenant's
/// credential only when the connection's policy allows it.
/// </summary>
public sealed class CredentialResolver(ICredentialStore store, IEnumerable<ICredentialScopeHandler> handlers) : ICredentialResolver
{
    public async Task<CredentialResolution> ResolveAsync(ConnectionDefinition connection, CallerContext caller, CredentialScope? scope = null, CancellationToken cancellationToken = default)
    {
        if (!ConnectorCallers.MayCall(caller))
        {
            return CredentialResolution.Failed(CredentialResolutionFailure.NotStaff, "Connections are used from the admin side only.");
        }

        if (connection.Auth.Scheme == AuthSchemes.None)
        {
            return CredentialResolution.Anonymous;
        }

        var requested = scope ?? connection.AllowedScopes.FirstOrDefault(CredentialScope.Tenant);
        if (!connection.Allows(requested))
        {
            return CredentialResolution.Failed(CredentialResolutionFailure.ScopeNotAllowed, $"Connection '{connection.Key}' does not allow {requested.Value} credentials.");
        }

        var handler = handlers.FirstOrDefault(h => h.Scope == requested);
        if (handler is null)
        {
            return CredentialResolution.Failed(CredentialResolutionFailure.ScopeNotAllowed, $"No handler for the {requested.Value} scope.");
        }

        var owner = handler.OwnerFor(caller);
        if (owner is { } found)
        {
            var resolved = await RevealAsync(connection, found, cancellationToken);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        if (requested != CredentialScope.Tenant && connection.Policy.AllowTenantFallback && connection.Allows(CredentialScope.Tenant))
        {
            var fallback = await RevealAsync(connection, CredentialOwner.Tenant, cancellationToken);
            if (fallback is not null)
            {
                return fallback with { FellBack = fallback.Succeeded };
            }
        }

        if (owner is null)
        {
            return CredentialResolution.Failed(CredentialResolutionFailure.NotStaff, $"Connection '{connection.Key}' needs a {requested.Value} credential, which this caller cannot hold.");
        }

        return CredentialResolution.Failed(CredentialResolutionFailure.NotConnected, requested == CredentialScope.User
            ? $"Connect your account to '{connection.DisplayName}' first (Connected accounts)."
            : $"Connection '{connection.Key}' has no {requested.Value} credential.");
    }

    private async Task<CredentialResolution?> RevealAsync(ConnectionDefinition connection, CredentialOwner owner, CancellationToken cancellationToken)
    {
        try
        {
            var credential = await store.RevealAsync(connection.Key, owner, cancellationToken);
            return credential is null ? null : new CredentialResolution { Owner = owner, Credential = credential };
        }
        catch (CredentialUnreadableException ex)
        {
            return CredentialResolution.Failed(CredentialResolutionFailure.Unreadable, ex.Message);
        }
    }
}

/// <summary>
/// App credentials by key: a stored set (managed on the Connections screen), else the tenant
/// credential of the connection with that key (its client id and secret).
/// </summary>
public sealed class AppCredentialsProvider(CredentialStore store, IConnectionRegistry registry) : IAppCredentialsProvider
{
    public async Task<AppCredentials?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (await store.FindAppCredentialsAsync(key) is { } stored)
        {
            return stored;
        }

        if (await registry.FindAsync(key, cancellationToken) is not { } connection)
        {
            return null;
        }

        var credential = await store.RevealAsync(connection.Key, CredentialOwner.Tenant, cancellationToken);
        return credential?[CredentialKeys.ClientId] is { } clientId ? new AppCredentials(clientId, credential[CredentialKeys.ClientSecret]) : null;
    }

    /// <summary>The app credentials an OAuth connection authenticates its client with.</summary>
    public async Task<AppCredentials?> ForConnectionAsync(ConnectionDefinition connection, CancellationToken cancellationToken = default)
    {
        if (connection.Auth.AppCredentialsKey is { } key && await GetAsync(key, cancellationToken) is { } shared)
        {
            return shared;
        }

        var credential = await store.RevealAsync(connection.Key, CredentialOwner.Tenant, cancellationToken);
        if (credential?[CredentialKeys.ClientId] is { } clientId)
        {
            return new AppCredentials(clientId, credential[CredentialKeys.ClientSecret]);
        }

        return connection.Auth.Settings.TryGetValue(CredentialKeys.ClientId, out var publicClient) && !string.IsNullOrEmpty(publicClient)
            ? new AppCredentials(publicClient, null)
            : null;
    }
}
