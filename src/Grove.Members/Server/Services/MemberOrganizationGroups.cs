using Grove.Access;
using Grove.Members.Constants;
using Grove.Members.Models;
using Grove.Users.Models;

namespace Grove.Members.Services;

/// <summary>
/// A member binding's roles come from the organization's groups (docs/permissions.md, the
/// permission model; access.md § Members ruling 2026-10-13): the binding keeps the class and
/// the membership, the organization's groups say what the member holds there. Placing a
/// member under roles puts them in the organization's group for each role, created from the
/// role when the organization has none.
/// </summary>
public sealed class MemberOrganizationGroups(IAccessGroupService groups)
{
    public async Task PlaceAsync(string organizationId, string userId, IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        var reach = AccessReach.Organization(organizationId);
        var existing = await groups.ListAsync(reach, cancellationToken);
        foreach (var role in roles.Where(role => !string.IsNullOrWhiteSpace(role)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var group = existing.FirstOrDefault(candidate => candidate.Roles.Count == 1 && string.Equals(candidate.Roles[0], role, StringComparison.OrdinalIgnoreCase))
                ?? await groups.CreateAsync(reach, new AccessGroupWrite(role, null, [role]), cancellationToken);
            await groups.AddUserAsync(group.Id, userId, cancellationToken);
        }
    }

    /// <summary>Removes the user from every group of the organization.</summary>
    public Task LeaveAsync(string organizationId, string userId, CancellationToken cancellationToken = default) =>
        groups.RemoveUserFromReachAsync(userId, AccessReach.Organization(organizationId), cancellationToken);

    /// <summary>
    /// The migration: every member binding's stored roles become memberships of the
    /// organization's group for each role. Idempotent.
    /// </summary>
    public async Task<int> MigrateBindingRolesAsync(IEnumerable<User> users, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var user in users)
        {
            foreach (var binding in UserClassSet.Bindings(user, UserClasses.Member))
            {
                if (binding.Roles.Count > 0)
                {
                    await PlaceAsync(binding.OrganizationId, user.UserId, binding.Roles, cancellationToken);
                    count++;
                }
            }
        }

        return count;
    }
}
