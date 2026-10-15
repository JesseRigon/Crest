using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.ArchiveLater.Drivers;
using Grove.ArchiveLater.Indexes;
using Grove.ArchiveLater.Models;
using Grove.ArchiveLater.Services;
using Grove.ArchiveLater.ViewModels;
using Grove.BackgroundTasks;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Handlers;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Modules;

namespace Grove.ArchiveLater;

public sealed class Startup : StartupBase
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<ArchiveLaterPartViewModel>();
        });

        services
            .AddContentPart<ArchiveLaterPart>()
            .UseDisplayDriver<ArchiveLaterPartDisplayDriver>();

        services.AddDataMigration<Migrations>();

        services.AddScoped<ArchiveLaterPartIndexProvider>();
        services.AddScoped<IScopedIndexProvider>(sp => sp.GetRequiredService<ArchiveLaterPartIndexProvider>());
        services.AddScoped<IContentHandler>(sp => sp.GetRequiredService<ArchiveLaterPartIndexProvider>());

        services.AddSingleton<IBackgroundTask, ScheduledArchivingBackgroundTask>();
    }
}
