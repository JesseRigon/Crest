namespace Grove.Search.Api;

// The search API's wire types, shared with the site search page (site-wasm links this file).

public static class SearchApiPaths
{
    public const string Search = "api/grove/search";
}

/// <summary>
/// A search: the page's title and placeholder (the site's search settings), the index searched
/// and a page of hits. <see cref="Status"/> is <c>Ok</c>, <c>NoDefaultIndex</c>,
/// <c>IndexNotFound</c> or <c>NoProvider</c> (a refused caller is answered 403).
/// </summary>
public sealed record SearchResponse(
    string Status,
    string? Index,
    string? Terms,
    string? PageTitle,
    string? Placeholder,
    SearchHitModel[] Hits,
    int From,
    int Size,
    bool HasMore);

public sealed record SearchHitModel(string ContentItemId, string ContentType, string? DisplayText, string? Url, string[] Highlights);
