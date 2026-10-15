using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.ContentManagement.GraphQL.Queries.Types;
using Grove.Flows.Models;
using Grove.Modules;

namespace Grove.Flows.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<BagPart, BagPartQueryObjectType>();

    }
}
