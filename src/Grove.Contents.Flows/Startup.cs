using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Contents.Flows.Drivers;
using Grove.Contents.Flows.Handlers;
using Grove.Contents.Flows.Indexing;
using Grove.Contents.Flows.Models;
using Grove.Contents.Flows.ViewModels;
using Grove.Data.Indexing;
using Grove.Display.Management;
using Grove.Modules;

namespace Grove.Contents.Flows;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<BagPartViewModel>();
        });

        services.AddScoped<IContentPartIndexHandler, BagPartDocumentIndexHandler>();

        // Bag Part
        services.AddContentPart<BagPart>()
            .UseDisplayDriver<BagPartDisplayDriver>()
            .AddHandler<BagPartHandler>();
        services.AddScoped<IContentPartIndexHandler, BagPartIndexHandler>();

        services.AddDataMigration<Migrations>();
    }
}
