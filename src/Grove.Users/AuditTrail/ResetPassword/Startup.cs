using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.AuditTrail.Services.Models;
using Grove.Modules;
using Grove.Users.Events;

namespace Grove.Users.AuditTrail.ResetPassword;

[RequireFeatures("Grove.Users.AuditTrail", UserConstants.Features.ResetPassword)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPasswordRecoveryFormEvents, UserResetPasswordEventHandler>();
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, UserResetPasswordAuditTrailEventConfiguration>();
    }
}
