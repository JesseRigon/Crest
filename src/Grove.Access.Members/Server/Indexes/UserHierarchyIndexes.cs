using Grove.Access.Members.Models;
using YesSql.Indexes;

namespace Grove.Access.Members.Indexes;

/// <summary>One row per hierarchy membership document: finds a user's node in an organization.</summary>
public sealed class UserHierarchyIndex : MapIndex
{
    public string UserId { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
}

/// <summary>
/// The closure table: one row per (ancestor, descendant) pair within an organization.
/// "Everyone under me" is <c>AncestorId == me</c>; "everyone above me" is
/// <c>DescendantId == me</c>. Every query carries the organization (the root guard).
/// </summary>
public sealed class UserHierarchyClosureIndex : MapIndex
{
    public string AncestorId { get; set; } = string.Empty;
    public string DescendantId { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
}

public sealed class UserHierarchyIndexProvider : IndexProvider<UserHierarchyMembership>
{
    public override void Describe(DescribeContext<UserHierarchyMembership> context)
    {
        context.For<UserHierarchyIndex>()
            .Map(membership => new UserHierarchyIndex { UserId = membership.UserId, OrganizationId = membership.OrganizationId });

        context.For<UserHierarchyClosureIndex>()
            .Map(membership => membership.AncestorIds.Select(ancestor => new UserHierarchyClosureIndex
            {
                AncestorId = ancestor,
                DescendantId = membership.UserId,
                OrganizationId = membership.OrganizationId,
            }));
    }
}
