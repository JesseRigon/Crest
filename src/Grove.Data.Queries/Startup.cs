using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Grove.Deployment;
using Grove.Display.Management.Handlers;
using Grove.Display.Liquid;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Data.Queries;
using Grove.Data.Queries.Services;
using Grove.Data.Queries.Deployment;
using Grove.Data.Queries.Liquid;
using Grove.Data.Queries.Recipes;
using Grove.Data.Queries.Structured;
using Grove.Data.Queries.Builtin;
using Grove.Infrastructure.Recipes;
using Grove.Scripting;
using Grove.Security.Permissions;

namespace Grove.Data.Queries;

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

[Feature("Grove.Data.Queries.Core")]
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
        services.AddScoped<Grove.Display.Management.Components.IDisplayDataSource, Services.QueryDisplayDataSource>();
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
