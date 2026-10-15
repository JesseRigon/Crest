using Microsoft.Extensions.DependencyInjection;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Users.Handlers;
using Grove.Users.Models;
using Grove.Users.TimeZone.Handlers;
using Grove.Users.TimeZone.Services;

namespace Grove.Users.TimeZone;

[Feature("Grove.Users.TimeZone")]
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
