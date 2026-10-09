using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.Lists.Models;
using Crest.Modules;
using Crest.XmlRpc;

namespace Crest.Lists.RemotePublishing;

[RequireFeatures("Crest.RemotePublishing")]
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
            areaName: "Crest.Lists",
            pattern: "xmlrpc/metaweblog/{contentItemId}/rsd",
            defaults: new { controller = "RemotePublishing", action = "Rsd" }
        );
    }
}
