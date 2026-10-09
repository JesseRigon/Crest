using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.AuditTrail.Services.Models;
using Crest.Modules;
using Crest.Users.Events;

namespace Crest.Users.AuditTrail.Registration;

[RequireFeatures("Crest.Users.AuditTrail", UserConstants.Features.UserRegistration)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRegistrationFormEvents, UserRegistrationEventHandler>();
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, UserRegistrationAuditTrailEventConfiguration>();
    }
}
