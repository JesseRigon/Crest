using Crest.DisplayManagement.ModelBinding;
using Crest.Notifications.Models;

namespace Crest.Navigation.Core;

public interface INotificationsAdminListQueryService
{
    Task<NotificationQueryResult> QueryAsync(int page, int pageSize, ListNotificationOptions options, IUpdateModel updater);
}
