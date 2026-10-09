using Microsoft.Extensions.DependencyInjection;
using Crest.DisplayManagement;
using Crest.Modules;

namespace Crest.Markdown.Media;

[RequireFeatures("Crest.Media")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShapeTableProvider<MediaShapes>();
    }
}
