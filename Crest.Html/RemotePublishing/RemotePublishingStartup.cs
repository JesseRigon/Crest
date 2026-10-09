using Microsoft.Extensions.DependencyInjection;
using Crest.MetaWeblog;
using Crest.Modules;

namespace Crest.Html.RemotePublishing;

[RequireFeatures("Crest.RemotePublishing")]
public sealed class RemotePublishingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IMetaWeblogDriver, HtmlBodyMetaWeblogDriver>();
    }
}
