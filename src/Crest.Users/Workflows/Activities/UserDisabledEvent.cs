using Microsoft.Extensions.Localization;
using Crest.Users.Services;
using Crest.Workflows.Platform.Services;

namespace Crest.Users.Workflows.Activities;

public class UserDisabledEvent : UserEvent
{
    public UserDisabledEvent(IUserService userService, IWorkflowScriptEvaluator scriptEvaluator, IStringLocalizer<UserDisabledEvent> localizer) : base(userService, scriptEvaluator, localizer)
    {
    }

    public override string Name => nameof(UserDisabledEvent);

    public override LocalizedString DisplayText => S["User Disabled Event"];
}
