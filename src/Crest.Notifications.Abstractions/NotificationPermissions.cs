using Crest.Security.Permissions;

namespace Crest.Notifications;

public static class NotificationPermissions
{
    public static readonly Permission ManageNotifications = new("ManageNotifications", "Manage notifications");
}
