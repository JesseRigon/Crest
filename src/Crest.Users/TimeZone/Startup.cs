using Microsoft.Extensions.DependencyInjection;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Users.Handlers;
using Crest.Users.Models;
using Crest.Users.TimeZone.Drivers;
using Crest.Users.TimeZone.Handlers;
using Crest.Users.TimeZone.Services;

namespace Crest.Users.TimeZone;

[Feature("Crest.Users.TimeZone")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IUserTimeZoneService, UserTimeZoneService>();
        services.AddScoped<IUserEventHandler, UserEventHandler>();
        services.AddScoped<ITimeZoneSelector, UserTimeZoneSelector>();
        services.AddDisplayDriver<User, UserTimeZoneDisplayDriver>();
    }
}
