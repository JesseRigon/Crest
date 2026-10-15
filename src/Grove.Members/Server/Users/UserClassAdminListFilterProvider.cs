using Grove.Members.Constants;
using Grove.Members.Indexes;
using Grove.Users.Models;
using Grove.Users.Services;
using YesSql.Filters.Query;
using YesSql.Services;

namespace Grove.Members.Users;

/// <summary>
/// The user list's class filter: a named search term, <c>class:member</c> or
/// <c>class:staff</c>, over the connection index (the stock UsersFilter enum is closed,
/// so a named term is the route, not an enum fork). A user who is both matches either.
/// Shaped like the stock role-restriction term, so a forced <c>.AlwaysRun()</c> scoping
/// term can join it later for portal-side lists.
/// </summary>
public sealed class UserClassAdminListFilterProvider : IUsersAdminListFilterProvider
{
    public const string TermName = "class";

    public void Build(QueryEngineBuilder<User> builder)
    {
        builder.WithNamedTerm(TermName, term => term
            .OneCondition((value, query, context) =>
            {
                var userClass = value?.Trim().ToLowerInvariant();
                if (userClass is UserClasses.Staff or UserClasses.Member)
                {
                    query.With<UserConnectionIndex>(index => index.Class == userClass);
                }

                return ValueTask.FromResult(query);
            }));
    }
}
