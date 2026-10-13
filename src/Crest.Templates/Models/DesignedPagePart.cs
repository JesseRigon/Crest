using Crest.ContentManagement;

namespace Crest.Templates.Models;

/// <summary>
/// A designed page (docs/blazor-display.md § 6a, decision 9): a tenant-authored tree with its own
/// URL. It is its own kind, never a component: it owns its route, its shell bucket, its document
/// fields and its permission sets (§ 10), and the components under it come from its Liquid
/// source. Stored as the <see cref="DesignedPagesConstants.ContentType"/> content item, with drafts
/// and publish but no overrides (an override of a page would change routing); its route exists
/// once it is published.
/// </summary>
public sealed class DesignedPagePart : ContentPart
{
    /// <summary>The page's stable name: its permissions and its tree's binding are keyed by it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The route pattern relative to the bucket's base (<c>/landing</c>, <c>/offers/{id}</c>).</summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>The shell bucket the page belongs to: <c>site</c>, <c>admin</c> or <c>member</c>. Never changed by an edit of the tree.</summary>
    public string Bucket { get; set; } = DesignedPagesConstants.SiteBucket;

    /// <summary>The document title.</summary>
    public string? Title { get; set; }

    /// <summary>The meta description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether a caller without the page's Data permission may view it (the route's <c>[AllowAnonymous]</c>).</summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>The Liquid source of the page's tree; it reads the route's parameters as <c>Model.Route</c>.</summary>
    public string Source { get; set; } = string.Empty;
}

public static class DesignedPagesConstants
{
    public const string ContentType = "DesignedPage";

    public const string SiteBucket = "site";

    public const string AdminBucket = "admin";

    public const string MemberBucket = "member";

    /// <summary>The shape a designed page's tree binds to: <c>DesignedPage__{Name}</c> resolves to its source.</summary>
    public const string ShapeType = "DesignedPage";

    /// <summary>The signal a publish, unpublish or removal of a designed page raises: the routes are re-read.</summary>
    public const string PublishedSignal = "Crest.Templates.DesignedPages.Published";

    public static bool IsBucket(string? bucket) => bucket is SiteBucket or AdminBucket or MemberBucket;
}
