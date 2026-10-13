using Crest.Entities;

namespace Crest.Members.Models;

/// <summary>One connection of a user to an organization, as stored on the user record:
/// which organization, in which CLASS (staff: the tenant's own organization; member: any
/// other), which member ROLE TEMPLATES apply there (tenant-defined Crest roles - the tenant
/// governs powers, the binding only points; staff connections carry none, staff roles are
/// the user's tenant roles), and whether this member is the org's member admin. Unique by
/// (organization, class).</summary>
public class MemberOrgBinding
{
    public string OrganizationId { get; set; } = string.Empty;

    /// <summary>One of <see cref="Constants.UserClasses"/>.</summary>
    public string Class { get; set; } = Constants.UserClasses.Member;

    public List<string> Roles { get; set; } = [];

    public bool IsMemberAdmin { get; set; }
}

/// <summary>
/// The binding record stored in <c>User.Properties</c> (the stock entity-aspect pattern):
/// the RECORD OF TRUTH for a user's classes and organizations (ruling 2026-10-06). The
/// class is a set derived from the bindings: staff is a binding to the tenant
/// organization, member a binding to any other. One account, many bindings; the ACTIVE
/// binding is session state, never stored here. Written only by
/// <see cref="Services.MemberBindingService"/>; the connection index and the hierarchy
/// nodes are projections rebuildable from it.
/// </summary>
public class CrestMemberInfo
{
    /// <summary>The linked Person content item id (the party model carries the
    /// person↔organization relationship; this record carries authentication).</summary>
    public string? PersonId { get; set; }

    public List<MemberOrgBinding> Bindings { get; set; } = [];
}

public static class UserClassSet
{
    /// <summary>The user's classes, derived from the bindings (empty: a user outside every
    /// organization, the failure mode the rebuild repairs).</summary>
    public static IReadOnlyList<string> Classes(Crest.Users.Models.User user) =>
        user.TryGet<CrestMemberInfo>(out var info)
            ? [.. info.Bindings.Select(binding => binding.Class).Distinct(StringComparer.Ordinal)]
            : [];

    public static bool Has(Crest.Users.Models.User user, string userClass) =>
        Classes(user).Contains(userClass, StringComparer.Ordinal);

    public static IEnumerable<MemberOrgBinding> Bindings(Crest.Users.Models.User user, string userClass) =>
        user.TryGet<CrestMemberInfo>(out var info)
            ? info.Bindings.Where(binding => binding.Class == userClass)
            : [];
}
