using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;

namespace Crest.Themes.Crest.SiteTheme;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection serviceCollection)
    {
        // The site renders on the display tree (docs/blazor-display.md rework step 3): its
        // layout, content frame and error pages are components in the theme's client library
        // (wasm/Components), its styles the design system's plus CrestSite.css. No Liquid views,
        // no Bootstrap resources.

        // Razor Components/SSR hosting (AddRazorComponents, AddInteractiveWebAssemblyComponents,
        // AddCrestComponents/AddCrestIconClient) is registered once in Crest.Server, not
        // here - Server is the single Blazor Web App host for both the API and SSR
        // document rendering, for every Crest theme that needs it. See docs/blazor-web.md.
    }
}
