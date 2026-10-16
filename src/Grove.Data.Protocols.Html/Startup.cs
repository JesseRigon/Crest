using Grove.Contents.Management.Handlers;
using Grove.Workflows.Extensions;
using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Data.Protocols.Html.Drivers;
using Grove.Data.Protocols.Html.Indexing;
using Grove.Data.Protocols.Html.Models;
using Grove.Data.Protocols.Html.Settings;
using Grove.Data.Protocols.Html.ViewModels;
using Grove.Data.Indexing;
using Grove.Modules;

namespace Grove.Data.Protocols.Html;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHtmlServices();
        
        services.Configure<TemplateOptions>(o => o.MemberAccessStrategy.Register<HtmlBodyFieldSetViewModel>());

        // Body Part
        services.AddContentFieldSet<HtmlBodyFieldSet>()
            .UseDisplayDriver<HtmlBodyFieldSetDisplayDriver>();
        services.AddScoped<Services.HtmlBodyBehaviour>();
        services.AddContentFieldSetProjection<Projections.HtmlBodyProjection>();

        services.AddDataMigration<Migrations>();
        services.AddScoped<IContentFieldSetIndexHandler, HtmlBodyFieldSetIndexHandler>();
    }
}

/// <summary>The field set's system flows and their activities (docs/content-engine.md › Behaviour is workflows).</summary>
[RequireFeatures(Grove.Workflows.WorkflowsConstants.ContentsFeatureId)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<Grove.Workflows.IWorkflowActivityProvider, Workflows.HtmlBodyWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFlowProvider, Workflows.HtmlBodyWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFieldSetHookAttachmentProvider, Workflows.HtmlBodyWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}
