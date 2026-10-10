using Microsoft.Extensions.Localization;
using Crest.Users.Models;
using Crest.Users.Services;
using Crest.Workflows.Platform.Abstractions.Models;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Models;
using Crest.Workflows.Platform.Services;

namespace Crest.Users.Workflows.Activities;

public abstract class UserActivity : Activity
{
    protected readonly IStringLocalizer S;

    protected UserActivity(IUserService userService, IWorkflowScriptEvaluator scriptEvaluator, IStringLocalizer localizer)
    {
        UserService = userService;
        ScriptEvaluator = scriptEvaluator;
        S = localizer;
    }

    protected IUserService UserService { get; }

    protected IWorkflowScriptEvaluator ScriptEvaluator { get; }

    public override LocalizedString Category => S["User"];

    /// <summary>
    /// An expression that evaluates to an <see cref="User"/> item.
    /// </summary>
    public WorkflowExpression<User> User
    {
        get => GetProperty(() => new WorkflowExpression<User>());
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome(S["Done"]);
    }

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }
}
