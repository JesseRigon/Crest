#nullable enable

using System.Text.Json.Nodes;
using Crest.Queries;

namespace Crest.Settings;

/// <summary>The site's name and branding renditions, as the <c>site</c> system query answers them.</summary>
public sealed record SiteIdentityResult(string Name, IReadOnlyDictionary<string, string> Branding)
{
    public static SiteIdentityResult Empty { get; } = new(string.Empty, new Dictionary<string, string>());

    /// <summary>A rendition's URL; empty when it is not set (the surface falls back).</summary>
    public string Rendition(string name) => Branding.TryGetValue(name, out var url) ? url : string.Empty;
}

/// <summary>
/// Reads the site's identity in process through the <c>site</c> system query, the one accessor
/// every surface binds (docs/branding.md › How surfaces read branding): the SSR document head
/// and the interim anonymous branding read use it, so nothing reads settings or media paths
/// directly for display.
/// </summary>
public sealed class SiteIdentity(IServiceProvider services)
{
    /// <summary>The <c>site</c> system query (Crest.Queries' <c>SystemQuerySource.SiteQuery</c>).</summary>
    public const string SiteQueryName = "site";

    public async Task<SiteIdentityResult> GetAsync(CancellationToken cancellationToken = default)
    {
        // The query system is a feature of its own; without it the site has no readable identity.
        if (services.GetService(typeof(IQueryManager)) is not IQueryManager queryManager)
        {
            return SiteIdentityResult.Empty;
        }

        var query = await queryManager.GetQueryAsync(SiteQueryName);
        if (query is null)
        {
            return SiteIdentityResult.Empty;
        }

        var results = await queryManager.ExecuteQueryAsync(query, new QueryRequest { CancellationToken = cancellationToken });
        if (results.Items?.FirstOrDefault() is not JsonObject row)
        {
            return SiteIdentityResult.Empty;
        }

        var branding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (row["Branding"] is JsonObject renditions)
        {
            foreach (var (name, value) in renditions)
            {
                branding[name] = value?.GetValue<string>() ?? string.Empty;
            }
        }

        return new SiteIdentityResult(row["Name"]?.GetValue<string>() ?? string.Empty, branding);
    }
}
