using Microsoft.Extensions.DependencyInjection;
using Grove.Feeds;
using Grove.Modules;

namespace Grove.Scripting;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddFeeds();
    }
}
