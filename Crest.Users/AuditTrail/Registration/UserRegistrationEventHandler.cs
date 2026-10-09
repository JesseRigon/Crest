using Microsoft.AspNetCore.Identity;
using Crest.AuditTrail.Services;
using Crest.AuditTrail.Services.Models;
using Crest.Users.AuditTrail.Models;
using Crest.Users.Events;

namespace Crest.Users.AuditTrail.Registration;

public class UserRegistrationEventHandler : RegistrationFormEventsBase
{
    private readonly IAuditTrailManager _auditTrailManager;
    private readonly UserManager<IUser> _userManager;

    public UserRegistrationEventHandler(
        IAuditTrailManager auditTrailManager,
        UserManager<IUser> userManager)
    {
        _auditTrailManager = auditTrailManager;
        _userManager = userManager;
    }

    public override Task RegisteredAsync(IUser user)
        => RecordAuditTrailEventAsync(UserRegistrationAuditTrailEventConfiguration.Registered, user);

    private async Task RecordAuditTrailEventAsync(string name, IUser user)
    {
        var userName = user.UserName;

        var userId = await _userManager.GetUserIdAsync(user);

        var userEvent = new AuditTrailUserEvent
        {
            UserName = userName,
            UserId = userId,
        };

        await _auditTrailManager.RecordEventAsync(
            new AuditTrailContext<AuditTrailUserEvent>
            (
                name,
                UserRegistrationAuditTrailEventConfiguration.User,
                userId,
                userId,
                userName,
                userEvent
            ));
    }
}
