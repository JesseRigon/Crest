using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Notifications.ViewModels;

namespace Crest.Notifications.Drivers;

public sealed class NotificationDisplayDriver : DisplayDriver<Notification>
{
    public override Task<IDisplayResult> DisplayAsync(Notification notification, BuildDisplayContext context)
    {
        return CombineAsync(
            Factory("NotificationsMeta_SummaryAdmin", static (Notification n) => new NotificationViewModel(n), notification)
                .Location(PlatformConstants.DisplayType.SummaryAdmin, "Meta:20"),
            Factory("NotificationsActions_SummaryAdmin", static (Notification n) => new NotificationViewModel(n), notification)
                .Location(PlatformConstants.DisplayType.SummaryAdmin, "Actions:5"),
            Factory("NotificationsButtonActions_SummaryAdmin", static (Notification n) => new NotificationViewModel(n), notification)
                .Location(PlatformConstants.DisplayType.SummaryAdmin, "ActionsMenu:10")
        );
    }
}
