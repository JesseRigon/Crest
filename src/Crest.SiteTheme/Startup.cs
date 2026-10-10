using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;

namespace Crest.Themes.Crest.SiteTheme;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();

        // Razor Components/SSR hosting (AddRazorComponents, AddInteractiveWebAssemblyComponents,
        // AddCrestComponents/AddCrestIconClient) is registered once in Crest.Server, not
        // here - Server is the single Blazor Web App host for both the API and SSR
        // document rendering, for every Crest theme that needs it. See docs/blazor-web.md.
    }
}
