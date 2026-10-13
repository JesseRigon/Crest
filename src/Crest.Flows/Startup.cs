using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.Flows.Drivers;
using Crest.Flows.Handlers;
using Crest.Flows.Indexing;
using Crest.Flows.Models;
using Crest.Flows.Settings;
using Crest.Flows.ViewModels;
using Crest.Indexing;
using Crest.DisplayManagement;
using Crest.Modules;

namespace Crest.Flows;

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
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, BagPartSettingsDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, BagPartBlocksEditorSettingsDriver>();
        services.AddScoped<IContentPartIndexHandler, BagPartIndexHandler>();

        // The content cards the BagPart editor is built from (moved here from the retired
        // Widgets module, docs/blazor-display.md rulings 2026-10-12).
        services.AddShapeTableProvider<ContentCardShapes>();

        services.AddDataMigration<Migrations>();

        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}
