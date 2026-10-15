using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.AuditTrail.Services.Models;
using Grove.Modules;
using Grove.Access.Users.Events;

namespace Grove.Access.Users.AuditTrail.ResetPassword;

[RequireFeatures("Grove.Access.Users.AuditTrail", UserConstants.Features.ResetPassword)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPasswordRecoveryFormEvents, UserResetPasswordEventHandler>();
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, UserResetPasswordAuditTrailEventConfiguration>();
    }
}
