using System.Security.Claims;
using Crest.Access;
using Crest.Roles;
using Crest.Security;
using Crest.Security.Permissions;
using Microsoft.AspNetCore.Identity;

namespace Crest.Workflows.Connectors;

/// <summary>
/// A connection's machine identity (docs/access.md, Anonymous actors, ruled 2026-10-11): an
/// external system calling a connection's webhook is authenticated by its signature, and the
/// run acts as the connection's application caller with the roles assigned on the connection.
/// The identity is the connection's key; the roles are read from the connection on every build.
/// </summary>
public static class WorkflowConnectionCaller
{
    /// <summary>The authentication type of a connection's principal: not an <c>Identity.</c> one, so the factory reads it as an application.</summary>
    public const string AuthenticationType = "WorkflowConnection";

    private const string Prefix = "connection:";

    /// <summary>The name identifier a connection's caller carries: never a user id.</summary>
    public static string NameIdentifierFor(string connectionKey) => Prefix + connectionKey;

    /// <summary>The connection key of a caller id, or null when the id is not a connection's.</summary>
    public static string? ConnectionKeyOf(string? userId) =>
        userId is not null && userId.StartsWith(Prefix, StringComparison.Ordinal) && userId.Length > Prefix.Length ? userId[Prefix.Length..] : null;

    /// <summary>The identity the gate builds a connection's caller from.</summary>
    public static ClaimsPrincipal Principal(WorkflowConnection connection) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, NameIdentifierFor(connection.Key)),
        new Claim(ClaimTypes.Name, connection.DisplayName.Length > 0 ? connection.DisplayName : connection.Key),
    ], AuthenticationType, ClaimTypes.Name, ClaimTypes.Role));
}

/// <summary>
/// Gives a connection's caller its rights: the application class, the roles assigned on the
/// connection and their permissions, the super user only when those roles include the admin
/// role. Contributed for every build of a connection's id, the first burst of a webhook's run
/// and every later one alike, so a role removed from the connection fails the next burst.
/// A connection that no longer exists keeps nothing.
/// </summary>
public sealed class WorkflowConnectionCallerContributor(
    WorkflowConnectionService connections,
    RoleManager<IRole> roleManager,
    ISystemRoleProvider systemRoles) : ICallerContextContributor
{
    public async Task ContributeAsync(CallerContextBuilder builder, CancellationToken cancellationToken = default)
    {
        if (WorkflowConnectionCaller.ConnectionKeyOf(builder.UserId) is not { } key)
        {
            return;
        }

        builder.UserClass = CallerClasses.Application;
        builder.IsSuperUser = false;
        builder.Roles.Clear();
        builder.Permissions.Clear();

        var connection = await connections.FindAsync(key);
        if (connection is null)
        {
            return;
        }

        var names = new List<string> { PlatformConstants.Roles.Anonymous, PlatformConstants.Roles.Authenticated };
        names.AddRange(connection.Roles);
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
