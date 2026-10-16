using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Display.Templates.Models;

/// <summary>
/// A designed page (docs/blazor-display.md § 6a, decision 9): a tenant-authored tree with its own
/// URL. It is its own kind, never a component: it owns its route, its shell bucket, its document
/// fields and its permission sets (§ 10), and the components under it come from its Liquid
/// source. Stored as the <see cref="DesignedPagesConstants.ContentType"/> content item, with drafts
/// and publish but no overrides (an override of a page would change routing); its route exists
/// once it is published. Its values are fields (text fields, a boolean field <c>AllowAnonymous</c>);
/// this class is the typed accessor over them.
/// </summary>
public sealed class DesignedPageFieldSet : ContentFieldSet
{
    /// <summary>The page's stable name: its permissions and its tree's binding are keyed by it.</summary>
    [JsonIgnore]
    public string Name
    {
        get => this.Get<TextField>(nameof(Name))?.Text ?? string.Empty;
        set => this.Alter<TextField>(nameof(Name), fieldValue => fieldValue.Text = value);
    }

    /// <summary>The route pattern relative to the bucket's base (<c>/landing</c>, <c>/offers/{id}</c>).</summary>
    [JsonIgnore]
    public string Route
    {
        get => this.Get<TextField>(nameof(Route))?.Text ?? string.Empty;
        set => this.Alter<TextField>(nameof(Route), fieldValue => fieldValue.Text = value);
    }

    /// <summary>The shell bucket the page belongs to: <c>site</c>, <c>admin</c> or <c>member</c>. Never changed by an edit of the tree.</summary>
    [JsonIgnore]
    public string Bucket
    {
        get => this.Get<TextField>(nameof(Bucket))?.Text ?? DesignedPagesConstants.SiteBucket;
        set => this.Alter<TextField>(nameof(Bucket), fieldValue => fieldValue.Text = value);
    }

    /// <summary>The document title.</summary>
    [JsonIgnore]
    public string? Title
    {
        get => this.Get<TextField>(nameof(Title))?.Text;
        set => this.Alter<TextField>(nameof(Title), fieldValue => fieldValue.Text = value);
    }

    /// <summary>The meta description.</summary>
    [JsonIgnore]
    public string? Description
    {
        get => this.Get<TextField>(nameof(Description))?.Text;
        set => this.Alter<TextField>(nameof(Description), fieldValue => fieldValue.Text = value);
    }

    /// <summary>Whether a caller without the page's Data permission may view it (the route's <c>[AllowAnonymous]</c>).</summary>
    [JsonIgnore]
    public bool AllowAnonymous
    {
        get => this.Get<BooleanField>(nameof(AllowAnonymous))?.Value ?? false;
        set => this.Alter<BooleanField>(nameof(AllowAnonymous), fieldValue => fieldValue.Value = value);
    }

    /// <summary>The Liquid source of the page's tree; it reads the route's parameters as <c>Model.Route</c>.</summary>
    [JsonIgnore]
    public string Source
    {
        get => this.Get<TextField>(nameof(Source))?.Text ?? string.Empty;
        set => this.Alter<TextField>(nameof(Source), fieldValue => fieldValue.Text = value);
    }
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
    public const string PublishedSignal = "Grove.Display.Templates.DesignedPages.Published";

    public static bool IsBucket(string? bucket) => bucket is SiteBucket or AdminBucket or MemberBucket;
}
