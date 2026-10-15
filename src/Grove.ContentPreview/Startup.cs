using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentPreview.Drivers;
using Grove.ContentPreview.Handlers;
using Grove.ContentPreview.Models;
using Grove.ContentTypes.Editors;
using Grove.Data.Migration;

namespace Grove.ContentPreview;

public sealed class Startup : Modules.StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();

        services.AddScoped<IContentDisplayDriver, ContentPreviewDriver>();

        // Preview Part
        services.AddContentPart<PreviewPart>()
            .AddHandler<PreviewPartHandler>();

        services.AddDataMigration<Migrations>();
        services.AddSingleton<IStartupFilter, PreviewStartupFilter>();
    }
}
