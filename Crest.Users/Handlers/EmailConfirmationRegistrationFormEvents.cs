using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Crest.Users.Events;
using Crest.Users.Models;
using Crest.Users.Services;

namespace Crest.Users.Handlers;

internal sealed class EmailConfirmationRegistrationFormEvents : RegistrationFormEventsBase
{
    private readonly IOptionsMonitor<RegistrationOptions> _registrationOptions;
    private readonly UserManager<IUser> _userManager;
    private readonly UserEmailService _userEmailConfirmationService;

    public EmailConfirmationRegistrationFormEvents(
        IOptionsMonitor<RegistrationOptions> registrationOptions,
        UserManager<IUser> userManager,
        UserEmailService userEmailConfirmationService)
    {
        _registrationOptions = registrationOptions;
        _userManager = userManager;
        _userEmailConfirmationService = userEmailConfirmationService;
    }

    public override async Task RegisteringAsync(UserRegisteringContext context)
    {
        if (!_registrationOptions.CurrentValue.UsersMustValidateEmail || await _userManager.IsEmailConfirmedAsync(context.User))
        {
            return;
        }

        context.CancelSignIn = true;
        await _userEmailConfirmationService.SendEmailConfirmationAsync(context.User);
    }
}
