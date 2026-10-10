using Microsoft.Extensions.DependencyInjection;
using Crest.Apis;
using Crest.Html.Models;
using Crest.Modules;

namespace Crest.Html.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<HtmlBodyPart, HtmlBodyQueryObjectType>();
    }
}
