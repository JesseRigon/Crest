namespace Crest.Access.Services;

/// <summary>What a role holds: its sets and its loose permissions.</summary>
public sealed record RoleDefinition(string Name, IReadOnlyList<string> Sets, IReadOnlyList<string> Permissions);

/// <summary>The relations one user's access at one reach is resolved from.</summary>
public sealed record AccessResolutionInput(
    IReadOnlyList<AccessGroupModel> Groups,
    IReadOnlyList<string> DirectRoles,
    IReadOnlyList<UserAccessOverride> Overrides,
    Func<string, RoleDefinition?> Role,
    Func<string, IReadOnlyList<string>?> Set);

/// <summary>
/// The one resolution (docs/permissions.md › Layers, Overrides): group → role → set →
/// permission, each level taking the user's overrides at that level, so the most specific
/// level that mentions a permission decides it. Pure: the projector feeds it the relations,
/// tests feed it tables. Ceilings are not applied here; the decision applies them after.
/// </summary>
public static class AccessResolution
{
    public static UserAccess Resolve(string userId, AccessReach reach, long version, AccessResolutionInput input)
    {
        // A grant and a deny of one name at one level is refused when written; here a deny wins.
        var overrides = input.Overrides.ToLookup(entry => (entry.Level, entry.Effect));
        IEnumerable<string> Named(OverrideLevel level, OverrideEffect effect) => overrides[(level, effect)].Select(entry => entry.Name);

        // Roles: the groups' and the direct ones, then the role overrides.
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        roles.UnionWith(input.Groups.SelectMany(group => group.Roles));
        roles.UnionWith(input.DirectRoles);
        roles.UnionWith(Named(OverrideLevel.Role, OverrideEffect.Grant));
        roles.ExceptWith(Named(OverrideLevel.Role, OverrideEffect.Deny));

        // Sets: those the held roles hold, then the set overrides.
        var setsVia = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var permissions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleName in roles)
        {
            if (input.Role(roleName) is not { } role)
            {
                continue;
            }

            foreach (var set in role.Sets)
            {
                setsVia.TryAdd(set, roleName);
            }

            foreach (var permission in role.Permissions)
            {
                permissions.TryAdd(permission, UserAccessGrant.RoleSource(roleName));
            }
        }

        foreach (var set in Named(OverrideLevel.Set, OverrideEffect.Grant))
        {
            setsVia.TryAdd(set, UserAccessGrant.OverrideSource);
        }

        foreach (var set in Named(OverrideLevel.Set, OverrideEffect.Deny))
        {
            setsVia.Remove(set);
        }

        // Permissions: the roles' loose ones, the held sets', then the permission overrides.
        foreach (var (set, via) in setsVia)
        {
            foreach (var permission in input.Set(set) ?? [])
            {
                permissions.TryAdd(permission, via == UserAccessGrant.OverrideSource ? $"set:{set} via override" : UserAccessGrant.SetSource(set, via));
            }
        }

        foreach (var permission in Named(OverrideLevel.Permission, OverrideEffect.Grant))
        {
            permissions[permission] = UserAccessGrant.OverrideSource;
        }

        foreach (var permission in Named(OverrideLevel.Permission, OverrideEffect.Deny))
        {
            permissions.Remove(permission);
        }

        return new UserAccess(
            userId,
            reach,
            new HashSet<string>(input.Groups.Select(group => group.Name), StringComparer.OrdinalIgnoreCase),
            roles,
            new HashSet<string>(setsVia.Keys, StringComparer.OrdinalIgnoreCase),
            permissions.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).Select(entry => new UserAccessGrant(entry.Key, entry.Value)).ToArray(),
            version);
    }

    /// <summary>The same-level conflict rule the editors refuse at write time.</summary>
    public static AccessOverrideConflictException? Conflict(IEnumerable<UserAccessOverride> overrides)
    {
        foreach (var group in overrides.GroupBy(entry => (entry.Level, Name: entry.Name.ToUpperInvariant())))
        {
            if (group.Select(entry => entry.Effect).Distinct().Count() > 1)
            {
                return new AccessOverrideConflictException(group.Key.Level, group.First().Name);
            }
        }

        return null;
    }
}
