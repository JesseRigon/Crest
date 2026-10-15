using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Feeds;
using Grove.Modules;

namespace Grove.Scripting;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddFeeds();
        services.AddApiContributor<Grove.Data.Feeds.Api.FeedsApi>();
    }
}
