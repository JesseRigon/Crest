using System.Text.Json.Nodes;

namespace Grove.Seo.Sitemaps.Api;

// The sitemaps API's wire types, shared with the admin pages (blazor-wasm links this file).

public static class SitemapsApiPaths
{
    public const string Base = "api/grove/sitemaps";

    public const string SourceTypes = Base + "/source-types";

    public const string ContentTypes = Base + "/content-types";

    public const string Cache = Base + "/cache";

    public static string Sitemap(string sitemapId) => $"{Base}/{Uri.EscapeDataString(sitemapId)}";

    public static string Sources(string sitemapId) => $"{Sitemap(sitemapId)}/sources";

    public static string Source(string sitemapId, string sourceId) => $"{Sources(sitemapId)}/{Uri.EscapeDataString(sourceId)}";

    public static string CacheFile(string fileName) => $"{Cache}/{Uri.EscapeDataString(fileName)}";
}

/// <summary>The two kinds of sitemap: a sitemap of sources, and an index of sitemaps.</summary>
public static class SitemapKinds
{
    public const string Sitemap = "sitemap";

    public const string Index = "index";
}

/// <summary>
/// A sitemap or a sitemap index. A sitemap has <see cref="Sources"/> (filled on a single read);
/// an index lists the sitemaps it contains.
/// </summary>
public sealed record SitemapModel(
    string SitemapId,
    string Kind,
    string Name,
    string Path,
    bool Enabled,
    SitemapSourceModel[] Sources,
    string[] ContainedSitemapIds);

/// <summary>
/// One source of a sitemap: its type (the source class name, <c>ContentTypesSitemapSource</c>,
/// <c>CustomPathSitemapSource</c>, ...) and its settings as the stored JSON (camel case).
/// </summary>
public sealed record SitemapSourceModel(string Id, string Type, JsonObject Data);

/// <summary>A sitemap or an index as the pages write it; the path defaults to the name's slug.</summary>
public sealed class SitemapWrite
{
    public string Kind { get; set; } = SitemapKinds.Sitemap;

    public string Name { get; set; } = string.Empty;

    public string? Path { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>For an index: the sitemaps it contains.</summary>
    public string[] ContainedSitemapIds { get; set; } = [];
}

/// <summary>A source as the pages write it: <see cref="Type"/> is read on create only.</summary>
public sealed class SitemapSourceWrite
{
    public string? Type { get; set; }

    public JsonObject Data { get; set; } = [];
}

/// <summary>A content type whose items have routes, for the content types source.</summary>
public sealed record RoutableContentTypeModel(string Name, string DisplayName);

/// <summary>A cached sitemap file.</summary>
public sealed record SitemapCacheFileModel(string FileName);
