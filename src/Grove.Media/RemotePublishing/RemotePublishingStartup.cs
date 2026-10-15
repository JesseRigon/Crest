using Microsoft.Extensions.DependencyInjection;
using Grove.MetaWeblog;
using Grove.Modules;

namespace Grove.Media.RemotePublishing;

[RequireFeatures("Grove.RemotePublishing")]
public sealed class RemotePublishingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IMetaWeblogDriver, MediaMetaWeblogDriver>();
    }
}
