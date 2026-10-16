using System.Security.Claims;
using System.Text;
using Grove.Access;
using Grove.Data.Protocols.Webhooks;
using Grove.Security;
using Grove.Security.Permissions;
using Microsoft.AspNetCore.Identity;

namespace Grove.Data.Connectors.Services;

/// <summary>
/// Verifies an inbound webhook for a connection: the connection must be an hmac one, its
/// signing secret (tenant credential) is read here and handed to the signature scheme; the
/// caller only learns whether the signature holds.
/// </summary>
public sealed class WebhookVerifier(IConnectionRegistry registry, ICredentialStore store, IEnumerable<IWebhookSignatureScheme> schemes) : IWebhookVerifier
{
    public async Task<WebhookVerification> VerifyAsync(string connectionKey, WebhookRequest request, CancellationToken cancellationToken = default)
    {
        var connection = await registry.FindAsync(connectionKey, cancellationToken);
        if (connection is null)
        {
            return new WebhookVerification(WebhookVerificationStatus.UnknownConnection, Error: $"No connection '{connectionKey}'.");
        }

        if (connection.Auth.Scheme != AuthSchemes.Hmac)
        {
            return new WebhookVerification(WebhookVerificationStatus.NotInbound, connection, $"Connection '{connectionKey}' does not receive signed webhooks.");
        }

        ConnectionCredential? credential;
        try
        {
            credential = await store.RevealAsync(connection.Key, CredentialOwner.Tenant, cancellationToken);
        }
        catch (CredentialUnreadableException ex)
        {
            return new WebhookVerification(WebhookVerificationStatus.Rejected, connection, ex.Message);
        }

        var secret = credential?[CredentialKeys.SigningSecret] ?? credential?[CredentialKeys.Token] ?? credential?[CredentialKeys.ApiKey];
        if (string.IsNullOrEmpty(secret))
        {
            return new WebhookVerification(WebhookVerificationStatus.Rejected, connection, "The connection has no signing secret.");
        }

        var schemeId = connection.Auth.SignatureScheme ?? HmacSha256HexScheme.SchemeId;
        var scheme = schemes.FirstOrDefault(s => string.Equals(s.Id, schemeId, StringComparison.OrdinalIgnoreCase));
        if (scheme is null)
        {
            return new WebhookVerification(WebhookVerificationStatus.Rejected, connection, $"No signature scheme '{schemeId}'.");
        }

        return scheme.Verify(Encoding.UTF8.GetBytes(secret), request, connection.Auth.SignatureHeader)
            ? new WebhookVerification(WebhookVerificationStatus.Verified, connection)
            : new WebhookVerification(WebhookVerificationStatus.Rejected, connection, "Missing or invalid signature.");
    }
}

/// <summary>
/// A connection's machine identity (docs/access.md › Anonymous actors): an external system
/// calling a connection's webhook is authenticated by its signature, and what it starts acts
/// as the connection's application caller, with the roles assigned on the connection.
/// </summary>
public static class ConnectionCaller
{
    /// <summary>The authentication type of a connection's principal: not an <c>Identity.</c> one, so the factory reads it as an application.</summary>
    public const string AuthenticationType = "Connection";

    private const string Prefix = "connection:";

    public static string NameIdentifierFor(string connectionKey) => Prefix + connectionKey;

    public static string? ConnectionKeyOf(string? userId) =>
        userId is not null && userId.StartsWith(Prefix, StringComparison.Ordinal) && userId.Length > Prefix.Length ? userId[Prefix.Length..] : null;

    public static ClaimsPrincipal Principal(ConnectionDefinition connection) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, NameIdentifierFor(connection.Key)),
        new Claim(ClaimTypes.Name, connection.DisplayName.Length > 0 ? connection.DisplayName : connection.Key),
    ], AuthenticationType, ClaimTypes.Name, ClaimTypes.Role));
}

/// <summary>
/// Gives a connection's caller its rights on every build: the application class, the
/// connection's roles and their permissions (the super user only through the admin role).
/// A connection that no longer exists keeps nothing.
/// </summary>
public sealed class ConnectionCallerContributor(IConnectionRegistry registry, RoleManager<IRole> roleManager, Grove.Access.Roles.ISystemRoleProvider systemRoles) : ICallerContextContributor
{
    public async Task ContributeAsync(CallerContextBuilder builder, CancellationToken cancellationToken = default)
    {
        if (ConnectionCaller.ConnectionKeyOf(builder.UserId) is not { } key)
        {
            return;
        }

        builder.UserClass = CallerClasses.Application;
        builder.IsSuperUser = false;
        builder.Roles.Clear();
        builder.Permissions.Clear();

        var connection = await registry.FindAsync(key, cancellationToken);
        if (connection is null)
        {
            return;
        }

        var names = new List<string> { PlatformConstants.Roles.Anonymous, PlatformConstants.Roles.Authenticated };
        names.AddRange(connection.CallerRoles);
        foreach (var roleName in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            if (!string.Equals(roleName, PlatformConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(roleName, PlatformConstants.Roles.Authenticated, StringComparison.OrdinalIgnoreCase))
            {
                builder.Roles.Add(roleName);
            }

            foreach (var claim in await roleManager.GetClaimsAsync(role))
            {
                if (string.Equals(claim.Type, Permission.ClaimType, StringComparison.OrdinalIgnoreCase))
                {
                    builder.Permissions.Add(claim.Value);
                }
            }
        }

        builder.IsSuperUser = builder.Roles.Contains(systemRoles.GetAdminRole().RoleName);
    }
}
