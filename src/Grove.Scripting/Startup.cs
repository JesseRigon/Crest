using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;
using Grove.Scripting.JavaScript;
using Grove.Scripting.Providers;

namespace Grove.Scripting;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddJavaScriptEngine();
        services.AddSingleton<IGlobalMethodProvider, LogProvider>();
        services.AddSingleton<IGlobalMethodProvider, ProtectDataProvider>();
        services.AddSingleton<IGlobalMethodProvider, DataProtectionMethods>();
    }
}
