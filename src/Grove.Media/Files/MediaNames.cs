#nullable enable
namespace Grove.Media.Files;

/// <summary>Placement names: one path segment, never a path.</summary>
public static class MediaNames
{
    public const int MaxLength = 255;

    public static string Validate(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) ||
            trimmed.Length > MaxLength ||
            trimmed is "." or ".." ||
            trimmed.IndexOfAny(['/', '\\']) >= 0 ||
            trimmed.Any(char.IsControl))
        {
            throw new ArgumentException("A name is one segment of up to 255 characters, without slashes.", nameof(name));
        }

        return trimmed;
    }
}

/// <summary>The placement does not exist for the caller: missing, or not visible to them
/// (requirement 68). The message never says which.</summary>
public sealed class MediaObjectNotFoundException() : KeyNotFoundException("The file or folder was not found.");

/// <summary>The operation conflicts with the file system: a folder inside itself, a file where
/// a folder is needed, a drive moved.</summary>
public sealed class MediaObjectConflictException(string message) : InvalidOperationException(message);
