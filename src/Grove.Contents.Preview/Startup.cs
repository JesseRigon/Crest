using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Preview.Models;
using Grove.Data.Migration;

namespace Grove.Contents.Preview;

public sealed class Startup : Modules.StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The editor's preview pane renders the unsaved item through this API.
        services.AddApiContributor<Api.ContentPreviewApi>();

        // Preview Part
        services.AddContentFieldSet<PreviewFieldSet>();
        services.AddContentFieldSetProjection<Projections.PreviewProjection>();

        services.AddDataMigration<Migrations>();
    }
}
