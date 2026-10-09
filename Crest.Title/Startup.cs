using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.Indexing;
using Crest.Modules;
using Crest.Title.Drivers;
using Crest.Title.Handlers;
using Crest.Title.Indexing;
using Crest.Title.Models;
using Crest.Title.Settings;
using Crest.Title.ViewModels;

namespace Crest.Title;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<TitlePartViewModel>();
            o.MemberAccessStrategy.Register<TitlePartSettingsViewModel>();
        });

        // Title Part
        services.AddContentPart<TitlePart>()
            .UseDisplayDriver<TitlePartDisplayDriver>()
            .AddHandler<TitlePartHandler>();

        services.AddScoped<IContentPartIndexHandler, TitlePartIndexHandler>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, TitlePartSettingsDisplayDriver>();

        services.AddDataMigration<Migrations>();
    }
}
