using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;
using Grove.Scripting;

namespace Grove.Contents.Scripting;

[RequireFeatures("Grove.Scripting")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IGlobalMethodProvider, ContentMethodsProvider>();
        services.AddSingleton<IGlobalMethodProvider, UrlMethodsProvider>();
    }
}
