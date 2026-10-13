namespace Crest.Shell;

/// <summary>
/// The path the browser asked for, while a shell page is prerendered. The shell middleware
/// rewrites the request path to the page's <c>@page</c> literal before endpoint routing (the
/// site's content page is "/" for every content path), so <c>NavigationManager.Uri</c> during
/// static SSR is that rewritten path, not the browser's. Only the server registers this; in
/// the browser and in a circuit the navigation manager has the real URL and pages resolve it
/// as optional.
/// </summary>
public interface IShellRequestPath
{
    /// <summary>The original request path, shell-relative ("/about"), or null outside a prerender.</summary>
    string? Path { get; }
}
