using Microsoft.Extensions.DependencyInjection;
using Grove.MetaWeblog;
using Grove.Modules;

namespace Grove.Markdown.RemotePublishing;

[RequireFeatures("Grove.RemotePublishing")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IMetaWeblogDriver, MarkdownBodyMetaWeblogDriver>();
    }
}
