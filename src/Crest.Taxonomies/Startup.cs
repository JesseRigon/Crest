using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Crest.Apis;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Handlers;
using Crest.Contents.Services;
using Crest.Contents.ViewModels;
using Crest.ContentTypes.Editors;
using Crest.Data;
using Crest.Data.Migration;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Handlers;
using Crest.Indexing;
using Crest.Liquid;
using Crest.Localization;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;
using Crest.Settings.Deployment;
using Crest.Taxonomies;
using Crest.Taxonomies.Drivers;
using Crest.Taxonomies.Fields;
using Crest.Taxonomies.GraphQL;
using Crest.Taxonomies.Handlers;
using Crest.Taxonomies.Indexing;
using Crest.Taxonomies.Liquid;
using Crest.Taxonomies.Models;
using Crest.Taxonomies.Services;
using Crest.Taxonomies.Settings;
using Crest.Taxonomies.ViewModels;

namespace Crest.Taxonomies;

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

        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, TaxonomyFieldSettingsDriver>();
        services.AddScoped<IContentFieldIndexHandler, TaxonomyFieldIndexHandler>();

        // Taxonomy Tags Display Mode and Editor.
        services.AddContentField<TaxonomyField>()
            .UseDisplayDriver<TaxonomyFieldTagsDisplayDriver>(d => string.Equals(d, "Tags", StringComparison.OrdinalIgnoreCase));

        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, TaxonomyFieldTagsEditorSettingsDriver>();

        services.AddScopedIndexProvider<TaxonomyIndexProvider>();

        // Terms.
        services.AddContentPart<TermPart>();
        services.AddScoped<IContentHandler, TermPartContentHandler>();
        services.AddScoped<IContentDisplayDriver, TermPartContentDriver>();

        services.AddScoped<IContentsTaxonomyListQueryService, DefaultContentsTaxonomyListQueryService>();
    }
}

[Feature("Crest.Taxonomies.ContentsAdminList")]
public sealed class ContentsAdminListStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentsAdminListFilter, TaxonomyContentsAdminListFilter>();
        services.AddDisplayDriver<ContentOptionsViewModel, TaxonomyContentsAdminListDisplayDriver>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddSiteDisplayDriver<TaxonomyContentsAdminListSettingsDisplayDriver>();
    }
}

[Feature("Crest.Taxonomies.ContentsAdminList")]
[RequireFeatures("Crest.Deployment")]
public sealed class ContentsAdminListDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<TaxonomyContentsAdminListSettings, ContentsAdminListDeploymentStartup>(S => S["Taxonomy Filters settings"], S => S["Exports the Taxonomy filters settings."]);
    }
}

[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class GraphQLStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<TaxonomyPart, TaxonomyPartQueryObjectType>();
        services.AddObjectGraphType<TaxonomyField, TaxonomyFieldQueryObjectType>();
    }
}
