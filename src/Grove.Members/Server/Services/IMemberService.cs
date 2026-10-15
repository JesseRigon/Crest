using Grove.Members.Models;
using Grove.Users;

namespace Grove.Members.Services;

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
/// Member accounts (design: docs/members.md). Reads go through the connection index;
/// every binding write goes through <see cref="MemberBindingService"/>, the binding
/// record's one write path.
/// </summary>
public interface IMemberService
{
    /// <summary>The user as a member (their member bindings); null when they hold none.</summary>
    Task<MemberModel?> GetAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemberModel>> ListByOrganizationAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>Creates a member-class user with its first org binding. The FIRST
    /// member of an organization becomes its member admin (ruling 2026-09-09). Errors
    /// are reported through <paramref name="reportError"/> (identity-validation shape,
    /// mirroring IUserService.CreateUserAsync).</summary>
    Task<IUser?> CreateMemberAsync(CreateMemberRequest request, Action<string, string> reportError, CancellationToken cancellationToken = default);
}
