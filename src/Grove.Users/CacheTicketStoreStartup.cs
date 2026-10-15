using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;

namespace Grove.Users.Authentication;

[Feature("Grove.Users.Authentication.CacheTicketStore")]
public sealed class CacheTicketStoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureOptions<CookieAuthenticationOptionsConfigure>();
    }
}
