using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.ArchiveLater.Drivers;
using Grove.Contents.ArchiveLater.Indexes;
using Grove.Contents.ArchiveLater.Models;
using Grove.Contents.ArchiveLater.Services;
using Grove.Contents.ArchiveLater.ViewModels;
using Grove.BackgroundTasks;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Handlers;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Modules;

namespace Grove.Contents.ArchiveLater;

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
