using Crest.Users.Models;
using YesSql;
using YesSql.Filters.Query.Services;

namespace Crest.Users.Services;

public class UserQueryContext : QueryExecutionContext<User>
{
    public UserQueryContext(IServiceProvider serviceProvider, IQuery<User> query) : base(query)
    {
        ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }
}
