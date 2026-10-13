using System.Security.Claims;

namespace Crest.Security.Services;

public interface IRoleService
{
    Task<IEnumerable<IRole>> GetRolesAsync();

    Task<IEnumerable<Claim>> GetRoleClaimsAsync(string role, CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GetNormalizedRoleNamesAsync();

    ValueTask<bool> IsAdminRoleAsync(string role)
        => ValueTask.FromResult(false);

    ValueTask<bool> IsSystemRoleAsync(string role)
        => ValueTask.FromResult(false);

    /// <summary>A role computed from a relationship (<c>Crest.Roles.RoleKind.Derived</c>,
    /// docs/sharing.md requirement 45): never assignable, never stored on a user.</summary>
    ValueTask<bool> IsDerivedRoleAsync(string role)
        => ValueTask.FromResult(false);
}
