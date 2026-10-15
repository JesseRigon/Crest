#nullable enable
using Grove.Modules;
using Grove.Communications;
using Grove.Communications.Models;
using Grove.Access.Users;
using Microsoft.AspNetCore.Identity;

namespace Grove.Media.Files;

/// <summary>Tells a personal drive's owner that an administrator is opening it (requirement 25).</summary>
public interface IPersonalDriveNotifier
{
    /// <returns>How it went, for the record: the methods it was sent by, or why it failed. A
    /// failure never stops the access.</returns>
    Task<(bool Sent, string Detail)> NotifyAsync(string ownerUserId, PersonalDriveNotice notice, string subject, string body, CancellationToken cancellationToken = default);
}

/// <summary>
/// Notices through the platform's notifications (<c>Grove.Communications</c>), each chosen
/// channel exactly once: a portal notice is the stored per-user notification only (stored as
/// the notification service stores one, with its events, but not sent through the user's
/// methods), an email notice is one send through the Email method. With the notifications
/// feature off nothing can be sent, and that is what is recorded.
/// </summary>
public sealed class NotificationPersonalDriveNotifier(
    IServiceProvider services,
    YesSql.ISession session,
    IClock clock,
    IEnumerable<INotificationService> notifications,
    IEnumerable<INotificationEvents> events,
    IEnumerable<INotificationMethodProvider> methods) : IPersonalDriveNotifier
{
    private const string EmailMethod = "Email";

    public async Task<(bool Sent, string Detail)> NotifyAsync(string ownerUserId, PersonalDriveNotice notice, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (notice == PersonalDriveNotice.None)
        {
            return (true, "no notice");
        }

        if (services.GetService(typeof(UserManager<IUser>)) is not UserManager<IUser> users || await users.FindByIdAsync(ownerUserId) is not { } owner)
        {
            return (false, "the owner's account was not found");
        }

        var message = new NotificationMessage { Subject = subject, Summary = System.Net.WebUtility.HtmlEncode(subject), TextBody = body, HtmlBody = System.Net.WebUtility.HtmlEncode(body), IsHtmlPreferred = false };
        var sent = new List<string>();
        var failed = new List<string>();

        if (notice.HasFlag(PersonalDriveNotice.Portal))
        {
            // A portal notice is stored for the portal to show (requirement 30); with portal
            // notifications off nobody would see it, so it counts as not sent.
            if (notifications.Any() && services.GetService(typeof(IPortalNotifications)) is not null)
            {
                await StoreAsync(owner, ownerUserId, message, cancellationToken);
                sent.Add("portal");
            }
            else
            {
                failed.Add("portal (portal notifications are off)");
            }
        }

        if (notice.HasFlag(PersonalDriveNotice.Email))
        {
            if (methods.FirstOrDefault(method => method.Method == EmailMethod) is { } email)
            {
                var result = await email.SendAsync(owner, message, cancellationToken);
                (result.Succeeded ? sent : failed).Add(result.Succeeded ? "email" : $"email ({string.Join("; ", result.Errors.Select(error => error.Message))})");
            }
            else
            {
                failed.Add("email (email notifications are off)");
            }
        }

        var detail = string.Join(", ", sent.Select(method => $"sent by {method}").Concat(failed.Select(method => $"failed: {method}")));
        return (failed.Count == 0, detail);
    }

    /// <summary>Stores the notification the portal shows, without sending it anywhere.</summary>
    private async Task StoreAsync(object owner, string ownerUserId, INotificationMessage message, CancellationToken cancellationToken)
    {
        var context = new NotificationContext(message, owner)
        {
            Notification = new Notification
            {
                NotificationId = IdGenerator.GenerateId(),
                CreatedUtc = clock.UtcNow,
                Subject = message.Subject,
                Summary = message.Summary,
                UserId = ownerUserId,
            },
        };

        foreach (var handler in events)
        {
            await handler.CreatingAsync(context, cancellationToken);
        }

        await session.SaveAsync(context.Notification, collection: NotificationConstants.NotificationCollection, cancellationToken: cancellationToken);
        foreach (var handler in events)
        {
            await handler.CreatedAsync(context, cancellationToken);
        }
    }
}
