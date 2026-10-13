namespace Crest.Notifications;

/// <summary>
/// Present while the tenant's portal notifications are on (<c>Crest.Notifications.Portal</c>,
/// docs/media.md requirement 30): each user's stored notifications are shown in Crest's admin
/// and member shells. A module storing a notification only for the portal checks for it, and
/// records the notice as not delivered when it is off.
/// </summary>
public interface IPortalNotifications
{
}
