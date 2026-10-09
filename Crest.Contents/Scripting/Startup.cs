using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;
using Crest.Scripting;

namespace Crest.Contents.Scripting;

[RequireFeatures("Crest.Scripting")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IGlobalMethodProvider, ContentMethodsProvider>();
        services.AddSingleton<IGlobalMethodProvider, UrlMethodsProvider>();
    }
}
