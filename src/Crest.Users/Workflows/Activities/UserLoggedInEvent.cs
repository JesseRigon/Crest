using Microsoft.Extensions.Localization;
using Crest.Users.Services;
using Crest.Workflows.Platform.Services;

namespace Crest.Users.Workflows.Activities;

public class UserLoggedInEvent : UserEvent
{
    public UserLoggedInEvent(IUserService userService, IWorkflowScriptEvaluator scriptEvaluator, IStringLocalizer<UserLoggedInEvent> localizer) : base(userService, scriptEvaluator, localizer)
    {
    }

    public override string Name => nameof(UserLoggedInEvent);

    public override LocalizedString DisplayText => S["User Loggedin Event"];
}
