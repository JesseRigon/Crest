using Microsoft.Extensions.Logging;
using Crest.Admin;
using Crest.Environment.Extensions;

namespace Crest.Routing;

// Single source of truth for "is the Blazor admin theme the tenant's currently active
// admin theme" - the same question BlazorAdminThemeMiddleware, RouteGateMatcherPolicy,
// and AdminRouteComponentTableProvider/DefaultRouteComponentTableManager each used to ask
// with their own, independently-typed logic (a raw theme-id string equality check that
// silently drifted from the real "is this theme id OR does it carry the blazor tag"
// rule the middleware alone had gotten right). Scoped: IAdminThemeService itself is
// scoped, and this is only ever consulted from within an active request/shell scope
// (the middleware's shell-host fallback path for pre-scope checks stays inline there -
// it is not "is Blazor active", it is "how do I get a scope to ask that question in").
public interface IBlazorAdminThemeDetector
{
    Task<bool> IsBlazorAdminThemeActiveAsync();
}

// A Crest Blazor admin theme is one that is, or descends from, a theme tagged
// crest-blazor (ThemeBuckets.IsCrestBlazorTheme) - so a branded fork of the Crest admin
// theme serves the Blazor admin shell exactly as the Crest theme does.
public sealed class BlazorAdminThemeDetector(
    IAdminThemeService adminThemeService,
    IExtensionManager extensionManager,
    ILogger<BlazorAdminThemeDetector> logger) : IBlazorAdminThemeDetector
{
    public async Task<bool> IsBlazorAdminThemeActiveAsync()
    {
        var adminTheme = await adminThemeService.GetAdminThemeAsync();
        var isBlazorAdminTheme = Crest.Themes.ThemeBuckets.IsCrestBlazorTheme(adminTheme, extensionManager.GetExtension);

        logger.LogDebug(
            "Blazor admin theme check: active admin theme '{ExtensionId}', serving Blazor: {ServeBlazor}.",
            adminTheme?.Id,
            isBlazorAdminTheme);

        return isBlazorAdminTheme;
    }
}
