using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Data.Protocols.Html.Models;
using Grove.Modules;

namespace Grove.Data.Protocols.Html.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<HtmlBodyFieldSet, HtmlBodyQueryObjectType>();
    }
}
