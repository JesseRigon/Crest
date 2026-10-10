using Microsoft.Extensions.DependencyInjection;
using Crest.Feeds;
using Crest.Modules;

namespace Crest.Scripting;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddFeeds();
    }
}
