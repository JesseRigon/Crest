namespace Crest.Routing;

// Which shell a Blazor endpoint belongs to, carried as the shell's id (ShellId) - never a raw
// theme id: a tenant's admin, site and member themes are all active at once, so "is this
// component's theme active" is true for every shell declaring @page "/" (see ShellId).
//
// Attached to a Blazor endpoint via an endpoint convention (see Startup.Configure), read off
// the component's assembly (ShellAssemblies) at the moment the endpoint is mapped - never a
// second, re-typed literal. RouteGateMatcherPolicy is the sole consumer: it vetoes a matched
// candidate whose shell is not the one the shell selector routed this request to, or whose
// module is not enabled for the tenant.
public sealed record ThemeOwnerMetadata(ShellId Shell);
