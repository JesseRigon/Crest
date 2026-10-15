using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.Modules;

namespace Grove.Contents.Deployment.Download;

[Feature("Grove.Contents.Deployment.Download")]
public sealed class DownloadStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentDisplayDriver, DownloadContentDriver>();
    }
}
