using Crest.Members.Models;
using Crest.Users;

namespace Crest.Members.Services;

/// <summary>What a member-creation request carries. Password null = external-login or
/// invitation flows set credentials later.</summary>
public sealed record CreateMemberRequest(
    string UserName,
    string? Email,
    string? Password,
    string? PersonId,
    string OrganizationId,
    IReadOnlyList<string>? Roles);

/// <summary>
/// Member accounts and org bindings (design: docs/members.md). All writes keep
/// the three stores in step: the user aspect (+ its index rows via re-save), and the
/// hierarchy tree (one node per binding, under the org's root).
/// </summary>
public interface IMemberService
{
    Task<MemberModel?> GetAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemberModel>> ListByOrganizationAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>Creates a member-class user with its first org binding. The FIRST
    /// member of an organization becomes its member admin (ruling 2026-09-09). Errors
    /// are reported through <paramref name="reportError"/> (identity-validation shape,
    /// mirroring IUserService.CreateUserAsync).</summary>
    Task<IUser?> CreateMemberAsync(CreateMemberRequest request, Action<string, string> reportError, CancellationToken cancellationToken = default);

    /// <summary>Adds an org binding to an existing member (first member of THAT org
    /// still becomes its member admin). Refuses staff-class users.</summary>
    Task<MemberOrgBindingModel> AddBindingAsync(string userId, string organizationId, IReadOnlyList<string>? roles, CancellationToken cancellationToken = default);

    /// <summary>Removes a binding and its hierarchy node (node must be re-parented
    /// first if it has children - the hierarchy service enforces that).</summary>
    Task RemoveBindingAsync(string userId, string organizationId, CancellationToken cancellationToken = default);
}
