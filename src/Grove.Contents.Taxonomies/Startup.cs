using Grove.Workflows.Extensions;
using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Services;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Display.Management;
using Grove.Display.Management.Handlers;
using Grove.Data.Indexing;
using Grove.Display.Liquid;
using Grove.Data.Localization;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;
using Grove.Infrastructure.Settings.Deployment;
using Grove.Contents.Taxonomies;
using Grove.Contents.Taxonomies.Drivers;
using Grove.Contents.Taxonomies.GraphQL;
using Grove.Contents.Taxonomies.Liquid;
using Grove.Contents.Taxonomies.Models;
using Grove.Contents.Taxonomies.Services;
using Grove.Contents.Taxonomies.Settings;
using Grove.Contents.Taxonomies.ViewModels;

namespace Grove.Contents.Taxonomies;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The taxonomy displays its term tree itself.
        services.Configure<Grove.Contents.Management.EmbeddedContentDisplayOptions>(options =>
            options.DisplayedElsewhere.Add($"Taxonomy.{Models.TaxonomyFieldSet.TermsEmbedding}"));

        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<TaxonomyFieldSetViewModel>();
            o.MemberAccessStrategy.Register<TermFieldSetViewModel>();
        })
        .AddLiquidFilter<InheritedTermsFilter>("inherited_terms")
        .AddLiquidFilter<TaxonomyTermsFilter>("taxonomy_terms");

        services.AddDataMigration<Migrations>();
        services.AddShapeTableProvider<TermShapes>();
        services.AddPermissionProvider<Permissions>();

        // Tags added by name from an option list field on a taxonomy's terms.
        services.AddScoped<Grove.Contents.Management.IEmbeddedContentItemFactory, TaxonomyTermFactory>();

        // Taxonomy Part
        // The Taxonomy field set: its data is its field; its behaviour, the system flows below.
        services.AddContentFieldSet<TaxonomyFieldSet>()
            .UseDisplayDriver<TaxonomyFieldSetDisplayDriver>();
        services.AddScoped<Services.TaxonomyBehaviour>();

        // Terms.
        services.AddContentFieldSet<TermFieldSet>();
        services.AddContentProjection<Projections.TermChildrenProjection>();
        services.AddScoped<IContentDisplayDriver, TermFieldSetContentDriver>();

        services.AddScoped<IContentsTaxonomyListQueryService, DefaultContentsTaxonomyListQueryService>();
    }
}

[Feature("Grove.Contents.Taxonomies.ContentsAdminList")]
public sealed class ContentsAdminListStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentItemListFilter, TaxonomyContentItemListFilter>();
        services.AddSettingsGroup<TaxonomyContentsAdminListSettings>(
            TaxonomyContentsAdminListSettings.GroupId,
            "Category Filters",
            Permissions.ManageTaxonomies,
            group => group.InMenu("settings", entryId: "taxonomyfilters"));
    }
}

[Feature("Grove.Contents.Taxonomies.ContentsAdminList")]
[RequireFeatures("Grove.Deployment")]
public sealed class ContentsAdminListDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<TaxonomyContentsAdminListSettings>();
    }
}

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class GraphQLStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<TaxonomyFieldSet, TaxonomyFieldSetQueryObjectType>();
    }
}

/// <summary>The field set's system flows and their activities (docs/content-engine.md › Behaviour is workflows).</summary>
[RequireFeatures(Grove.Workflows.WorkflowsConstants.ContentsFeatureId)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<Grove.Workflows.IWorkflowActivityProvider, Workflows.TaxonomyWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFlowProvider, Workflows.TaxonomyWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFieldSetHookAttachmentProvider, Workflows.TaxonomyWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}
