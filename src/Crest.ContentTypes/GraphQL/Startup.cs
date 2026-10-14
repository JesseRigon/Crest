using Microsoft.Extensions.DependencyInjection;
using Crest.ContentTypes.Editors;
using Crest.Modules;

namespace Crest.ContentTypes.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
    }
}
