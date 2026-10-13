using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing; // MatcherPolicy itself lives directly in this namespace
using Microsoft.AspNetCore.Routing.Matching; // IEndpointSelectorPolicy/CandidateSet live here
using Microsoft.Extensions.Logging;
using Crest.Extensions; // BlazorHosting.ShellBucketItem

namespace Crest.Routing;

// The gate: vetoes a matched Blazor endpoint whose ThemeOwnerMetadata.Shell isn't the shell
// actually serving this request. Runs AFTER endpoint matching (IEndpointSelectorPolicy), so
// the Endpoint/RouteEndpoint.Metadata that Blazor's own SDK produced (render-mode
// negotiation, boot-config metadata) is never touched, reconstructed, or reflected over -
// only whether a given already-matched candidate is allowed to win is decided here. This is
// the documented ASP.NET Core pattern for "veto a match based on custom per-request state"
// (Microsoft's own "A/B Testing Migrated Endpoints" migration guide uses the same shape) -
// not a DynamicRouteValueTransformer (wrong pipeline stage) and not a filtering wrapper
// around RazorComponentEndpointDataSource<App> (internal-shaped). See docs/blazor-web.md's
// "Route reachability" section.
//
// Shell disambiguation - NOT a theme-id comparison. A tenant's active admin theme and active
// site theme are independent, simultaneous settings: a clean-tenant GET / matched both
// Admin/Home.razor's endpoint (@page "/", base-relative) and Site's content page (@page "/"),
// and both counted as "active theme", leaving two valid "/" candidates and an
// AmbiguousMatchException. Which one should win is entirely decided by the shell the
// selector routed this request to (ShellSelectionMiddleware rewrites the request path AND
// stamps BlazorHosting.ShellBucketItem before endpoint routing runs) - the same signal
// Components/App.razor reads to pick a document. A candidate is valid iff its shell IS the
// stamped shell; absent means Site, the fallback. Equality rather than one arm per shell, so
// adding a shell changes nothing here and no two shells can both be valid.
//
// This policy never referees between a Blazor page and MVC or static files: the site has no
// catch-all page, and the shell middleware forwards a legacy-owned path to MVC itself
// (ILegacyRouteOwnership, ruling 2026-10-13).
public sealed class RouteGateMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
{
    // Runs before Blazor's own render-mode negotiation policies, so a vetoed candidate
    // never reaches that later stage.
    public override int Order => -1000;

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints) =>
        endpoints.Any(endpoint => endpoint.Metadata.GetMetadata<ThemeOwnerMetadata>() is not null);

    public async Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        var requestShell = httpContext.Items.TryGetValue(BlazorHosting.ShellBucketItem, out var shellItem)
            && shellItem is ShellId stamped
                ? stamped
                : ShellId.Site;

        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger<RouteGateMatcherPolicy>();
        var enabledModules = httpContext.RequestServices.GetService<IEnabledModuleAssemblies>();

        for (var index = 0; index < candidates.Count; index++)
        {
            if (!candidates.IsValidCandidate(index))
            {
                continue;
            }

            var themeOwner = candidates[index].Endpoint?.Metadata.GetMetadata<ThemeOwnerMetadata>();
            if (themeOwner is null)
            {
                continue;
            }

            if (themeOwner.Shell != requestShell)
            {
                logger.LogDebug(
                    "RouteGateMatcherPolicy vetoed candidate {DisplayName} (shell {Shell}) for {Path}: this request is serving the {RequestShell} shell.",
                    candidates[index].Endpoint.DisplayName,
                    themeOwner.Shell,
                    httpContext.Request.Path,
                    requestShell);
                candidates.SetValidity(index, false);
                continue;
            }

            // A disabled module is absent for the tenant: its page endpoints exist (endpoints
            // are mapped once for every tenant) but never win.
            var componentType = candidates[index].Endpoint?.Metadata
                .GetMetadata<Microsoft.AspNetCore.Components.Endpoints.ComponentTypeMetadata>()?.Type;
            if (componentType is not null && enabledModules is not null && !await enabledModules.IsEnabledAsync(componentType.Assembly))
            {
                logger.LogDebug(
                    "RouteGateMatcherPolicy vetoed candidate {DisplayName} for {Path}: its module is not enabled for this tenant.",
                    candidates[index].Endpoint.DisplayName,
                    httpContext.Request.Path);
                candidates.SetValidity(index, false);
            }
        }
    }
}
