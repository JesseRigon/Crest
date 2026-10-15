using Grove.Routing;

namespace Grove.Data.Feeds.Models;

public class ContextualizeContext
{
    public IServiceProvider ServiceProvider { get; set; }

    /// <summary>The tenant's link generator for the feed request (absolute URLs, content item routes).</summary>
    public IShellLinkGenerator Links { get; set; }
}
