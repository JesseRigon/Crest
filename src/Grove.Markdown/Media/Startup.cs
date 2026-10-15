using Microsoft.Extensions.DependencyInjection;
using Grove.DisplayManagement;
using Grove.Modules;

namespace Grove.Markdown.Media;

[RequireFeatures("Grove.Media")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShapeTableProvider<MediaShapes>();
    }
}
