using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;

namespace Crest.Users.Authentication;

[Feature("Crest.Users.Authentication.CacheTicketStore")]
public sealed class CacheTicketStoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureOptions<CookieAuthenticationOptionsConfigure>();
    }
}
