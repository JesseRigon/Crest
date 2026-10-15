using Grove.Security.Permissions;

namespace Grove.Notifications;

public static class NotificationPermissions
{
    public static readonly Permission ManageNotifications = new("ManageNotifications", "Manage notifications");
}
