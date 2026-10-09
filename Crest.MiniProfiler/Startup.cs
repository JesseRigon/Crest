using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Crest.DisplayManagement.Implementation;
using Crest.Modules;
using Crest.Security.Permissions;
using YesSql;

namespace Crest.MiniProfiler;

public sealed class Startup : StartupBase
{
    // Early in the pipeline to wrap all other middleware
    public override int Order => -500;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<MvcOptions>((options) =>
        {
            options.Filters.Add<MiniProfilerFilter>();
        });

        services.AddScoped<IShapeDisplayEvents, ShapeStep>();

        services.AddMiniProfiler();

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
