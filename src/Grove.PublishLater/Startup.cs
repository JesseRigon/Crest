using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.BackgroundTasks;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Handlers;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Modules;
using Grove.PublishLater.Drivers;
using Grove.PublishLater.Indexes;
using Grove.PublishLater.Models;
using Grove.PublishLater.Services;
using Grove.PublishLater.ViewModels;

namespace Grove.PublishLater;

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
