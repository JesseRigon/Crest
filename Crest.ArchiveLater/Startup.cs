using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Crest.ArchiveLater.Drivers;
using Crest.ArchiveLater.Indexes;
using Crest.ArchiveLater.Models;
using Crest.ArchiveLater.Services;
using Crest.ArchiveLater.ViewModels;
using Crest.BackgroundTasks;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Handlers;
using Crest.Data;
using Crest.Data.Migration;
using Crest.Modules;

namespace Crest.ArchiveLater;

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
