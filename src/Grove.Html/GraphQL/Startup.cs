using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Html.Models;
using Grove.Modules;

namespace Grove.Html.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<HtmlBodyPart, HtmlBodyQueryObjectType>();
    }
}
