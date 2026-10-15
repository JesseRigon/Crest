using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.Liquid;
using Grove.Modules;
using Grove.Navigation;
using Grove.Queries;
using Grove.Queries.Services;
using Grove.Queries.Deployment;
using Grove.Queries.Liquid;
using Grove.Queries.Recipes;
using Grove.Queries.Structured;
using Grove.Queries.Builtin;
using Grove.Recipes;
using Grove.Scripting;
using Grove.Security.Permissions;

namespace Grove.Queries;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddApiContributor<Api.QueriesApi>();
        services.AddPermissionProvider<Permissions>();
    }
}

[Feature("Grove.Queries.Core")]
public sealed class CoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<Recipes.QueryStep>();
        services.AddDeployment<AllQueriesDeploymentSource, AllQueriesDeploymentStep>();
        services.AddSingleton<IGlobalMethodProvider, QueryGlobalMethodProvider>();

        services.Configure<TemplateOptions>(o =>
        {
            o.Scope.SetValue("Queries", new ObjectValue(new LiquidQueriesAccessor()));
            o.MemberAccessStrategy.Register<LiquidQueriesAccessor, FluidValue>(async (obj, name, context) =>
            {
                var liquidTemplateContext = (LiquidTemplateContext)context;
                var queryManager = liquidTemplateContext.Services.GetRequiredService<IQueryManager>();

                var query = await queryManager.GetQueryAsync(name);

                return FluidValue.Create(query, context.Options);
            });
        })
        .AddLiquidFilter<QueryFilter>("query");

        services.AddScoped<IQueryManager, DefaultQueryManager>();
        // Display trees read through the query system (docs/blazor-display.md decision 4).
        services.AddScoped<Grove.DisplayManagement.Components.IDisplayDataSource, Services.QueryDisplayDataSource>();
        services.AddScoped<IQueryCatalog, DefaultQueryCatalog>();

        services.AddScoped<IIndexTableCatalog, IndexTableCatalog>();
        services.AddQuerySource<StructuredQuerySource>(StructuredQuerySource.SourceName);
        services.AddScoped<IQueryHandler, StructuredQueryHandler>();

        services.AddQuerySource<SystemQuerySource>(SystemQuerySource.SourceName);
    }
}

[RequireFeatures("Grove.Deployment", "Grove.Contents")]
public class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<QueryBasedContentDeploymentSource, QueryBasedContentDeploymentStep>();
    }
}
