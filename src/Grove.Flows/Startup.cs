using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentTypes.Editors;
using Grove.Data.Migration;
using Grove.Flows.Drivers;
using Grove.Flows.Handlers;
using Grove.Flows.Indexing;
using Grove.Flows.Models;
using Grove.Flows.ViewModels;
using Grove.Indexing;
using Grove.DisplayManagement;
using Grove.Modules;

namespace Grove.Flows;

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
