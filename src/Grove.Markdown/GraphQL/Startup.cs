using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Markdown.Fields;
using Grove.Markdown.Models;
using Grove.Modules;

namespace Grove.Markdown.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<MarkdownBodyPart, MarkdownBodyQueryObjectType>();
        services.AddObjectGraphType<MarkdownField, MarkdownFieldQueryObjectType>();
    }
}
