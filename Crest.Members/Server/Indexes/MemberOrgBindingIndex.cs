using Crest.Members.Models;
using OrchardCore.Entities;
using OrchardCore.Users.Models;
using YesSql.Indexes;

namespace Crest.Members.Indexes;

/// <summary>One row per org binding on a member account - "the members of org X" is a
/// SQL query, and "does org X have any members yet" (the first-member-becomes-admin
/// rule) is a COUNT. Same SelectMany shape as stock UserPickerFieldIndexProvider.</summary>
public class MemberOrgBindingIndex : MapIndex
{
    public string UserId { get; set; } = string.Empty;

    public string OrganizationId { get; set; } = string.Empty;

    public bool IsMemberAdmin { get; set; }
}

public class MemberOrgBindingIndexProvider : IndexProvider<User>
{
    public override void Describe(DescribeContext<User> context)
    {
        context.For<MemberOrgBindingIndex>()
            .Map(user =>
            {
                if (!user.TryGet<CrestMemberInfo>(out var memberInfo) || memberInfo.Bindings.Count == 0)
                {
                    return [];
                }

                return memberInfo.Bindings.Select(binding => new MemberOrgBindingIndex
                {
                    UserId = user.UserId,
                    OrganizationId = binding.OrganizationId,
                    IsMemberAdmin = binding.IsMemberAdmin,
                });
            });
    }
}
