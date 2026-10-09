using Crest.Members.Constants;
using Crest.Members.Models;
using Crest.Entities;
using Crest.Users.Models;
using YesSql.Indexes;

namespace Crest.Members.Indexes;

/// <summary>One row per user: the class the account was stamped with. Backs reliable
/// SQL filtering of users by class (the Properties bag itself is unqueryable).</summary>
public class UserClassIndex : MapIndex
{
    public string UserId { get; set; } = string.Empty;

    public string Class { get; set; } = UserClasses.Staff;
}

/// <summary>
/// Same registration mechanism as stock <c>UserIndexProvider</c>. Must stay
/// SINGLETON-SAFE (index providers are singletons; see the ILookupNormalizer note in
/// stock Users/Startup.cs) - no scoped dependencies. Users with no aspect map to staff.
/// YesSql has no index rebuild path, so a row only appears when its user is saved.
/// </summary>
public class UserClassIndexProvider : IndexProvider<User>
{
    public override void Describe(DescribeContext<User> context)
    {
        context.For<UserClassIndex>()
            .Map(user => new UserClassIndex
            {
                UserId = user.UserId,
                Class = user.TryGet<CrestUserClass>(out var userClass)
                    ? userClass.Class
                    : UserClasses.Staff,
            });
    }
}
