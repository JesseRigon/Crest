using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.ContentPreview.Handlers;
using Grove.ContentPreview.Models;
using Grove.Data.Migration;

namespace Grove.ContentPreview;

public sealed class Startup : Modules.StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The editor's preview pane renders the unsaved item through this API.
        services.AddApiContributor<Api.ContentPreviewApi>();

        // Preview Part
        services.AddContentPart<PreviewPart>()
            .AddHandler<PreviewPartHandler>();

        services.AddDataMigration<Migrations>();
    }
}
