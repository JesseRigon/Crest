using Crest.DisplayManagement.Views;

namespace Crest.Notifications.ViewModels;

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
