using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Contents.Flows.Models;
using Grove.Modules;

namespace Grove.Contents.Flows.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<BagPart, BagPartQueryObjectType>();

    }
}
