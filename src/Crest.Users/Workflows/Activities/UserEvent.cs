using Microsoft.Extensions.Localization;
using Crest.Users.Services;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Models;
using Crest.Workflows.Platform.Services;

namespace Crest.Users.Workflows.Activities;

public abstract class UserEvent : UserActivity, IEvent
{
    public UserEvent(IUserService userService, IWorkflowScriptEvaluator scriptEvaluator, IStringLocalizer localizer) : base(userService, scriptEvaluator, localizer)
    {
    }

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Halt();
    }

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }
}
