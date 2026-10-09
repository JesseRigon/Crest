using Crest.Users.Models;
using YesSql.Filters.Query;

namespace Crest.Users.Services;

public interface IUsersAdminListFilterProvider
{
    void Build(QueryEngineBuilder<User> builder);
}
