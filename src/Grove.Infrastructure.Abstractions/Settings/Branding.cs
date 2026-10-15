#nullable enable
using Grove.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Infrastructure.Settings;

/// <summary>
/// The tenant's branding group on its site settings (docs/branding.md): one media path per
/// named rendition. The stored value is the media path, never a URL; URLs are resolved when
/// the <c>site</c> query reads them. The display name is <see cref="ISite.SiteName"/>, not here.
/// </summary>
public sealed class BrandingSettings
{
    /// <summary>Rendition name → media path. A rendition with no entry (or an empty path) is not set.</summary>
    public Dictionary<string, string> Renditions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// A named logo rendition a surface consumes (docs/branding.md › Logos — named renditions).
/// Registered, never hard-coded: the settings editor lists the registered renditions and the
/// <c>site</c> query answers one value per rendition.
/// </summary>
/// <param name="Name">The key (<c>login</c>, <c>sidebar</c>, <c>favicon</c>, <c>email</c>); the path under <c>site › branding</c>.</param>
/// <param name="Label">The editor's label.</param>
/// <param name="UsedBy">Where it is shown, for the editor's hint.</param>
/// <param name="Width">The intended width in pixels; null when any.</param>
/// <param name="Height">The intended height in pixels; null when any.</param>
/// <param name="Accept">The file types the upload offers (an <c>accept</c> list); null for any image.</param>
/// <param name="Reserved">True when no surface consumes it yet (registered so it can be set ahead of time).</param>
public sealed record BrandingRendition(string Name, string Label, string UsedBy, int? Width = null, int? Height = null, string? Accept = null, bool Reserved = false);

/// <summary>The rendition registry: every module that adds a surface registers the rendition it consumes.</summary>
public sealed class BrandingOptions
{
    private readonly List<BrandingRendition> _renditions = [];

    public IReadOnlyList<BrandingRendition> Renditions => _renditions;

    /// <summary>Registers a rendition; a later registration of the same name replaces the earlier one.</summary>
    public BrandingOptions Add(BrandingRendition rendition)
    {
        ArgumentNullException.ThrowIfNull(rendition);
        _renditions.RemoveAll(existing => string.Equals(existing.Name, rendition.Name, StringComparison.OrdinalIgnoreCase));
        _renditions.Add(rendition);
        return this;
    }
}

/// <summary>Resolves a rendition's stored media path to the URL a page shows; registered by the media module.</summary>
public interface IBrandingUrlResolver
{
    /// <summary>The public URL of a media path; null when the path cannot be resolved.</summary>
    string? ResolveUrl(string mediaPath);
}

public static class BrandingServiceCollectionExtensions
{
    /// <summary>Registers a branding rendition (docs/branding.md).</summary>
    public static IServiceCollection AddBrandingRendition(this IServiceCollection services, BrandingRendition rendition) =>
        services.Configure<BrandingOptions>(options => options.Add(rendition));

    /// <summary>
    /// The branding renditions as URLs, one entry per registered rendition: the resolved URL, or
    /// an empty string when it is not set or cannot be resolved (empty is a value; the surface
    /// falls back).
    /// </summary>
    public static Dictionary<string, string> ResolveRenditions(this ISite site, BrandingOptions options, IBrandingUrlResolver? resolver)
    {
        var stored = site.TryGet<BrandingSettings>(out var branding) ? branding.Renditions : null;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rendition in options.Renditions)
        {
            var path = stored is not null && stored.TryGetValue(rendition.Name, out var value) ? value : null;
            result[rendition.Name] = string.IsNullOrWhiteSpace(path) || resolver is null ? string.Empty : resolver.ResolveUrl(path) ?? string.Empty;
        }

        return result;
    }
}
