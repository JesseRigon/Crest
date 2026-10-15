using YesSql.Filters.Query;

namespace Grove.Communications;

public interface INotificationAdminListFilterProvider
{
    void Build(QueryEngineBuilder<Notification> builder);
}
