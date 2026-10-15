using Grove.Display.Management.Document;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Display.Management.Implementation;
using Grove.Modules;
using Grove.Security.Permissions;
using YesSql;

namespace Grove.Diagnostics.MiniProfiler;

public sealed class Startup : StartupBase
{
    // Early in the pipeline to wrap all other middleware
    public override int Order => -500;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDocumentContributor<MiniProfilerDocumentContributor>();


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
