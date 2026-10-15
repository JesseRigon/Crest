using Grove.Api;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Grove.Entities;
using Grove.Modules;
using Grove.Environment.Cache;
using Grove.Communications.Indexes;
using Grove.Communications.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using YesSql;

namespace Grove.Communications.Notifications.Portal;

public sealed record PortalNotificationDto(string Id, string Subject, string Summary, DateTime CreatedUtc, bool IsRead, DateTime? ReadUtc);

public sealed record PortalNotificationListDto(int Unread, int Total, IReadOnlyList<PortalNotificationDto> Items);

/// <summary>
/// Portal notifications (docs/media.md requirement 30): the signed-in user's own stored
/// notifications as Grove's admin and member shells show them, an indicator (the unread count),
/// the list, and marking as read. Only ever the caller's own: the user id comes from the
/// request's principal, never from the request.
/// </summary>
public sealed class PortalNotificationsApi : ApiContributor
{
    public override string Name => "PortalNotifications";

    public override string FeatureId => PortalNotificationsStartup.FeatureId;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("notifications").RequireAuthorization();
        routes.MapGet("", (HttpContext http, [FromQuery] int? skip, [FromQuery] int? take, [FromQuery] bool? unreadOnly) => ApiHandlers.For<PortalNotificationsHandlers>(http).ListAsync(skip ?? 0, take ?? 20, unreadOnly ?? false));
        routes.MapPost("read-all", ApiHandlers.Handle<PortalNotificationsHandlers>(h => h.MarkAllReadAsync()));
        routes.MapPost("{id}/read", (HttpContext http, [FromRoute] string id) => ApiHandlers.For<PortalNotificationsHandlers>(http).MarkReadAsync(id));
        routes.MapPost("{id}/unread", (HttpContext http, [FromRoute] string id) => ApiHandlers.For<PortalNotificationsHandlers>(http).MarkUnreadAsync(id));
        routes.MapDelete("{id}", (HttpContext http, [FromRoute] string id) => ApiHandlers.For<PortalNotificationsHandlers>(http).DeleteAsync(id));
    }
}

public sealed class PortalNotificationsHandlers(
    YesSql.ISession session,
    IAuthorizationService authorization,
    ITagCache tagCache,
    IClock clock) : ApiHandlers
{
    private const int MaxTake = 100;

    public async Task<IResult> ListAsync(int skip = 0, int take = 20, bool unreadOnly = false)
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

    public async Task<IResult> MarkReadAsync(string id)
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

    public async Task<IResult> MarkAllReadAsync()
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

    public async Task<IResult> MarkUnreadAsync(string id)
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

        var readInfo = notification.GetOrCreate<NotificationReadInfo>();
        if (readInfo.IsRead)
        {
            readInfo.IsRead = false;
            readInfo.ReadAtUtc = null;
            notification.Put(readInfo);
            await session.SaveAsync(notification, collection: NotificationConstants.NotificationCollection);
            await tagCache.RemoveTagAsync(NotificationsHelper.GetUnreadUserNotificationTagKey(User.Identity?.Name));
        }

        return Ok(ToDto(notification));
    }

    public async Task<IResult> DeleteAsync(string id)
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

        session.Delete(notification, collection: NotificationConstants.NotificationCollection);
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
