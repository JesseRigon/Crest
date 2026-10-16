using System.Text.Json.Serialization;
using Grove.Access;

namespace Grove.Data.Connectors;

/// <summary>
/// Whose a credential is. An open value, not an enum, so a scope can be added (an
/// organization's own credentials) without breaking the contract: each scope has an
/// <see cref="ICredentialScopeHandler"/> that names the owner for a caller.
/// </summary>
[JsonConverter(typeof(CredentialScopeJsonConverter))]
public readonly record struct CredentialScope
{
    /// <summary>A service credential shared by the tenant, managed with <c>ManageConnections</c>.</summary>
    public static readonly CredentialScope Tenant = new("tenant");

    /// <summary>A staff user's own token or OAuth grant, managed on their Connected accounts page.</summary>
    public static readonly CredentialScope User = new("user");

    public CredentialScope(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;

    public static bool TryParse(string? value, out CredentialScope scope)
    {
        scope = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        scope = new CredentialScope(value);
        return true;
    }
}

public sealed class CredentialScopeJsonConverter : JsonConverter<CredentialScope>
{
    public override CredentialScope Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options) =>
        new(reader.GetString() ?? throw new System.Text.Json.JsonException("A credential scope is required."));

    public override void Write(System.Text.Json.Utf8JsonWriter writer, CredentialScope value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}

/// <summary>The owner of a credential: a scope and the owner's id within it (null for the tenant scope).</summary>
public readonly record struct CredentialOwner(CredentialScope Scope, string? OwnerId)
{
    public static readonly CredentialOwner Tenant = new(CredentialScope.Tenant, null);

    public static CredentialOwner ForUser(string userId) => new(CredentialScope.User, userId);

    public override string ToString() => OwnerId is null ? Scope.Value : $"{Scope.Value}:{OwnerId}";
}

/// <summary>
/// Who may hold and use credentials. Connectors apply to staff only: callers in the admin
/// shell, system actors, and machine identities (an application, a connection's webhook
/// caller), which use tenant credentials. A member-shell or anonymous site caller never
/// resolves a credential, and the connection and credential APIs refuse it.
/// </summary>
public static class ConnectorCallers
{
    /// <summary>True for an authenticated admin-shell user.</summary>
    public static bool IsStaffUser(CallerContext caller) =>
        caller.Side == CallerSide.Admin && caller.IsAuthenticated && !caller.IsSystem
        && !string.Equals(caller.UserClass, CallerClasses.Application, StringComparison.Ordinal)
        && !string.Equals(caller.UserClass, CallerClasses.Member, StringComparison.Ordinal);

    /// <summary>True for a machine identity on the staff side (an application, a connection's caller).</summary>
    public static bool IsApplication(CallerContext caller) =>
        caller.Side != CallerSide.Member && caller.IsAuthenticated
        && string.Equals(caller.UserClass, CallerClasses.Application, StringComparison.Ordinal);

    /// <summary>True for a caller that may run connector calls at all: a staff user, a system actor or an application.</summary>
    public static bool MayCall(CallerContext caller) =>
        caller.Side != CallerSide.Member && (caller.IsSystem || caller.Side == CallerSide.System || IsStaffUser(caller) || IsApplication(caller));
}

/// <summary>
/// One credential scope's rules: which owner a caller acts as in it, and who may manage it.
/// The engine ships tenant and user; another scope is added by registering a handler.
/// </summary>
public interface ICredentialScopeHandler
{
    CredentialScope Scope { get; }

    /// <summary>The owner the caller's own credential in this scope is stored under, or null when the caller holds none in it (a member user in the user scope).</summary>
    CredentialOwner? OwnerFor(CallerContext caller);

    /// <summary>Whether the caller may create, replace or revoke credentials of <paramref name="owner"/>.</summary>
    bool CanManage(CallerContext caller, CredentialOwner owner);
}

/// <summary>
/// The secret material of one credential, in process only: never serialized to a client, a
/// definition, an export or a log. Values are free-form per scheme (<see cref="CredentialKeys"/>).
/// </summary>
public sealed class ConnectionCredential
{
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>OAuth tokens held by an authorization-code or device-code grant.</summary>
    public OAuthTokenSet? Tokens { get; init; }

    public string? this[string key] => Values.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    public override string ToString() => "[credential]";
}

/// <summary>Well-known credential value keys.</summary>
public static class CredentialKeys
{
    public const string ApiKey = "apiKey";
    public const string Username = "username";
    public const string Password = "password";
    public const string Token = "token";
    public const string ClientId = "clientId";
    public const string ClientSecret = "clientSecret";
    public const string SigningSecret = "signingSecret";
    public const string ConnectionString = "connectionString";
}

public enum CredentialStatus
{
    /// <summary>Usable.</summary>
    Ready,

    /// <summary>An OAuth grant whose access token expired and could not be refreshed.</summary>
    Expired,

    /// <summary>Stored, but the tenant's data protection can no longer open it (keys rotated or lost). Re-enter it.</summary>
    Unreadable,
}

/// <summary>What anyone may see about a credential: never the secret.</summary>
public sealed record CredentialInfo
{
    public required string ConnectionKey { get; init; }

    public required CredentialOwner Owner { get; init; }

    public string? Label { get; init; }

    /// <summary>The account the credential acts as, when known (an OAuth grant's user name).</summary>
    public string? AccountName { get; init; }

    /// <summary>Which values are set (names only), so a form can show "set" without the value.</summary>
    public IReadOnlyList<string> ValueNames { get; init; } = [];

    public IReadOnlyList<string> GrantedScopes { get; init; } = [];

    public DateTime CreatedUtc { get; init; }

    public DateTime UpdatedUtc { get; init; }

    public DateTime? ExpiresUtc { get; init; }

    public CredentialStatus Status { get; init; }
}

/// <summary>A sealed credential could not be opened: an explicit error, never an anonymous call.</summary>
public sealed class CredentialUnreadableException(string connectionKey, CredentialOwner owner, Exception? inner = null)
    : Exception($"The {owner.Scope.Value} credential of connection '{connectionKey}' cannot be decrypted; enter it again.", inner)
{
    public string ConnectionKey { get; } = connectionKey;

    public CredentialOwner Owner { get; } = owner;
}

/// <summary>
/// The sealed credential store: values sealed with the tenant's data protection, one entry per
/// connection and owner. Only the engine and protocol seams read <see cref="RevealAsync"/>.
/// </summary>
public interface ICredentialStore
{
    Task<IReadOnlyList<CredentialInfo>> ListAsync(string? connectionKey = null, CredentialOwner? owner = null, CancellationToken cancellationToken = default);

    Task<CredentialInfo?> FindAsync(string connectionKey, CredentialOwner owner, CancellationToken cancellationToken = default);

    /// <summary>Opens the credential; null when none is stored. Throws <see cref="CredentialUnreadableException"/> when it cannot be decrypted.</summary>
    Task<ConnectionCredential?> RevealAsync(string connectionKey, CredentialOwner owner, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores or replaces the credential. Values absent from <paramref name="credential"/> are
    /// kept when <paramref name="merge"/> is true (a form that leaves a secret blank keeps it).
    /// </summary>
    Task<CredentialInfo> SaveAsync(string connectionKey, CredentialOwner owner, ConnectionCredential credential, string? label = null, bool merge = true, CancellationToken cancellationToken = default);

    /// <summary>Replaces only the OAuth tokens (after an exchange or a refresh).</summary>
    Task StoreTokensAsync(string connectionKey, CredentialOwner owner, OAuthTokenSet tokens, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string connectionKey, CredentialOwner owner, CancellationToken cancellationToken = default);
}

public enum CredentialResolutionFailure
{
    None,

    /// <summary>The caller may not use connectors (a member-shell or site caller).</summary>
    NotStaff,

    /// <summary>The requested scope is not one the connection allows.</summary>
    ScopeNotAllowed,

    /// <summary>No credential is stored for the caller (and no fallback applies): the user must connect their account.</summary>
    NotConnected,

    Unreadable,
}

/// <summary>The credential a call runs with, or why there is none.</summary>
public sealed record CredentialResolution
{
    public CredentialOwner? Owner { get; init; }

    public ConnectionCredential? Credential { get; init; }

    public CredentialResolutionFailure Failure { get; init; }

    public string? Error { get; init; }

    /// <summary>True when a per-user call fell back to the tenant credential (the policy allowed it).</summary>
    public bool FellBack { get; init; }

    public bool Succeeded => Failure == CredentialResolutionFailure.None;

    /// <summary>A connection with auth scheme none resolves to no credential, successfully.</summary>
    public static CredentialResolution Anonymous { get; } = new();

    public static CredentialResolution Failed(CredentialResolutionFailure failure, string error) => new() { Failure = failure, Error = error };
}

/// <summary>
/// Picks the credential a caller's call runs with: the requested scope (or the connection's
/// first allowed), the caller's owner in it (never a member user's), and the tenant fallback
/// only when the connection's policy allows it.
/// </summary>
public interface ICredentialResolver
{
    Task<CredentialResolution> ResolveAsync(ConnectionDefinition connection, CallerContext caller, CredentialScope? scope = null, CancellationToken cancellationToken = default);
}

/// <summary>A vendor's app credentials, shared by its sign-in provider and its connection (tenant scope).</summary>
public sealed record AppCredentials(string ClientId, string? ClientSecret);

/// <summary>
/// Reads app credentials by key (<see cref="ConnectionAuth.AppCredentialsKey"/>, or a
/// connection key) for in-process consumers such as a sign-in handler. Never exposed by an API.
/// </summary>
public interface IAppCredentialsProvider
{
    Task<AppCredentials?> GetAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes app credentials (tenant scope, sealed). An integration may instead store a client id
/// and secret as the tenant credential of the connection with that key; the provider reads
/// either. Every write raises <see cref="IConnectionChangeHandler"/> with the key.
/// </summary>
public interface IAppCredentialsStore
{
    /// <summary>Stores the set; a null secret keeps the stored one, an empty one clears it.</summary>
    Task SaveAsync(string key, AppCredentials credentials, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>A module's settings that name the connection it sends through (a provider's connection key).</summary>
public class ConnectionSettingOptions
{
    /// <summary>The key of the connection to use; null or empty when none is chosen.</summary>
    public string? ConnectionKey { get; set; }
}
