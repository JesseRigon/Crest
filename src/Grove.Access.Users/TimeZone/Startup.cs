using Microsoft.Extensions.DependencyInjection;
using Grove.Display.Management.Handlers;
using Grove.Modules;
using Grove.Access.Users.Handlers;
using Grove.Access.Users.Models;
using Grove.Access.Users.TimeZone.Handlers;
using Grove.Access.Users.TimeZone.Services;

namespace Grove.Access.Users.TimeZone;

[Feature("Grove.Access.Users.TimeZone")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IUserTimeZoneService, UserTimeZoneService>();
        services.AddScoped<IUserEventHandler, UserEventHandler>();
        services.AddScoped<ITimeZoneSelector, UserTimeZoneSelector>();
        services.AddApiContributor<Api.UserTimeZoneApi>();
    }
}
