using System.Text.Encodings.Web;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Crest.ContentManagement;
using Crest.Users;
using Crest.Users.Indexes;
using Crest.Users.Models;
using Crest.Workflows.Platform.Models;
using Crest.Workflows.Platform.Services;
using YesSql;

namespace Crest.Notifications.Activities;

public class NotifyContentOwnerTask : NotifyUserTaskActivity<NotifyContentOwnerTask>
{
    private readonly ISession _session;

    public NotifyContentOwnerTask(
       INotificationService notificationCoordinator,
       IWorkflowExpressionEvaluator expressionEvaluator,
       HtmlEncoder htmlEncoder,
       ILogger<NotifyContentOwnerTask> logger,
       IStringLocalizer<NotifyContentOwnerTask> localizer,
       ISession session
   ) : base(notificationCoordinator,
       expressionEvaluator,
       htmlEncoder,
       logger,
       localizer)
    {
        _session = session;
    }

    public override LocalizedString DisplayText => S["Notify Content's Owner Task"];

    protected override async Task<IEnumerable<IUser>> GetUsersAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (workflowContext.Input.TryGetValue("ContentItem", out var obj)
            && obj is ContentItem contentItem
            && !string.IsNullOrEmpty(contentItem.Owner))
        {
            if (workflowContext.Input.TryGetValue("Owner", out var ownerObject) && ownerObject is User user && user.IsEnabled)
            {
                return new[] { user };
            }

            var owner = await _session.Query<User, UserIndex>(x => x.UserId == contentItem.Owner && x.IsEnabled).FirstOrDefaultAsync();

            if (owner != null)
            {
                workflowContext.Input.TryAdd("Owner", owner);

                return new[] { owner };
            }
        }

        return [];
    }
}
