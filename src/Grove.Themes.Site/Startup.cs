using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;

namespace Grove.Themes.Site;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection serviceCollection)
    {
        // The site renders on the display tree (docs/blazor-display.md rework step 3): its
        // layout, content frame and error pages are components in the theme's client library
        // (wasm/Components), its styles the design system's plus GroveSite.css. No Liquid views,
        // no Bootstrap resources.

        // Razor Components/SSR hosting (AddRazorComponents, AddInteractiveWebAssemblyComponents,
        // AddGroveComponents/AddIconClient) is registered once in Grove.Infrastructure.Server, not
        // here - Server is the single Blazor Web App host for both the API and SSR
        // document rendering, for every Grove theme that needs it. See docs/blazor-web.md.
    }
}
