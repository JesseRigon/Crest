using YesSql.Filters.Query;

namespace Crest.Notifications;

public interface INotificationAdminListFilterProvider
{
    void Build(QueryEngineBuilder<Notification> builder);
}
