#nullable enable
namespace Crest.Media.Library;

/// <summary>
/// What a field, a template or a shortcode points at (ruling F1): a placement id, or a media
/// library path for content written before ids were stored. A reader asks for the id first and
/// falls back to the path; either way the placement is opened through the access check.
/// </summary>
public readonly record struct MediaReference(string? PlacementId, string? Path)
{
    public static MediaReference ForPlacement(string placementId, string? path = null) => new(placementId, path);

    public static MediaReference ForPath(string path) => new(null, path);

    public bool HasPlacement => !string.IsNullOrEmpty(PlacementId);

    public bool IsEmpty => string.IsNullOrEmpty(PlacementId) && string.IsNullOrEmpty(Path);
}
