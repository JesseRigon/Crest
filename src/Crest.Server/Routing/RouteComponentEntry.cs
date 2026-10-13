namespace Crest.Routing;

// One entry per @page-attributed Blazor component belonging to a shell.
// RoutePattern is always read back from the component's own [Route]/@page literal via
// reflection (see IRouteComponentTableProvider) - never re-typed as a second string
// constant anywhere else, per the standing "no invented path literals" rule (agents.md).
// Shell is the shell id (ShellId), not a raw theme id - see ShellId for why a theme-id
// comparison can never decide which shell serves a request.
// AllowsAnonymous is read off the component's own [AllowAnonymous] attribute (the same
// ASP.NET marker MVC uses): such a page is served without the shell's login redirect or
// route authorization, and rendered shell-less.
// IsOrganizationFree is read off [OrganizationFreePage]: a member page served at the member
// shell's base rather than under an organization (sign-in, registration, the picker).
public sealed record RouteComponentEntry(
    string RoutePattern,
    Type ComponentType,
    ShellId Shell,
    bool IsDefaultLanding = false,
    bool AllowsAnonymous = false,
    bool IsOrganizationFree = false);
