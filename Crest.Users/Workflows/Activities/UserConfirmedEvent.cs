using Microsoft.Extensions.Localization;
using Crest.Users.Services;
using Crest.Workflows.Platform.Services;

namespace Crest.Users.Workflows.Activities;

public class UserConfirmedEvent : UserEvent
{
    public UserConfirmedEvent(
        IUserService userService,
        IWorkflowScriptEvaluator scriptEvaluator,
        IStringLocalizer<UserUpdatedEvent> stringLocalizer)
        : base(userService, scriptEvaluator, stringLocalizer)
    {
    }

    public override string Name
        => nameof(UserConfirmedEvent);

    public override LocalizedString DisplayText
        => S["User Confirmed Event"];
}
