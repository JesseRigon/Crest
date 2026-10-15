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
using Grove.Contents.Taxonomies.Fields;
using Grove.Contents.Taxonomies.GraphQL;
using Grove.Contents.Taxonomies.Handlers;
using Grove.Contents.Taxonomies.Indexing;
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
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<TaxonomyField>();
            o.MemberAccessStrategy.Register<TaxonomyPartViewModel>();
            o.MemberAccessStrategy.Register<TermPartViewModel>();
            o.MemberAccessStrategy.Register<DisplayTaxonomyFieldViewModel>();
            o.MemberAccessStrategy.Register<DisplayTaxonomyFieldTagsViewModel>();
        })
        .AddLiquidFilter<InheritedTermsFilter>("inherited_terms")
        .AddLiquidFilter<TaxonomyTermsFilter>("taxonomy_terms");

        services.AddDataMigration<Migrations>();
        services.AddShapeTableProvider<TermShapes>();
        services.AddPermissionProvider<Permissions>();
        services.AddApiContributor<Api.TaxonomiesApi>();

        // Taxonomy Part
        services.AddContentPart<TaxonomyPart>()
            .UseDisplayDriver<TaxonomyPartDisplayDriver>()
            .AddHandler<TaxonomyPartHandler>();

        // Taxonomy Field
        services.AddContentField<TaxonomyField>()
            .UseDisplayDriver<TaxonomyFieldDisplayDriver>(d => !string.Equals(d, "Tags", StringComparison.OrdinalIgnoreCase))
            .AddHandler<TaxonomyFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, TaxonomyFieldIndexHandler>();

        // Taxonomy Tags Display Mode and Editor.
        services.AddContentField<TaxonomyField>()
            .UseDisplayDriver<TaxonomyFieldTagsDisplayDriver>(d => string.Equals(d, "Tags", StringComparison.OrdinalIgnoreCase));


        services.AddScopedIndexProvider<TaxonomyIndexProvider>();

        // Terms.
        services.AddContentPart<TermPart>();
        services.AddScoped<IContentHandler, TermPartContentHandler>();
        services.AddScoped<IContentDisplayDriver, TermPartContentDriver>();

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
            "Taxonomy Filters",
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
        services.AddObjectGraphType<TaxonomyPart, TaxonomyPartQueryObjectType>();
        services.AddObjectGraphType<TaxonomyField, TaxonomyFieldQueryObjectType>();
    }
}
