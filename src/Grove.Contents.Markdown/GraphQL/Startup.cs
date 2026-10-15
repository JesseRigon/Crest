using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Contents.Markdown.Fields;
using Grove.Contents.Markdown.Models;
using Grove.Modules;

namespace Grove.Contents.Markdown.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<MarkdownBodyPart, MarkdownBodyQueryObjectType>();
        services.AddObjectGraphType<MarkdownField, MarkdownFieldQueryObjectType>();
    }
}
