using Grove.Media.Library;
using Shortcodes;

namespace Grove.Media.Shortcodes;

/// <summary>
/// The explicit reference forms the media shortcodes take (docs/shortcodes.md): <c>id="…"</c>, a
/// placement id, and <c>path="…"</c>, a media library path; with both, the id is tried first and
/// the path is the fallback, so content written with a path keeps working and content written
/// with an id survives a rename or a move. The body form (<c>[image]…[/image]</c>) still guesses
/// from the value.
/// </summary>
internal static class MediaShortcodeReference
{
    /// <summary>The URL for the explicit arguments, or null when the shortcode uses none.</summary>
    public static async Task<string> ResolveExplicitAsync(Arguments arguments, IMediaReferenceResolver references)
    {
        var id = arguments.NamedOrDefault("id");
        var path = arguments.NamedOrDefault("path");
        if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var resolved = await references.ResolveAsync(new MediaReference(string.IsNullOrWhiteSpace(id) ? null : id.Trim(), string.IsNullOrWhiteSpace(path) ? null : path.Trim()));
        if (resolved is not null)
        {
            return resolved.Url;
        }

        // Not found, or not for this caller: keep the path form, which answers through the same
        // access check when requested; an id alone renders nothing.
        return string.IsNullOrWhiteSpace(path) ? string.Empty : await references.GetUrlAsync(path.Trim());
    }
}
