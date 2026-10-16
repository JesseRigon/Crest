using Grove.Display.Management.Document;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Profiling;
using StackExchange.Profiling.Storage;
using Grove.Display.Management.Implementation;
using Grove.Modules;
using Grove.Security.Permissions;
using YesSql;

namespace Grove.Diagnostics.MiniProfiler;

public sealed class Startup : StartupBase
{
    /// <summary>How long a request's profile is kept for the profiler's UI.</summary>
    public static readonly TimeSpan ProfileRetention = TimeSpan.FromMinutes(5);

    // Early in the pipeline to wrap all other middleware
    public override int Order => -500;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDocumentContributor<MiniProfilerDocumentContributor>();

        services.AddMiniProfiler();

        // Every request is profiled, SQL text included (about 16 KB a request), and the profiles
        // live in the tenant's memory cache. The library keeps them 30 minutes, so the store grows
        // with traffic; a short retention bounds it to recent requests, which is what the
        // profiler's UI shows anyway.
        services.AddOptions<MiniProfilerOptions>()
            .Configure<IMemoryCache>((options, cache) => options.Storage = new MemoryCacheStorage(cache, ProfileRetention));

        services.AddPermissionProvider<Permissions>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseMiniProfiler();

        var store = serviceProvider.GetService<IStore>();

        // Wrap the current factory with MiniProfilerConnectionFactory.
        store.Configuration.ConnectionFactory = new MiniProfilerConnectionFactory(store.Configuration.ConnectionFactory);
    }
}
