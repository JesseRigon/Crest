using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentPreview.Drivers;
using Crest.ContentPreview.Handlers;
using Crest.ContentPreview.Models;
using Crest.ContentPreview.Settings;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;

namespace Crest.ContentPreview;

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
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, PreviewPartSettingsDisplayDriver>();
        services.AddSingleton<IStartupFilter, PreviewStartupFilter>();
    }
}
