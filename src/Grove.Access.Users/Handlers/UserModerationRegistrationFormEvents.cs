using Microsoft.Extensions.Options;
using Grove.Access.Users.Events;
using Grove.Access.Users.Models;

namespace Grove.Access.Users.Handlers;

internal sealed class UserModerationRegistrationFormEvents : RegistrationFormEventsBase
{
    private readonly IOptionsMonitor<RegistrationOptions> _registrationOptions;

    public UserModerationRegistrationFormEvents(IOptionsMonitor<RegistrationOptions> registrationOptions)
    {
        _registrationOptions = registrationOptions;
    }

    public override Task RegisteringAsync(UserRegisteringContext context)
    {
        if (context.User is User user &&
            _registrationOptions.CurrentValue.UsersAreModerated &&
            !user.IsEnabled)
        {
            context.CancelSignIn = true;
        }

        return Task.CompletedTask;
    }
}
