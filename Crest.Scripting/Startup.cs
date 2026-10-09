using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;
using Crest.Scripting.JavaScript;
using Crest.Scripting.Providers;

namespace Crest.Scripting;

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
