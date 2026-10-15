using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement.GraphQL;
using Grove.Modules;

namespace Grove.Contents.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentGraphQL();
    }
}
