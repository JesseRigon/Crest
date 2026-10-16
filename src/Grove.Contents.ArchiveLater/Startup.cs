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
using Grove.Workflows.Extensions;

namespace Grove.Contents.ArchiveLater;

public sealed class Startup : StartupBase
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<ArchiveLaterFieldSetViewModel>();
        });

        services
            .AddContentFieldSet<ArchiveLaterFieldSet>()
            .UseDisplayDriver<ArchiveLaterFieldSetDisplayDriver>();

        services.AddDataMigration<Migrations>();

        services.AddScoped<ArchiveLaterIndexProvider>();
        services.AddScoped<IScopedIndexProvider>(sp => sp.GetRequiredService<ArchiveLaterIndexProvider>());
        services.AddScoped<IContentHandler>(sp => sp.GetRequiredService<ArchiveLaterIndexProvider>());

        services.AddSingleton<IBackgroundTask, ScheduledArchivingBackgroundTask>();
    }
}

/// <summary>Scheduled archiving's system flow and its activity (docs/content-engine.md › Behaviour is workflows).</summary>
[RequireFeatures(Grove.Workflows.WorkflowsConstants.ContentsFeatureId)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<Services.ArchiveLaterBehaviour>();
        services.AddScoped<Grove.Workflows.IWorkflowActivityProvider, Workflows.ArchiveLaterWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFlowProvider, Workflows.ArchiveLaterWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFieldSetHookAttachmentProvider, Workflows.ArchiveLaterWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}
