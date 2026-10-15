using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.Lists.Models;
using Grove.Modules;
using Grove.XmlRpc;

namespace Grove.Lists.RemotePublishing;

[RequireFeatures("Grove.RemotePublishing")]
public sealed class RemotePublishingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IXmlRpcHandler, MetaWeblogHandler>();
        services.AddContentPart<ListPart>()
            .UseDisplayDriver<ListMetaWeblogDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
            name: "RSD",
            areaName: "Grove.Lists",
            pattern: "xmlrpc/metaweblog/{contentItemId}/rsd",
            defaults: new { controller = "RemotePublishing", action = "Rsd" }
        );
    }
}
