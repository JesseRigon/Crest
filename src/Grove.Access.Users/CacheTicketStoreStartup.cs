using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;

namespace Grove.Access.Users.Authentication;

[Feature("Grove.Access.Users.Authentication.CacheTicketStore")]
public sealed class CacheTicketStoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureOptions<CookieAuthenticationOptionsConfigure>();
    }
}
