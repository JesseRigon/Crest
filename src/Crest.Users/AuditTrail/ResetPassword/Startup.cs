using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.AuditTrail.Services.Models;
using Crest.Modules;
using Crest.Users.Events;

namespace Crest.Users.AuditTrail.ResetPassword;

[RequireFeatures("Crest.Users.AuditTrail", UserConstants.Features.ResetPassword)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPasswordRecoveryFormEvents, UserResetPasswordEventHandler>();
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, UserResetPasswordAuditTrailEventConfiguration>();
    }
}
