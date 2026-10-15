using Grove.Users.Models;
using YesSql.Filters.Query;

namespace Grove.Users.Services;

public interface IUsersAdminListFilterProvider
{
    void Build(QueryEngineBuilder<User> builder);
}
