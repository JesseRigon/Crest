using Grove.Contents.Fields;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Markdown.Handlers;
using Grove.Workflows.Extensions;
using Fluid;
using Markdig;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Environment.Shell.Configuration;
using Grove.Data.Indexing;
using Grove.Display.Liquid;
using Grove.Contents.Markdown.Drivers;
using Grove.Contents.Markdown.Fields;
using Grove.Contents.Markdown.Filters;
using Grove.Contents.Markdown.Indexing;
using Grove.Contents.Markdown.Models;
using Grove.Contents.Markdown.Services;
using Grove.Contents.Markdown.Settings;
using Grove.Contents.Markdown.ViewModels;
using Grove.Modules;

namespace Grove.Contents.Markdown;

public sealed class Startup : StartupBase
{
    private const string DefaultMarkdownExtensions = "nohtml+advanced";

    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<MarkdownBodyFieldSetViewModel>();
            o.MemberAccessStrategy.Register<MarkdownFieldViewModel>();
        })
        .AddLiquidFilter<Markdownify>("markdownify");

        // Markdown Part
        services.AddContentFieldSet<MarkdownBodyFieldSet>()
            .UseDisplayDriver<MarkdownBodyFieldSetDisplayDriver>();
        services.AddScoped<Services.MarkdownBodyBehaviour>();
        services.AddContentFieldSetProjection<Projections.MarkdownBodyProjection>();

        services.AddDataMigration<Migrations>();
        services.AddScoped<IContentFieldSetIndexHandler, MarkdownBodyFieldSetIndexHandler>();

        // Markdown Field
        services.AddFieldType<MarkdownField, Grove.Contents.Markdown.Settings.MarkdownFieldSettings>()
            .UseDisplayDriver<MarkdownFieldDisplayDriver>()
            .AddHandler<MarkdownFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, MarkdownFieldIndexHandler>();

        services.AddOptions<MarkdownPipelineOptions>();
        services.ConfigureMarkdownPipeline((pipeline) =>
        {
            var extensions = _shellConfiguration.GetValue("Grove_Markdown:Extensions", DefaultMarkdownExtensions);
            pipeline.Configure(extensions);
        });

        services.AddScoped<IMarkdownService, DefaultMarkdownService>();
    }
}

/// <summary>The field set's system flows and their activities (docs/content-engine.md › Behaviour is workflows).</summary>
[RequireFeatures(Grove.Workflows.WorkflowsConstants.ContentsFeatureId)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<Grove.Workflows.IWorkflowActivityProvider, Workflows.MarkdownBodyWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFlowProvider, Workflows.MarkdownBodyWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFieldSetHookAttachmentProvider, Workflows.MarkdownBodyWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}
