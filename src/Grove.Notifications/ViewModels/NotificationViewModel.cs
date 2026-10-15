using Grove.DisplayManagement.Views;

namespace Grove.Notifications.ViewModels;

public class NotificationViewModel : ShapeViewModel
{
    public Notification Notification { get; set; }

    public NotificationViewModel()
    {
    }

    public NotificationViewModel(Notification notification)
    {
        Notification = notification;
    }
}
