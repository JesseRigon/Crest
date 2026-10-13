using System.Security.Claims;
using Crest.Entities;
using Crest.Environment.Cache;
using Crest.Modules;
using Crest.Notifications.Indexes;
using Crest.Notifications.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YesSql;

namespace Crest.Notifications.Portal;

public sealed record PortalNotificationDto(string Id, string Subject, string Summary, DateTime CreatedUtc, bool IsRead, DateTime? ReadUtc);

public sealed record PortalNotificationListDto(int Unread, int Total, IReadOnlyList<PortalNotificationDto> Items);

/// <summary>
/// Portal notifications (docs/media.md requirement 30): the signed-in user's own stored
/// notifications as Crest's admin and member shells show them, an indicator (the unread count),
/// the list, and marking as read. Only ever the caller's own: the user id comes from the
/// request's principal, never from the request.
/// </summary>
[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken]
[Feature(PortalNotificationsStartup.FeatureId)]
[Route("api/crest/notifications")]
public sealed class PortalNotificationsController(
    YesSql.ISession session,
    IAuthorizationService authorization,
    ITagCache tagCache,
    IClock clock) : ControllerBase
{
    private const int MaxTake = 100;

    [HttpGet]
    public async Task<ActionResult<PortalNotificationListDto>> ListAsync([FromQuery] int skip = 0, [FromQuery] int take = 20, [FromQuery] bool unreadOnly = false)
    {
        if (await UserIdAsync() is not { } userId)
        {
            return Forbid();
        }

        take = Math.Clamp(take, 1, MaxTake);
        skip = Math.Max(0, skip);

        var unread = await session.QueryIndex<NotificationIndex>(index => index.UserId == userId && !index.IsRead, collection: NotificationConstants.NotificationCollection).CountAsync();
        var query = unreadOnly
            ? session.Query<Notification, NotificationIndex>(index => index.UserId == userId && !index.IsRead, collection: NotificationConstants.NotificationCollection)
            : session.Query<Notification, NotificationIndex>(index => index.UserId == userId, collection: NotificationConstants.NotificationCollection);
        var total = await query.CountAsync();
        var page = await query.OrderByDescending(index => index.CreatedAtUtc).Skip(skip).Take(take).ListAsync();

        return Ok(new PortalNotificationListDto(unread, total, [.. page.Select(ToDto)]));
    }

    [HttpPost("{id}/read")]
    public async Task<ActionResult<PortalNotificationDto>> MarkReadAsync(string id)
    {
        if (await UserIdAsync() is not { } userId)
        {
            return Forbid();
        }

        var notification = await session.Query<Notification, NotificationIndex>(index => index.NotificationId == id && index.UserId == userId, collection: NotificationConstants.NotificationCollection).FirstOrDefaultAsync();
        if (notification is null)
        {
            return NotFound();
        }

        await MarkAsync(notification);
        await tagCache.RemoveTagAsync(NotificationsHelper.GetUnreadUserNotificationTagKey(User.Identity?.Name));
        return Ok(ToDto(notification));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllReadAsync()
    {
        if (await UserIdAsync() is not { } userId)
        {
            return Forbid();
        }

        foreach (var notification in await session.Query<Notification, NotificationIndex>(index => index.UserId == userId && !index.IsRead, collection: NotificationConstants.NotificationCollection).ListAsync())
        {
            await MarkAsync(notification);
        }

        await tagCache.RemoveTagAsync(NotificationsHelper.GetUnreadUserNotificationTagKey(User.Identity?.Name));
        return NoContent();
    }

    /// <summary>The caller's own user id, when they may manage their notifications.</summary>
    private async Task<string?> UserIdAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userId) && await authorization.AuthorizeAsync(User, NotificationPermissions.ManageNotifications) ? userId : null;
    }

    private async Task MarkAsync(Notification notification)
    {
        var readInfo = notification.GetOrCreate<NotificationReadInfo>();
        if (readInfo.IsRead)
        {
            return;
        }

        readInfo.IsRead = true;
        readInfo.ReadAtUtc = clock.UtcNow;
        notification.Put(readInfo);
        await session.SaveAsync(notification, collection: NotificationConstants.NotificationCollection);
    }

    private static PortalNotificationDto ToDto(Notification notification)
    {
        var readInfo = notification.GetOrCreate<NotificationReadInfo>();
        return new PortalNotificationDto(notification.NotificationId, notification.Subject, notification.Summary, notification.CreatedUtc, readInfo.IsRead, readInfo.ReadAtUtc);
    }
}
