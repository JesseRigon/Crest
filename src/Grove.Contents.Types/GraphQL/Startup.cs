using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;

namespace Grove.Contents.Types.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
    }
}
