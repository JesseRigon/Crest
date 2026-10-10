using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.Modules;

namespace Crest.Contents.Deployment.Download;

[Feature("Crest.Contents.Deployment.Download")]
public sealed class DownloadStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDisplayDriver, DownloadContentDriver>();
    }
}
