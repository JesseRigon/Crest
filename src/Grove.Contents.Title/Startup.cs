using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Data.Indexing;
using Grove.Modules;
using Grove.Contents.Title.Drivers;
using Grove.Contents.Title.Handlers;
using Grove.Contents.Title.Indexing;
using Grove.Contents.Title.Models;
using Grove.Contents.Title.ViewModels;

namespace Grove.Contents.Title;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<TitlePartViewModel>();
        });

        // Title Part
        services.AddContentPart<TitlePart>()
            .UseDisplayDriver<TitlePartDisplayDriver>()
            .AddHandler<TitlePartHandler>();

        services.AddScoped<IContentPartIndexHandler, TitlePartIndexHandler>();

        services.AddDataMigration<Migrations>();
    }
}
