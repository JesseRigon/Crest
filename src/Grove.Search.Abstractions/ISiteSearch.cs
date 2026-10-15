using System.Security.Claims;

namespace Grove.Search.Abstractions;

/// <summary>
/// The search seam: one search over the tenant's indexes, whatever engine serves them. An engine
/// (Lucene, Elasticsearch, Azure AI Search, ...) registers its <see cref="ISearchService"/> with
/// <c>AddSearchService</c> under its provider name; an index profile names the provider that
/// serves it. The search API and the site search page call this, never an engine.
/// </summary>
public interface ISiteSearch
{
    /// <summary>
    /// Searches <paramref name="indexName"/> (the site's default index when null) for
    /// <paramref name="terms"/> as <paramref name="user"/>, who must be allowed to query the index.
    /// </summary>
    Task<SiteSearchResult> SearchAsync(string? indexName, string? terms, int from, int size, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

/// <summary>What a search answered.</summary>
public sealed class SiteSearchResult
{
    public SiteSearchStatus Status { get; init; }

    /// <summary>The index searched (its name); null when none was found.</summary>
    public string? Index { get; init; }

    /// <summary>The matching content items, in the engine's order: published versions, or the
    /// latest when the index holds latest versions.</summary>
    public IReadOnlyList<SiteSearchHit> Hits { get; init; } = [];

    /// <summary>Whether more hits follow the page.</summary>
    public bool HasMore { get; init; }
}

public sealed record SiteSearchHit(Grove.Contents.Management.ContentItem ContentItem, IReadOnlyList<string> Highlights);

public enum SiteSearchStatus
{
    /// <summary>The search ran (an empty <c>terms</c> runs nothing and answers no hits).</summary>
    Ok,

    /// <summary>No index was named and the site has no default search index.</summary>
    NoDefaultIndex,

    /// <summary>The named index does not exist.</summary>
    IndexNotFound,

    /// <summary>The caller may not query the index.</summary>
    Forbidden,

    /// <summary>No search engine is registered for the index's provider.</summary>
    NoProvider,
}
