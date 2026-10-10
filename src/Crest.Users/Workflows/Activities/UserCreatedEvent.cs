using Microsoft.Extensions.Localization;
using Crest.Users.Services;
using Crest.Workflows.Platform.Services;

namespace Crest.Users.Workflows.Activities;

public class UserCreatedEvent : UserEvent
{
    public UserCreatedEvent(IUserService userService, IWorkflowScriptEvaluator scriptEvaluator, IStringLocalizer<UserCreatedEvent> localizer) : base(userService, scriptEvaluator, localizer)
    {
    }

    public override string Name => nameof(UserCreatedEvent);

    public override LocalizedString DisplayText => S["User Created Event"];
}
