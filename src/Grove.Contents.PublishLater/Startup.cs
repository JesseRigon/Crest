using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.BackgroundTasks;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Handlers;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Modules;
using Grove.Contents.PublishLater.Drivers;
using Grove.Contents.PublishLater.Indexes;
using Grove.Contents.PublishLater.Models;
using Grove.Contents.PublishLater.Services;
using Grove.Contents.PublishLater.ViewModels;

namespace Grove.Contents.PublishLater;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<PublishLaterPartViewModel>();
        });

        services
            .AddContentPart<PublishLaterPart>()
            .UseDisplayDriver<PublishLaterPartDisplayDriver>();

        services.AddDataMigration<Migrations>();

        services.AddScoped<PublishLaterPartIndexProvider>();
        services.AddScoped<IScopedIndexProvider>(sp => sp.GetRequiredService<PublishLaterPartIndexProvider>());
        services.AddScoped<IContentHandler>(sp => sp.GetRequiredService<PublishLaterPartIndexProvider>());

        services.AddSingleton<IBackgroundTask, ScheduledPublishingBackgroundTask>();
    }
}
