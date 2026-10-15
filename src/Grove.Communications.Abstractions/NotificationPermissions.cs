using Grove.Security.Permissions;

namespace Grove.Communications;

public static class NotificationPermissions
{
    public static readonly Permission ManageNotifications = new("ManageNotifications", "Manage notifications");
}
