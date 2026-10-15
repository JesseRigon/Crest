using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Handlers;
using Grove.Contents.Services;
using Grove.Contents.ViewModels;
using Grove.ContentTypes.Editors;
using Grove.Data;
using Grove.Data.Migration;
using Grove.DisplayManagement;
using Grove.DisplayManagement.Handlers;
using Grove.Indexing;
using Grove.Liquid;
using Grove.Localization;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;
using Grove.Settings.Deployment;
using Grove.Taxonomies;
using Grove.Taxonomies.Drivers;
using Grove.Taxonomies.Fields;
using Grove.Taxonomies.GraphQL;
using Grove.Taxonomies.Handlers;
using Grove.Taxonomies.Indexing;
using Grove.Taxonomies.Liquid;
using Grove.Taxonomies.Models;
using Grove.Taxonomies.Services;
using Grove.Taxonomies.Settings;
using Grove.Taxonomies.ViewModels;

namespace Grove.Taxonomies;

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
        services.AddScoped<IJSLocalizer, TaxonomiesJSLocalizer>();

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

[Feature("Grove.Taxonomies.ContentsAdminList")]
public sealed class ContentsAdminListStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentsAdminListFilter, TaxonomyContentsAdminListFilter>();
        services.AddDisplayDriver<ContentOptionsViewModel, TaxonomyContentsAdminListDisplayDriver>();
        services.AddPageSource<AdminPages>();
        services.AddSiteDisplayDriver<TaxonomyContentsAdminListSettingsDisplayDriver>();
    }
}

[Feature("Grove.Taxonomies.ContentsAdminList")]
[RequireFeatures("Grove.Deployment")]
public sealed class ContentsAdminListDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<TaxonomyContentsAdminListSettings, ContentsAdminListDeploymentStartup>(S => S["Taxonomy Filters settings"], S => S["Exports the Taxonomy filters settings."]);
    }
}

[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class GraphQLStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<TaxonomyPart, TaxonomyPartQueryObjectType>();
        services.AddObjectGraphType<TaxonomyField, TaxonomyFieldQueryObjectType>();
    }
}
