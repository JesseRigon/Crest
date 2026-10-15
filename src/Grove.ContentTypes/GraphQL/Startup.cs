using Microsoft.Extensions.DependencyInjection;
using Grove.ContentTypes.Editors;
using Grove.Modules;

namespace Grove.ContentTypes.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
    }
}
