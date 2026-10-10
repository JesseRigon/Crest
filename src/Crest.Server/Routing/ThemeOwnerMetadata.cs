namespace Crest.Routing;

// Which routing bucket a Blazor endpoint belongs to. Deliberately NOT a raw theme-id
// string: routing only ever needs to answer "which shell is serving this request" -
// never "which literal theme id is active." A tenant has an active admin theme, an
// active site theme and (with Members enabled) an active member theme ALL AT THE SAME
// TIME - they are independent Crest settings, not mutually exclusive alternatives - so
// comparing a component's own theme id against "is it any of the active theme ids" can
// never disambiguate a collision between buckets (e.g. Admin/Home.razor,
// Site/Home.razor and Member/Home.razor all declaring @page "/"): every side of that OR
// is simultaneously true by construction. See RouteGateMatcherPolicy for how a
// candidate's bucket is actually gated (via BlazorAdminThemeMiddleware's own
// shell-selection signal, not a theme-id comparison).
public enum RouteBucket
{
    Admin,
    Site,
    // The member shell: organization-bound users, at MemberOptions.MemberUrlPrefix.
    // Site stays the fallback (the bucket for every request no shell claimed), so a new
    // bucket is never reached by absence of a match - only by the middleware explicitly
    // selecting it. See CrestBlazorHosting.ShellBucketItem for why the shell is carried
    // as this enum rather than inferred from the base path.
    Member,
}

// Attached to a Blazor endpoint via an endpoint convention (see Startup.Configure),
// sourced from the matching RouteComponentEntry at the moment the endpoint is mapped,
// carried as the Bucket - never a second, re-typed theme id literal.
// RouteGateMatcherPolicy is the sole consumer: it vetoes a matched candidate whose
// bucket is not the shell BlazorAdminThemeMiddleware routed this request to.
public sealed record ThemeOwnerMetadata(RouteBucket Bucket);
