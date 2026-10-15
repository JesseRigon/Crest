namespace Grove.Communications;

public static class NotificationsHelper
{
    public static string GetUnreadUserNotificationTagKey(string username)
        => $"{NotificationConstants.TopUnreadUserNotificationCacheTag}:{username}";
}
