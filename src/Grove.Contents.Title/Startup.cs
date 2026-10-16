using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Data.Indexing;
using Grove.Modules;
using Grove.Contents.Title.Drivers;
using Grove.Contents.Title.Indexing;
using Grove.Contents.Title.Models;
using Grove.Contents.Title.Services;
using Grove.Contents.Title.ViewModels;
using Grove.Workflows;
using Grove.Workflows.Extensions;

namespace Grove.Contents.Title;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<TitleFieldSetViewModel>();
        });

        // The Title field set: its data is its Title field; its behaviour, the system flows below.
        services.AddContentFieldSet<TitleFieldSet>()
            .UseDisplayDriver<TitleFieldSetDisplayDriver>();
        services.AddScoped<TitleBehaviour>();

        services.AddScoped<IContentFieldSetIndexHandler, TitleFieldSetIndexHandler>();

        services.AddDataMigration<Migrations>();
    }
}

/// <summary>The title's system flows and their activities (docs/content-engine.md › Behaviour is workflows).</summary>
[RequireFeatures(WorkflowsConstants.ContentsFeatureId)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkflowActivityProvider, Workflows.TitleWorkflowProvider>();
        services.AddScoped<IWorkflowFlowProvider, Workflows.TitleWorkflowProvider>();
        services.AddScoped<IWorkflowFieldSetHookAttachmentProvider, Workflows.TitleWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}
