#nullable enable

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Grove.Display.Management.Components;

/// <summary>
/// One part of a tree's composite version (decision 12): something a built tree depends on that
/// can change without the tree being rebuilt (the published templates, the overrides, the layer
/// placements). Modules that feed the tree register one; the version must change whenever what
/// they feed does, and must be cheap to read (no evaluation).
/// </summary>
public interface IDisplayTreeVersionPart
{
    ValueTask<string?> GetVersionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The composite version of a display tree (decision 12): the version parts the modules register,
/// the tenant's content version (bumped on every publish, unpublish and removal), the culture and
/// what the caller passes (the page, the viewer's scope signature). It is the tree's ETag: the
/// server compares it without evaluating anything and answers "unchanged", and it keys the small
/// server cache of shared trees. The content version starts from a stamp taken when the tenant
/// starts, so a restart or another node never answers "unchanged" for a tree it did not see.
/// </summary>
public sealed class DisplayTreeVersions
{
    private readonly string _stamp = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
    private long _content;

    /// <summary>Raised by a content change any tree may show (a publish, an unpublish, a removal).</summary>
    public void BumpContent() => Interlocked.Increment(ref _content);

    public async Task<string> ComputeAsync(IEnumerable<IDisplayTreeVersionPart> parts, IEnumerable<string?> scope, CancellationToken cancellationToken = default)
    {
        var text = new StringBuilder()
            .Append(_stamp).Append('\u001f')
            .Append(Interlocked.Read(ref _content).ToString(CultureInfo.InvariantCulture)).Append('\u001f')
            .Append(CultureInfo.CurrentUICulture.Name).Append('\u001f');

        foreach (var part in parts)
        {
            text.Append(part.GetType().FullName).Append('=').Append(await part.GetVersionAsync(cancellationToken)).Append('\u001f');
        }

        foreach (var value in scope)
        {
            text.Append(value).Append('\u001f');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..32];
    }

    /// <summary>The ETag header value of a version.</summary>
    public static string ETag(string version) => "\"" + version + "\"";

    /// <summary>Whether an If-None-Match header names <paramref name="version"/>.</summary>
    public static bool Matches(string? ifNoneMatch, string version) =>
        !string.IsNullOrEmpty(ifNoneMatch)
        && ifNoneMatch.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(tag => tag == "*" || string.Equals(tag.StartsWith("W/", StringComparison.Ordinal) ? tag[2..] : tag, ETag(version), StringComparison.Ordinal));
}

/// <summary>
/// The small server cache of built trees (decision 12), a tenant singleton bounded by the size of
/// what it holds, not by viewers. Only trees every viewer of one permission set shares go in
/// (anonymous pages); a tree built with a signed-in viewer's rights never does (security audit
/// item 9). Drafts and previews are never cached. Entries are keyed by their composite version,
/// so a change anywhere a tree depends on simply misses.
/// </summary>
public sealed class DisplayTreeCache : IDisposable
{
    /// <summary>The bound, in characters of serialized tree.</summary>
    public const long SizeLimit = 16 * 1024 * 1024;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = SizeLimit });

    public bool TryGet<T>(string key, out T? value) where T : class => _cache.TryGetValue(key, out value);

    public void Set<T>(string key, T value, long size) where T : class =>
        _cache.Set(key, value, new MemoryCacheEntryOptions
        {
            Size = Math.Clamp(size, 1, SizeLimit),
            SlidingExpiration = TimeSpan.FromMinutes(10),
        });

    public void Dispose() => _cache.Dispose();
}
