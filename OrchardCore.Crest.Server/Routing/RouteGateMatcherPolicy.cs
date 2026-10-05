using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing; // MatcherPolicy itself lives directly in this namespace
using Microsoft.AspNetCore.Routing.Matching; // IEndpointSelectorPolicy/CandidateSet live here
using Microsoft.Extensions.Logging;
using Crest.Extensions; // CrestBlazorHosting.ShellBasePathItem

namespace Crest.Routing;

// The gate: vetoes a matched Blazor endpoint whose ThemeOwnerMetadata.Bucket isn't the
// bucket actually serving this request. Runs AFTER endpoint matching
// (IEndpointSelectorPolicy), so the Endpoint/RouteEndpoint.Metadata that Blazor's own SDK
// produced (render-mode negotiation, boot-config metadata) is never touched,
// reconstructed, or reflected over - only whether a given already-matched candidate is
// allowed to win is decided here. This is the documented ASP.NET Core pattern for "veto a
// match based on custom per-request state" (Microsoft's own "A/B Testing Migrated
// Endpoints" migration guide uses the same shape) - not a DynamicRouteValueTransformer
// (wrong pipeline stage, designed for rewriting/generating candidates, not vetoing
// already-generated ones) and not a filtering wrapper around
// RazorComponentEndpointDataSource<App> (internal-shaped, would require re-deriving
// undocumented caching/change-token semantics). See docs/blazor-web.md's "Route
// reachability" section for the full research and reasoning.
//
// Bucket disambiguation - NOT a theme-id comparison. Earlier versions of this policy
// invalidated a candidate whose ThemeOwnerMetadata.ThemeId wasn't "the tenant's active
// admin theme OR the tenant's active site theme". That is always true for BOTH buckets at
// once: a tenant's active admin theme and active site theme are independent, simultaneous
// settings, not mutually exclusive alternatives. Confirmed empirically: a clean-tenant GET
// / matched both Admin/Home.razor's endpoint (@page "/", deliberately base-relative - see
// that file's own header comment) and Site/Home.razor's endpoint, and BOTH evaluated as
// "active theme" under the old OR check, leaving two valid "/" candidates and throwing
// AmbiguousMatchException. The two endpoints are not actually ambiguous to a human: which
// one should win is entirely decided by whether BlazorAdminThemeMiddleware routed this
// specific request to the admin shell (it rewrites the request path AND stashes
// CrestBlazorHosting.ShellBasePathItem before endpoint routing ever runs) - the exact
// same signal Components/App.razor itself reads to decide which document to render. So:
// a candidate is valid iff its bucket IS the bucket the middleware selected for this
// request, stamped as CrestBlazorHosting.ShellBucketItem. Site is the fallback for every
// request the middleware didn't claim - see BlazorAdminThemeMiddleware's
// IsPageRequest/shell-matching gating, which never even runs for a bare "/" request - so
// an absent stamp reads as Site.
//
// The buckets are mutually exclusive by construction, mirroring exactly the shell branch
// in Components/App.razor: a request the middleware rewrote into "/" (e.g. a bare
// "/Admin" with no further segments, or "/members") must leave only that shell's
// candidate standing, never two. This is an equality test rather than one arm per bucket
// precisely so that property cannot be broken by adding a shell.
public sealed class RouteGateMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
{
    // Runs before Blazor's own render-mode negotiation policies, so a vetoed candidate
    // never reaches that later stage.
    public override int Order => -1000;

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints) =>
        endpoints.Any(endpoint => endpoint.Metadata.GetMetadata<ThemeOwnerMetadata>() is not null);

    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        // The admin shell marker BlazorAdminThemeMiddleware stashes before UseRouting()
        // runs - the same HttpContext.Items key Components/App.razor reads to pick which
        // document to render. No theme-service calls needed here: whether Admin's bucket
        // is allowed to win is entirely a function of the middleware's own routing
        // decision, already made once per request.
        // The bucket the middleware selected for this request. Absent means Site: Site is
        // the fallback bucket for every request no shell claimed, and the middleware
        // never runs its gating for those (a bare "/" never reaches it). Reading the
        // bucket rather than "is a base path present" is what makes a third shell
        // possible at all - presence is one bit and cannot distinguish three shells.
        var requestBucket = httpContext.Items.TryGetValue(CrestBlazorHosting.ShellBucketItem, out var bucketItem)
            && bucketItem is RouteBucket stamped
                ? stamped
                : RouteBucket.Site;

        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger<RouteGateMatcherPolicy>();

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

            // Exactly one bucket is valid per request: the one the middleware selected.
            // Equality rather than a per-bucket arm, so adding a shell needs no change
            // here - and so no two buckets can ever both be valid, which is what the
            // AmbiguousMatchException described above came from.
            if (themeOwner.Bucket != requestBucket)
            {
                logger.LogDebug(
                    "RouteGateMatcherPolicy vetoed candidate {DisplayName} (bucket {Bucket}) for {Path}: this request is serving the {RequestBucket} shell.",
                    candidates[index].Endpoint.DisplayName,
                    themeOwner.Bucket,
                    httpContext.Request.Path,
                    requestBucket);
                candidates.SetValidity(index, false);
            }
        }

        return Task.CompletedTask;
    }
}
