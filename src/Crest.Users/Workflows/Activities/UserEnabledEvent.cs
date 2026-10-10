using Microsoft.Extensions.Localization;
using Crest.Users.Services;
using Crest.Workflows.Platform.Services;

namespace Crest.Users.Workflows.Activities;

public class UserEnabledEvent : UserEvent
{
    public UserEnabledEvent(IUserService userService, IWorkflowScriptEvaluator scriptEvaluator, IStringLocalizer<UserEnabledEvent> localizer) : base(userService, scriptEvaluator, localizer)
    {
    }

    public override string Name => nameof(UserEnabledEvent);

    public override LocalizedString DisplayText => S["User Enabled Event"];
}
