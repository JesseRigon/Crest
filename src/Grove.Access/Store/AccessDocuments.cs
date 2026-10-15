using YesSql.Indexes;

namespace Grove.Access.Store;

/// <summary>A group, one YesSql document (docs/permissions.md › Layers): users bound to roles within a reach.</summary>
public sealed class AccessGroup
{
    public string GroupId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>The organization the group belongs to; empty for a tenant group.</summary>
    public string OrganizationId { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];

    public List<string> UserIds { get; set; } = [];

    /// <summary>The tenant group an organization's group was cloned from.</summary>
    public string? TemplateId { get; set; }
}

/// <summary>One row per group: name and reach.</summary>
public sealed class AccessGroupIndex : MapIndex
{
    public string GroupId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string OrganizationId { get; set; } = string.Empty;

    public string? TemplateId { get; set; }
}

/// <summary>One row per (group, user): "which groups is this user in, at this reach" is a query.</summary>
public sealed class AccessGroupMemberIndex : MapIndex
{
    public string GroupId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string OrganizationId { get; set; } = string.Empty;
}

public sealed class AccessGroupIndexProvider : IndexProvider<AccessGroup>
{
    public override void Describe(DescribeContext<AccessGroup> context)
    {
        context.For<AccessGroupIndex>()
            .Map(group => new AccessGroupIndex { GroupId = group.GroupId, Name = group.Name, OrganizationId = group.OrganizationId, TemplateId = group.TemplateId });

        context.For<AccessGroupMemberIndex>()
            .Map(group => group.UserIds
                .Distinct(StringComparer.Ordinal)
                .Select(userId => new AccessGroupMemberIndex { GroupId = group.GroupId, UserId = userId, OrganizationId = group.OrganizationId }));
    }
}

/// <summary>A user's overrides at one reach, one document (docs/permissions.md › Overrides).</summary>
public sealed class UserAccessOverrides
{
    public string UserId { get; set; } = string.Empty;

    public string OrganizationId { get; set; } = string.Empty;

    public List<UserAccessOverride> Entries { get; set; } = [];
}

public sealed class UserAccessOverrideIndex : MapIndex
{
    public string UserId { get; set; } = string.Empty;

    public string OrganizationId { get; set; } = string.Empty;
}

public sealed class UserAccessOverrideIndexProvider : IndexProvider<UserAccessOverrides>
{
    public override void Describe(DescribeContext<UserAccessOverrides> context) =>
        context.For<UserAccessOverrideIndex>()
            .Map(overrides => new UserAccessOverrideIndex { UserId = overrides.UserId, OrganizationId = overrides.OrganizationId });
}

/// <summary>
/// The projection document (docs/permissions.md › Projection): one per (user, reach), the
/// resolved access under the permission version it was built at. Rebuilt, never edited.
/// </summary>
public sealed class UserAccessProjection
{
    public string UserId { get; set; } = string.Empty;

    public string OrganizationId { get; set; } = string.Empty;

    public long PermissionVersion { get; set; }

    public List<string> Groups { get; set; } = [];

    public List<string> Roles { get; set; } = [];

    public List<string> Sets { get; set; } = [];

    public List<UserAccessGrant> Permissions { get; set; } = [];
}

/// <summary>
/// <c>UserAccessIndex</c>: the rows the hot path reads, keyed by (user, tenant | organization),
/// one per group, role, set and permission the user holds there, with the provenance of
/// each permission and the version the projection was built under.
/// </summary>
public sealed class UserAccessIndex : MapIndex
{
    public const string GroupKind = "group";
    public const string RoleKind = "role";
    public const string SetKind = "set";
    public const string PermissionKind = "permission";

    public string UserId { get; set; } = string.Empty;

    public string OrganizationId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Source { get; set; }

    public long PermissionVersion { get; set; }
}

public sealed class UserAccessIndexProvider : IndexProvider<UserAccessProjection>
{
    public override void Describe(DescribeContext<UserAccessProjection> context) =>
        context.For<UserAccessIndex>()
            .Map(projection =>
            {
                UserAccessIndex Row(string kind, string name, string? source = null) => new()
                {
                    UserId = projection.UserId,
                    OrganizationId = projection.OrganizationId,
                    Kind = kind,
                    Name = name,
                    Source = source,
                    PermissionVersion = projection.PermissionVersion,
                };

                return projection.Groups.Select(group => Row(UserAccessIndex.GroupKind, group))
                    .Concat(projection.Roles.Select(role => Row(UserAccessIndex.RoleKind, role)))
                    .Concat(projection.Sets.Select(set => Row(UserAccessIndex.SetKind, set)))
                    .Concat(projection.Permissions.Select(grant => Row(UserAccessIndex.PermissionKind, grant.Permission, grant.Source)))
                    // A user with nothing at a reach still has a row, so the reader can tell "built, empty" from "never built".
                    .DefaultIfEmpty(Row(UserAccessIndex.PermissionKind, string.Empty))
                    .ToArray();
            });
}
