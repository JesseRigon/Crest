#nullable enable

namespace Grove.Access.Roles;

/// <summary>
/// How a role is held (docs/sharing.md § 1, requirement 44): <see cref="Assigned"/> roles are
/// stored on users, the ordinary kind; <see cref="Derived"/> roles are computed per caller by an
/// <see cref="IDerivedRoleProvider"/> from records that are the only source of truth (a
/// business relationship), and are never stored on a user or assigned.
/// </summary>
public enum RoleKind
{
    Assigned = 0,
    Derived = 1,
}

/// <summary>A role a provider computes: its stable name (the role's identity everywhere, the
/// access index included) and a short description of what holding it means.</summary>
public sealed record DerivedRoleDefinition(string RoleName, string? Description = null);

/// <summary>
/// Who an <see cref="IDerivedRoleProvider"/> is asked about: the user, and the organization they
/// act for (null on the admin and site sides). With <see cref="AllOrganizations"/> the provider
/// answers for every organization the user is linked to, each grant naming its organization
/// (the user editor's read-only list, requirement 47); otherwise only for
/// <see cref="OrganizationId"/> (requirement 42).
/// </summary>
public sealed record DerivedRoleContext(string UserId, string? OrganizationId, bool AllOrganizations = false);

/// <summary>
/// One derived role a user holds and the relationship that grants it ("Customer, member of Acme
/// Corp"). <see cref="OrganizationId"/> is the organization the role holds for: the user holds it
/// only while acting for that organization (requirement 42). Null: held whatever the user acts
/// for (a relationship of the person themselves). <see cref="RelationshipId"/> names the record
/// that grants it (a content item id), so a screen can link to it: changing that record is the
/// only way to change the role.
/// </summary>
public sealed record DerivedRoleGrant(
    string RoleName,
    string Relationship,
    string? OrganizationId = null,
    string? RelationshipId = null);

/// <summary>
/// Computes business roles (docs/sharing.md § 5, requirement 48): given a user and the
/// organization they act for, which of its <see cref="Roles"/> they hold. Party relationships are
/// one provider; nothing that reads roles needs to know what a party is. Register it scoped from
/// the module that owns the relationship records, and bump the tenant's permission version
/// whenever a record that grants a role changes (requirement 43): callers are rebuilt on their
/// next request.
/// </summary>
public interface IDerivedRoleProvider
{
    /// <summary>A short name for screens and the registry ("parties").</summary>
    string Name { get; }

    /// <summary>The roles this provider computes. Each is a real role (the registry creates it
    /// when it is missing) and is <see cref="RoleKind.Derived"/>: it can never be assigned.</summary>
    IEnumerable<DerivedRoleDefinition> Roles { get; }

    Task<IReadOnlyList<DerivedRoleGrant>> GetRolesAsync(DerivedRoleContext context, CancellationToken cancellationToken = default);
}

/// <summary>A role as the registry knows it: its kind and, for a derived role, its provider.</summary>
public sealed record RegisteredRole(string RoleName, RoleKind Kind, string? Provider = null, string? Description = null);

/// <summary>
/// Grove's role registry (requirement 44): every role with its kind. Orchard's role model has
/// no field for the kind and its system-role mechanism covers only its fixed roles, so the kind
/// is kept here: a role is <see cref="RoleKind.Derived"/> when an enabled provider computes it.
/// </summary>
public interface IRoleRegistry
{
    /// <summary>Every stored role and every derived role, by name.</summary>
    Task<IReadOnlyList<RegisteredRole>> ListAsync(CancellationToken cancellationToken = default);

    RoleKind KindOf(string roleName);

    /// <summary>The derived roles <paramref name="context"/>'s user holds, from every provider.</summary>
    Task<IReadOnlyList<DerivedRoleGrant>> DerivedRolesAsync(DerivedRoleContext context, CancellationToken cancellationToken = default);

    /// <summary>Creates the stored role of every derived role that has none, with no
    /// permissions, so it appears in pickers and permission screens (requirement 40).</summary>
    Task EnsureDerivedRolesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Thrown when a derived role is assigned (requirement 45): every assignment path
/// refuses it on the server.</summary>
public sealed class DerivedRoleAssignmentException(string roleName)
    : InvalidOperationException($"'{roleName}' is a derived role: it is computed from a relationship and cannot be assigned.")
{
    public string RoleName { get; } = roleName;
}

public static class DerivedRoles
{
    /// <summary>Whether a provider among <paramref name="providers"/> computes <paramref name="roleName"/>.</summary>
    public static bool IsDerived(IEnumerable<IDerivedRoleProvider> providers, string roleName) =>
        providers.Any(provider => provider.Roles.Any(role => string.Equals(role.RoleName, roleName, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// The grants every provider gives <paramref name="context"/>'s user. Unless the context asks
    /// for all organizations, only grants for no organization or for the acting one count
    /// (requirement 42), whatever a provider returned.
    /// </summary>
    public static async Task<IReadOnlyList<DerivedRoleGrant>> GrantsAsync(IEnumerable<IDerivedRoleProvider> providers, DerivedRoleContext context, CancellationToken cancellationToken = default)
    {
        var grants = new List<DerivedRoleGrant>();
        foreach (var provider in providers)
        {
            var declared = provider.Roles.Select(role => role.RoleName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var grant in await provider.GetRolesAsync(context, cancellationToken))
            {
                // A provider grants only the roles it declares: nothing it returns can stand in
                // for an assigned role.
                if (!declared.Contains(grant.RoleName))
                {
                    continue;
                }

                if (context.AllOrganizations || grant.OrganizationId is null || string.Equals(grant.OrganizationId, context.OrganizationId, StringComparison.Ordinal))
                {
                    grants.Add(grant);
                }
            }
        }

        return grants;
    }
}
