using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.Liquid;
using Crest.Modules;
using Crest.Navigation;
using Crest.Queries.Core;
using Crest.Queries.Core.Services;
using Crest.Queries.Deployment;
using Crest.Queries.Drivers;
using Crest.Queries.Liquid;
using Crest.Queries.Recipes;
using Crest.Queries.Structured;
using Crest.Queries.Builtin;
using Crest.Recipes;
using Crest.Scripting;
using Crest.Security.Permissions;

namespace Crest.Queries;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<AdminMenu>();
        services.AddDisplayDriver<Query, QueryDisplayDriver>();
        services.AddPermissionProvider<Permissions>();
    }
}

[Feature("Crest.Queries.Core")]
public sealed class CoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<Recipes.QueryStep>();
        services.AddDeployment<AllQueriesDeploymentSource, AllQueriesDeploymentStep, AllQueriesDeploymentStepDriver>();
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
        services.AddScoped<IQueryCatalog, DefaultQueryCatalog>();

        services.AddScoped<IIndexTableCatalog, IndexTableCatalog>();
        services.AddQuerySource<StructuredQuerySource>(StructuredQuerySource.SourceName);
        services.AddScoped<IQueryHandler, StructuredQueryHandler>();

        services.AddQuerySource<SystemQuerySource>(SystemQuerySource.SourceName);
    }
}

[RequireFeatures("Crest.Deployment", "Crest.Contents")]
public class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<QueryBasedContentDeploymentSource, QueryBasedContentDeploymentStep, QueryBasedContentDeploymentStepDriver>();
    }
}
