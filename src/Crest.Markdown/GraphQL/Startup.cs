using Microsoft.Extensions.DependencyInjection;
using Crest.Apis;
using Crest.Markdown.Fields;
using Crest.Markdown.Models;
using Crest.Modules;

namespace Crest.Markdown.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<MarkdownBodyPart, MarkdownBodyQueryObjectType>();
        services.AddObjectGraphType<MarkdownField, MarkdownFieldQueryObjectType>();
    }
}
