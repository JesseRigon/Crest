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
using Crest.Modules;

namespace Crest.Flows;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<BagPartViewModel>();
            o.MemberAccessStrategy.Register<FlowPartViewModel>();
            o.MemberAccessStrategy.Register<FlowMetadata>();
            o.MemberAccessStrategy.Register<FlowPart>();
        });

        services.AddScoped<IContentPartIndexHandler, BagPartDocumentIndexHandler>();

        // Flow Part
        services.AddContentPart<FlowPart>()
            .UseDisplayDriver<FlowPartDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, FlowPartSettingsDisplayDriver>();
        services.AddScoped<IContentPartIndexHandler, FlowPartIndexHandler>();

        services.AddScoped<IContentDisplayDriver, FlowMetadataDisplayDriver>();

        // Bag Part
        services.AddContentPart<BagPart>()
            .UseDisplayDriver<BagPartDisplayDriver>()
            .AddHandler<BagPartHandler>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, BagPartSettingsDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, BagPartBlocksEditorSettingsDriver>();
        services.AddScoped<IContentPartIndexHandler, BagPartIndexHandler>();

        services.AddContentPart<FlowMetadata>();

        services.AddDataMigration<Migrations>();

        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}
