using Grove.Display.Management.Views;

namespace Grove.Communications.ViewModels;

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
