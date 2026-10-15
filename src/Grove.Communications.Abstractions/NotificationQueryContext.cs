using Grove.Communications;
using YesSql;
using YesSql.Filters.Query.Services;

namespace Grove.Display.Navigation;

public class NotificationQueryContext : QueryExecutionContext<Notification>
{
    public NotificationQueryContext(IServiceProvider serviceProvider, IQuery<Notification> query)
        : base(query)
    {
        ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }
}
