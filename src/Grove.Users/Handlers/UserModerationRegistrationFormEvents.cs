using Microsoft.Extensions.Options;
using Grove.Users.Events;
using Grove.Users.Models;

namespace Grove.Users.Handlers;

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
