using Grove.Members.Models;
using Grove.Entities;
using Grove.Users.Models;
using YesSql.Indexes;

namespace Grove.Members.Indexes;

/// <summary>
/// A user's connections, one row per binding keyed by the composite
/// <c>(UserId, OrganizationId, Class)</c> (ruling 2026-10-06): "is staff", "may act for
/// organization X", "members of organization X", "does X have any members yet" and the
/// switcher's organization list are all queries on it. A rebuildable projection of the
/// binding record on the user (<see cref="GroveMemberInfo"/>), mapped when the user is saved.
/// </summary>
public class UserConnectionIndex : MapIndex
{
    public string UserId { get; set; } = string.Empty;

    public string OrganizationId { get; set; } = string.Empty;

    public string Class { get; set; } = string.Empty;

    public bool IsMemberAdmin { get; set; }
}

/// <summary>Singleton-safe (no scoped dependencies), like the stock user index provider.</summary>
public class UserConnectionIndexProvider : IndexProvider<User>
{
    public override void Describe(DescribeContext<User> context)
    {
        context.For<UserConnectionIndex>()
            .Map(user =>
            {
                if (!user.TryGet<GroveMemberInfo>(out var info) || info.Bindings.Count == 0)
                {
                    return [];
                }

                return info.Bindings
                    .GroupBy(binding => (binding.OrganizationId, binding.Class))
                    .Select(group => new UserConnectionIndex
                    {
                        UserId = user.UserId,
                        OrganizationId = group.Key.OrganizationId,
                        Class = group.Key.Class,
                        IsMemberAdmin = group.Any(binding => binding.IsMemberAdmin),
                    });
            });
    }
}
