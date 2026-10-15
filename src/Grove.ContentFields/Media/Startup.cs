using Microsoft.Extensions.DependencyInjection;
using Grove.DisplayManagement;
using Grove.Modules;

namespace Grove.ContentFields.Media;

[RequireFeatures("Grove.Media")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShapeTableProvider<MediaShapes>();
    }
}
