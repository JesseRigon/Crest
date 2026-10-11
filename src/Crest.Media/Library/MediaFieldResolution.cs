#nullable enable
using Crest.Media.Fields;
using Crest.Media.Files;

namespace Crest.Media.Library;

/// <summary>One item of a media field, opened for the caller.</summary>
/// <param name="Index">Its position in the field (media texts, anchors and file names line up with it).</param>
/// <param name="Resolved">Null when the caller may not view it, or it no longer exists.</param>
public sealed record MediaFieldEntry(int Index, MediaReference Reference, ResolvedMedia? Resolved)
{
    /// <summary>The placement's name, else the last segment of the path.</summary>
    public string Name => Resolved?.Item.Placement.Name ?? MediaLibraryPaths.SplitLast(Reference.Path ?? string.Empty).Name;

    /// <summary>The best path to show: the library path, else the stored one.</summary>
    public string? Path => Resolved?.Path ?? Reference.Path;

    public bool IsImage => Resolved is { } resolved ? MediaFieldResolution.IsImage(resolved.Item) : MediaFieldResolution.IsImageName(Name);
}

/// <summary>Reads a media field's items through <see cref="IMediaReferenceResolver"/> (ruling F1):
/// ids first, paths for content not yet migrated, every item through the access check.</summary>
public static class MediaFieldResolution
{
    private static readonly HashSet<string> s_imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".jpeg", ".jpg", ".png", ".webp",
    };

    public static async Task<IReadOnlyList<MediaFieldEntry>> ResolveAsync(this IMediaReferenceResolver resolver, MediaField? field, MediaUrlOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (field is null)
        {
            return [];
        }

        var references = field.GetReferences();
        var entries = new List<MediaFieldEntry>(references.Count);
        for (var i = 0; i < references.Count; i++)
        {
            var reference = references[i];
            entries.Add(new MediaFieldEntry(i, reference, reference.IsEmpty ? null : await resolver.ResolveAsync(reference, options, cancellationToken)));
        }

        return entries;
    }

    public static bool IsImage(MediaItem item)
    {
        var version = item.Content.Versions.FirstOrDefault(candidate => candidate.Number == item.Content.PublishedVersion) ?? item.Content.Versions.LastOrDefault();
        var type = version?.DetectedType ?? version?.ClaimedType;
        return type is not null
            ? type.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && !type.Contains("svg", StringComparison.OrdinalIgnoreCase)
            : IsImageName(item.Placement.Name);
    }

    public static bool IsImageName(string? name) =>
        !string.IsNullOrEmpty(name) && s_imageExtensions.Contains(System.IO.Path.GetExtension(name));
}
