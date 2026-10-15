using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentTypes.Editors;
using Grove.Data.Migration;
using Grove.Indexing;
using Grove.Modules;
using Grove.Title.Drivers;
using Grove.Title.Handlers;
using Grove.Title.Indexing;
using Grove.Title.Models;
using Grove.Title.ViewModels;

namespace Grove.Title;

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

        services.AddDataMigration<Migrations>();
    }
}
