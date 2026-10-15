using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management.GraphQL;
using Grove.Modules;

namespace Grove.Contents.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentGraphQL();
    }
}
