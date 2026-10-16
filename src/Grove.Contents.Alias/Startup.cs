using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Alias.Indexes;
using Grove.Contents.Alias.Indexing;
using Grove.Contents.Alias.Models;
using Grove.Contents.Alias.Services;
using Grove.Contents.Alias.Settings;
using Grove.Contents.Alias.ViewModels;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Handlers;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Display.Management;
using Grove.Data.Indexing;
using Grove.Display.Liquid;
using Grove.Modules;
using Grove.Workflows.Extensions;
using YesSql;

namespace Grove.Contents.Alias;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<AliasFieldSetViewModel>();

            o.MemberAccessStrategy.Register<LiquidContentAccessor, LiquidPropertyAccessor>("Alias", (obj, context) =>
            {
                var liquidTemplateContext = (LiquidTemplateContext)context;

                return new LiquidPropertyAccessor(liquidTemplateContext, async (alias, context) =>
                {
                    var session = context.Services.GetRequiredService<ISession>();

#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
                    var contentItem = await session.Query<ContentItem, AliasIndex>(x =>
                        x.Published && x.Alias == alias.ToLowerInvariant()).FirstOrDefaultAsync();
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons

                    if (contentItem == null)
                    {
                        return NilValue.Instance;
                    }

                    var contentManager = context.Services.GetRequiredService<IContentManager>();
                    contentItem = await contentManager.LoadAsync(contentItem);

                    return new ObjectValue(contentItem);
                });
            });
        });

        services.AddScoped<AliasIndexProvider>();
        services.AddScoped<IScopedIndexProvider>(sp => sp.GetRequiredService<AliasIndexProvider>());
        services.AddScoped<IContentHandler>(sp => sp.GetRequiredService<AliasIndexProvider>());

        services.AddDataMigration<Migrations>();
        services.AddScoped<IContentHandleProvider, AliasFieldSetContentHandleProvider>();

        // The Alias field set: its data is its Alias field; its behaviour, the system flows below.
        services.AddContentFieldSet<AliasFieldSet>();
        services.AddScoped<AliasBehaviour>();

        services.AddScoped<IContentFieldSetIndexHandler, AliasFieldSetIndexHandler>();
    }
}

[RequireFeatures("Grove.Contents")]
public sealed class ContentAliasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShapeTableProvider<ContentAliasShapeTableProvider>();
    }
}

/// <summary>The alias's system flows and their activities (docs/content-engine.md › Behaviour is workflows).</summary>
[RequireFeatures(Grove.Workflows.WorkflowsConstants.ContentsFeatureId)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<Grove.Workflows.IWorkflowActivityProvider, Workflows.AliasWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFlowProvider, Workflows.AliasWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFieldSetHookAttachmentProvider, Workflows.AliasWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}
