using Microsoft.Extensions.Options;
using Grove.AuditTrail.Services.Models;

namespace Grove.Access.Users.AuditTrail.Registration;

public sealed class UserRegistrationAuditTrailEventConfiguration : IConfigureOptions<AuditTrailOptions>
{
    public const string User = nameof(User);
    public const string Registered = nameof(Registered);

    public void Configure(AuditTrailOptions options)
    {
        options.For<UserRegistrationAuditTrailEventConfiguration>(User, S => S["User"])
            .WithEvent(Registered, S => S["Registered"], S => S["A user was successfully registered."], true);
    }
}
