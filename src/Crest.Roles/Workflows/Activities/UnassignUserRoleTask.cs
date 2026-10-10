using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Crest.Users;
using Crest.Users.Models;
using Crest.Users.Services;
using Crest.Workflows.Platform.Abstractions.Models;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Models;
using Crest.Workflows.Platform.Services;

namespace Crest.Roles.Workflows.Activities;

public class UnassignUserRoleTask : TaskActivity<UnassignUserRoleTask>
{
    private readonly UserManager<IUser> _userManager;
    private readonly IUserService _userService;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    protected readonly IStringLocalizer S;

    public UnassignUserRoleTask(UserManager<IUser> userManager, IUserService userService, IWorkflowExpressionEvaluator expressionvaluator, IStringLocalizer<UnassignUserRoleTask> localizer)
    {
        _userManager = userManager;
        _userService = userService;
        _expressionEvaluator = expressionvaluator;
        S = localizer;
    }

    public override LocalizedString DisplayText => S["Unassign User Role Task"];

    public override LocalizedString Category => S["User"];

    public WorkflowExpression<string> UserName
    {
        get => GetProperty(() => new WorkflowExpression<string>());
        set => SetProperty(value);
    }

    public IEnumerable<string> Roles
    {
        get => GetProperty(() => new List<string>());
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome(S["Done"], S["Failed"]);

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var userName = await _expressionEvaluator.EvaluateAsync(UserName, workflowContext, null);

        var u = await _userService.GetUserAsync(userName);

        if (u is User user)
        {
            foreach (var role in Roles)
            {
                if (user.RoleNames.Contains(role))
                {
                    await _userManager.RemoveFromRoleAsync(user, role);

                    // Update the security stamp to invalidate any existing sessions for the user.
                    await _userManager.UpdateSecurityStampAsync(user);
                }
            }

            return Outcome("Done");
        }

        return Outcome("Failed");
    }
}
