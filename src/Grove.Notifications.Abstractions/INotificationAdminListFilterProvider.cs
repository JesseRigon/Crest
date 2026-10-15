using YesSql.Filters.Query;

namespace Grove.Notifications;

public interface INotificationAdminListFilterProvider
{
    void Build(QueryEngineBuilder<Notification> builder);
}
