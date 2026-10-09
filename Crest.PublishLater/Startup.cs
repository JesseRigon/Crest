using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Crest.BackgroundTasks;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Handlers;
using Crest.Data;
using Crest.Data.Migration;
using Crest.Modules;
using Crest.PublishLater.Drivers;
using Crest.PublishLater.Indexes;
using Crest.PublishLater.Models;
using Crest.PublishLater.Services;
using Crest.PublishLater.ViewModels;

namespace Crest.PublishLater;

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
