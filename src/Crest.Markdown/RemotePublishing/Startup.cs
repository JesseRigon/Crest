using Microsoft.Extensions.DependencyInjection;
using Crest.MetaWeblog;
using Crest.Modules;

namespace Crest.Markdown.RemotePublishing;

[RequireFeatures("Crest.RemotePublishing")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IMetaWeblogDriver, MarkdownBodyMetaWeblogDriver>();
    }
}
