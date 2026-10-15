using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;
using Grove.XmlRpc.Services;

namespace Grove.XmlRpc;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IXmlRpcReader, XmlRpcReader>();
        services.AddScoped<IXmlRpcWriter, XmlRpcWriter>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
            name: "XmlRpc",
            areaName: "Grove.XmlRpc",
            pattern: "xmlrpc",
            defaults: new { controller = "Home", action = "Index" }
        );
    }
}

[Feature("Grove.RemotePublishing")]
public sealed class MetaWeblogStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
            name: "MetaWeblog",
            areaName: "Grove.XmlRpc",
            pattern: "xmlrpc/metaweblog",
            defaults: new { controller = "MetaWeblog", action = "Manifest" }
        );
    }
}
