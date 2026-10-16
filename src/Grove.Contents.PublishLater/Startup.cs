using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.BackgroundTasks;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Handlers;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Modules;
using Grove.Workflows.Extensions;
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
            o.MemberAccessStrategy.Register<PublishLaterFieldSetViewModel>();
        });

        services
            .AddContentFieldSet<PublishLaterFieldSet>()
            .UseDisplayDriver<PublishLaterFieldSetDisplayDriver>();

        services.AddDataMigration<Migrations>();

        services.AddScoped<PublishLaterIndexProvider>();
        services.AddScoped<IScopedIndexProvider>(sp => sp.GetRequiredService<PublishLaterIndexProvider>());
        services.AddScoped<IContentHandler>(sp => sp.GetRequiredService<PublishLaterIndexProvider>());

        services.AddSingleton<IBackgroundTask, ScheduledPublishingBackgroundTask>();
    }
}

/// <summary>Scheduled publishing's system flow and its activity (docs/content-engine.md › Behaviour is workflows).</summary>
[RequireFeatures(Grove.Workflows.WorkflowsConstants.ContentsFeatureId)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<Services.PublishLaterBehaviour>();
        services.AddScoped<Grove.Workflows.IWorkflowActivityProvider, Workflows.PublishLaterWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFlowProvider, Workflows.PublishLaterWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFieldSetHookAttachmentProvider, Workflows.PublishLaterWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}
