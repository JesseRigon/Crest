using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Contents.BagParts.Drivers;
using Grove.Contents.BagParts.Handlers;
using Grove.Contents.BagParts.Indexing;
using Grove.Contents.BagParts.Models;
using Grove.Contents.BagParts.ViewModels;
using Grove.Data.Indexing;
using Grove.Display.Management;
using Grove.Modules;

namespace Grove.Contents.BagParts;

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
