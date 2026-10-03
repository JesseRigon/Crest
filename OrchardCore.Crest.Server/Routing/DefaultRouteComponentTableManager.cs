using System.Collections.Concurrent;
using OrchardCore.Admin;
using OrchardCore.Themes.Services;

namespace Crest.Routing;

// Mirrors DefaultShapeTableManager's actual caching shape (confirmed against
// OrchardCore.DisplayManagement/Descriptors/DefaultShapeTableManager.cs): a keyed
// singleton dictionary, populated lazily per key, with NO separate invalidation
// signal/event subscription. Orchard already tears down and rebuilds the whole tenant
// shell (and every singleton inside it, including this cache) on feature/theme change -
// piggybacking on that is the correct, minimal amount of invalidation logic, not a gap.
//
// Keyed by the concrete (admin theme id, site theme id) pair currently active for this
// shell - still a valid, useful cache key (it changes exactly when a theme change could
// change which pages are registered). The member theme is deliberately NOT in the key:
// member routes are registered unconditionally (see BuildAsync), so no member theme
// change can change the table's contents, and a change of the active member theme tears
// the shell down anyway, which drops this cache with it. Which BUCKETS are active is decided
// below via IBlazorAdminThemeDetector, not by comparing those ids against each
// provider's own id. Earlier code compared adminThemeId/siteThemeId directly against
// each IRouteComponentTableProvider.ThemeId (then a raw string) via
// "activeThemeIds.Contains(provider.ThemeId)" - the exact same bug class
// RouteGateMatcherPolicy had: IAdminThemeService.GetAdminThemeNameAsync() returns null
// unless a tenant/recipe explicitly called SetAdminThemeAsync, and even when it doesn't,
// a tenant can still have the Blazor admin theme active via the "blazor" extension tag
// (see IBlazorAdminThemeDetector) - a case the old string-equality check silently missed,
// leaving AdminRouteComponentTableProvider's routes never registered for that tenant.
public sealed class DefaultRouteComponentTableManager(
    IAdminThemeService adminThemeService,
    ISiteThemeService siteThemeService,
    IBlazorAdminThemeDetector blazorAdminThemeDetector,
    IEnumerable<IRouteComponentTableProvider> providers,
    ConcurrentDictionary<(string? AdminThemeId, string? SiteThemeId), Task<RouteComponentTable>> cache)
    : IRouteComponentTableManager
{
    public async Task<RouteComponentTable> GetRouteComponentTableAsync()
    {
        var adminThemeId = await adminThemeService.GetAdminThemeNameAsync();
        var siteThemeId = await siteThemeService.GetSiteThemeNameAsync();
        var key = (adminThemeId, siteThemeId);

        return await cache.GetOrAdd(key, _ => BuildAsync());
    }

    private async Task<RouteComponentTable> BuildAsync()
    {
        // Site is the fallback bucket for every request the admin middleware doesn't
        // itself claim (see RouteGateMatcherPolicy's comment) - its routes are always
        // registered. Admin's routes are only registered when Blazor is genuinely the
        // active admin theme for this tenant.
        //
        // Member's routes are always registered too, and deliberately NOT gated on the
        // admin theme: a tenant's admin theme and member theme are independent,
        // simultaneously-active settings, so gating the member shell on the admin one
        // would make a product's own member portal vanish because staff switched their
        // back-office theme. The member shell is present when the Members feature is
        // enabled, which is what decides whether a member provider is registered at all.
        var isBlazorAdminThemeActive = await blazorAdminThemeDetector.IsBlazorAdminThemeActiveAsync();

        var entries = providers
            .Where(provider => provider.Bucket != RouteBucket.Admin || isBlazorAdminThemeActive)
            .SelectMany(provider => provider.GetRouteComponents())
            .ToArray();

        return new RouteComponentTable(entries);
    }
}
