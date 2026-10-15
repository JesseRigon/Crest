using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.AuditTrail.Services.Models;
using Grove.Modules;
using Grove.Users.Events;

namespace Grove.Users.AuditTrail.Registration;

[RequireFeatures("Grove.Users.AuditTrail", UserConstants.Features.UserRegistration)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRegistrationFormEvents, UserRegistrationEventHandler>();
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, UserRegistrationAuditTrailEventConfiguration>();
    }
}
