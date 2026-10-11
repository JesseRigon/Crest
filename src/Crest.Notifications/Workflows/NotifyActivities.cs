using Crest.ContentManagement;
using Crest.Notifications.Models;
using Crest.Users.Indexes;
using Crest.Users.Models;
using Crest.Workflows;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Microsoft.Extensions.Logging;
using YesSql;
using YesSql.Services;

namespace Crest.Notifications.Workflows;

/// <summary>
/// What the notification activities share: the message inputs and the send loop. An
/// external effect: the unit that reached the node commits first; the notifications go out
/// in a unit of their own through every enabled notification method.
/// </summary>
public abstract class NotifyActivityBase : Activity, IUnitBoundary
{
    [Input(DisplayName = "Subject", UIHint = InputUIHints.SingleLine)]
    public Input<string?> Subject { get; set; } = null!;

    [Input(DisplayName = "Summary", Description = "A short summary shown in notification lists.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> Summary { get; set; } = null!;

    [Input(DisplayName = "Text body", UIHint = InputUIHints.MultiLine)]
    public Input<string?> TextBody { get; set; } = null!;

    [Input(DisplayName = "HTML body", UIHint = InputUIHints.MultiLine)]
    public Input<string?> HtmlBody { get; set; } = null!;

    [Input(DisplayName = "Prefer HTML", Description = "Send the HTML body where the method supports it.")]
    public Input<bool> IsHtmlPreferred { get; set; } = new(false);

    [Output(Description = "How many notifications were sent.")]
    public Output<int> Sent { get; set; } = null!;

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var users = await GetUsersAsync(context);
        if (users.Count == 0)
        {
            await FailAsync(context, "No user to notify.");
            return;
        }

        var message = new NotificationMessage
        {
            Subject = Subject.GetOrDefault(context),
            Summary = Summary.GetOrDefault(context),
            TextBody = TextBody.GetOrDefault(context),
            HtmlBody = HtmlBody.GetOrDefault(context),
            IsHtmlPreferred = IsHtmlPreferred.GetOrDefault(context),
        };

        var notifications = context.GetRequiredService<INotificationService>();
        var sent = 0;
        foreach (var user in users)
        {
            var result = await notifications.SendAsync(user, message);
            sent += result.SuccessfulCount;
        }

        Sent.Set(context, sent);
        if (sent == 0)
        {
            await FailAsync(context, "No notification method accepted the message.");
            return;
        }

        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    protected abstract ValueTask<IReadOnlyList<User>> GetUsersAsync(ActivityExecutionContext context);

    protected async ValueTask FailAsync(ActivityExecutionContext context, string reason)
    {
        Failure.Set(context, reason);
        context.GetRequiredService<ILoggerFactory>().CreateLogger(GetType()).LogWarning("{Activity} failed: {Reason}", GetType().Name, reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }
}

/// <summary>Notifies the named users.</summary>
[Activity("Crest.Notifications", "Notifications", "Notifies specific users after this flow's transaction commits.", DisplayName = "Notify users", Kind = ActivityKind.Task, RunAsynchronously = true)]
[FlowNode("Done", "Failed")]
public class NotifyUsers : NotifyActivityBase
{
    [Input(DisplayName = "User names", Description = "The users to notify, comma separated.", UIHint = InputUIHints.SingleLine)]
    public Input<string> UserNames { get; set; } = null!;

    protected override async ValueTask<IReadOnlyList<User>> GetUsersAsync(ActivityExecutionContext context)
    {
        var names = (UserNames.GetOrDefault(context) ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.ToUpperInvariant())
            .Distinct()
            .ToArray();
        if (names.Length == 0)
        {
            return [];
        }

        var session = context.GetRequiredService<ISession>();
        var users = new List<User>();
        foreach (var page in names.PagesOf(1000))
        {
            users.AddRange(await session.Query<User, UserIndex>(user => user.NormalizedUserName.IsIn(page)).ListAsync());
        }

        return users;
    }
}

/// <summary>Notifies the owner of a content item: the one named, or the trigger payload's.</summary>
[Activity("Crest.Notifications", "Notifications", "Notifies the owner of a content item after this flow's transaction commits.", DisplayName = "Notify content owner", Kind = ActivityKind.Task, RunAsynchronously = true)]
[FlowNode("Done", "Failed")]
public class NotifyContentOwner : NotifyActivityBase
{
    [Input(DisplayName = "Content item id", Description = "Empty = the ContentItemId of the trigger payload.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> ContentItemId { get; set; } = null!;

    protected override async ValueTask<IReadOnlyList<User>> GetUsersAsync(ActivityExecutionContext context)
    {
        var contentItemId = ContentItemId.GetOrDefault(context);
        if (string.IsNullOrWhiteSpace(contentItemId))
        {
            var payload = context.FindWorkflowInput<IDictionary<string, object>>(WorkflowsConstants.InputKeys.Payload);
            contentItemId = payload is not null && payload.TryGetValue("ContentItemId", out var id) ? id?.ToString() : null;
        }

        if (string.IsNullOrWhiteSpace(contentItemId))
        {
            return [];
        }

        var item = await context.GetRequiredService<IContentManager>().GetAsync(contentItemId, VersionOptions.Latest);
        if (item is null || string.IsNullOrEmpty(item.Owner))
        {
            return [];
        }

        var owner = await context.GetRequiredService<ISession>().Query<User, UserIndex>(x => x.UserId == item.Owner && x.IsEnabled).FirstOrDefaultAsync();
        return owner is null ? [] : [owner];
    }
}

/// <summary>What Notifications registers with the workflow registry.</summary>
public sealed class NotificationsWorkflowProvider : IWorkflowActivityProvider
{
    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("notifications.notify-users", "Notify users", WorkflowsConstants.Objects.Platform, "Crest.Notifications.NotifyUsers", "Notifies specific users after the unit commits.", 10, Category: "Notifications"),
        new("notifications.notify-content-owner", "Notify content owner", WorkflowsConstants.Objects.Content, "Crest.Notifications.NotifyContentOwner", "Notifies the owner of a content item after the unit commits.", 20, Category: "Notifications"),
    ];
}
