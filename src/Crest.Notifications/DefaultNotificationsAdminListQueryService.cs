using Crest.DisplayManagement.ModelBinding;
using Crest.Notifications;
using Crest.Notifications.Models;
using YesSql;

namespace Crest.Navigation;

public sealed class DefaultNotificationsAdminListQueryService : INotificationsAdminListQueryService
{
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;

    public DefaultNotificationsAdminListQueryService(
        ISession session,
        IServiceProvider serviceProvider)
    {
        _session = session;
        _serviceProvider = serviceProvider;
    }

    public async Task<NotificationQueryResult> QueryAsync(int page, int pageSize, ListNotificationOptions options, IUpdateModel updater)
    {
        var query = _session.Query<Notification>(collection: NotificationConstants.NotificationCollection);

        query = await options.FilterResult.ExecuteAsync(new NotificationQueryContext(_serviceProvider, query));

        // Query the count before applying pagination logic.
        var totalCount = await query.CountAsync();

        if (pageSize > 0)
        {
            if (page > 1)
            {
                query = query.Skip((page - 1) * pageSize);
            }

            query = query.Take(pageSize);
        }

        return new NotificationQueryResult()
        {
            Notifications = await query.ListAsync(),
            TotalCount = totalCount,
        };
    }
}
