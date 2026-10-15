using Grove.Media.Files;
using Microsoft.AspNetCore.StaticFiles;

namespace Grove.Media.Scanning.Services;

/// <summary>
/// Whether a file's content matches its claimed type (requirement 87): the type the client sent
/// and the type its name's extension implies, each against the type detected from the content.
/// A claimed type the detector would recognise must be recognised; containers (zip, OLE, ISO media)
/// and text accept the types built on them.
/// </summary>
public static class TypeConsistency
{
    private const string OctetStream = "application/octet-stream";

    private static readonly FileExtensionContentTypeProvider s_extensions = new();

    private static readonly Dictionary<string, string> s_aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpg"] = "image/jpeg",
        ["image/pjpeg"] = "image/jpeg",
        ["image/x-png"] = "image/png",
        ["application/x-pdf"] = "application/pdf",
        ["application/x-zip-compressed"] = "application/zip",
        ["application/x-zip"] = "application/zip",
        ["audio/x-m4a"] = "audio/mp4",
        ["video/x-m4v"] = "video/mp4",
        ["image/x-ms-bmp"] = "image/bmp",
        ["application/x-gzip"] = "application/gzip",
        ["audio/x-wav"] = "audio/wav",
        ["audio/wave"] = "audio/wav",
        ["video/x-msvideo"] = "video/avi",
        ["text/xml"] = "application/xml",
        ["application/x-javascript"] = "text/javascript",
        ["application/javascript"] = "text/javascript",
        ["application/vnd.ms-office"] = "application/x-ole-storage",
        ["application/cdfv2"] = "application/x-ole-storage",
        ["application/x-msi"] = "application/x-ole-storage",
    };

    /// <summary>Types a detector recognises by signature: a claim of one must be confirmed by the content.</summary>
    private static readonly HashSet<string> s_signatureTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "image/webp", "image/bmp", "image/tiff", "image/heic", "image/avif",
        "application/pdf", "application/zip", "application/gzip", "application/x-ole-storage",
        "video/mp4", "audio/mp4", "video/quicktime", "video/3gpp", "video/webm", "audio/wav", "video/avi", "audio/ogg", "audio/mpeg",
        "application/x-dosexec", "application/x-executable",
    };

    private static readonly HashSet<string> s_textualApplicationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json", "application/xml", "image/svg+xml", "application/x-yaml", "application/yaml", "application/csv",
        "application/rtf", "application/x-sh", "application/ld+json", "application/xhtml+xml", "application/rss+xml", "application/atom+xml",
    };

    private static readonly string[] s_oleTypes =
    [
        "application/msword", "application/vnd.ms-excel", "application/vnd.ms-powerpoint", "application/vnd.ms-outlook", "application/vnd.visio",
    ];

    private static readonly string[] s_isoMediaTypes =
    [
        "video/mp4", "audio/mp4", "video/quicktime", "video/3gpp", "video/3gpp2", "image/heic", "image/heif", "image/avif",
    ];

    /// <summary>The type check as a verdict: pending until detection has run, failed when it could not, otherwise clean or rejected.</summary>
    public static ScanVerdict Check(string name, string? claimedType, bool detected, string? detectedType, string? detectorError = null)
    {
        if (!detected)
        {
            return detectorError is null
                ? ScanVerdict.Pending
                : ScanVerdict.Failed($"Type detection failed: {detectorError}");
        }

        var detectedNormalized = Normalize(detectedType);

        foreach (var claim in Claims(name, claimedType))
        {
            if (!IsConsistent(claim, detectedNormalized))
            {
                return new ScanVerdict(
                    ScanOutcome.Rejected,
                    $"The file's content is {detectedNormalized ?? "of an unknown type"}, not the claimed {claim}.",
                    detectedType);
            }
        }

        return new ScanVerdict(ScanOutcome.Clean, null, detectedType);
    }

    /// <summary>The claimed type and the type the name's extension implies, normalized and deduplicated.</summary>
    public static IEnumerable<string> Claims(string name, string? claimedType)
    {
        var claims = new List<string>(2);

        if (Normalize(claimedType) is { } claimed && claimed != OctetStream)
        {
            claims.Add(claimed);
        }

        if (!string.IsNullOrEmpty(name) && s_extensions.TryGetContentType(name, out var byExtension)
            && Normalize(byExtension) is { } extensionType && extensionType != OctetStream && !claims.Contains(extensionType))
        {
            claims.Add(extensionType);
        }

        return claims;
    }

    public static bool IsConsistent(string claimed, string? detected)
    {
        claimed = Normalize(claimed) ?? OctetStream;
        detected = Normalize(detected);

        if (detected is null || detected == OctetStream)
        {
            // Unidentified content: fine unless the claim is a type the detector would have recognised.
            return !s_signatureTypes.Contains(claimed);
        }

        if (claimed == detected)
        {
            return true;
        }

        if (IsTextual(detected))
        {
            return IsTextual(claimed);
        }

        return detected switch
        {
            "application/zip" => IsZipBased(claimed),
            "application/x-ole-storage" => s_oleTypes.Contains(claimed),
            _ when s_isoMediaTypes.Contains(detected) => s_isoMediaTypes.Contains(claimed),
            _ => false,
        };
    }

    public static string? Normalize(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        var separator = type.IndexOf(';');
        var bare = (separator >= 0 ? type[..separator] : type).Trim().ToLowerInvariant();

        return s_aliases.TryGetValue(bare, out var alias) ? alias : bare;
    }

    private static bool IsTextual(string type)
        => type.StartsWith("text/", StringComparison.Ordinal) || s_textualApplicationTypes.Contains(type);

    // Office Open XML, OpenDocument, EPUB, Java archives and other zip-packaged formats.
    private static bool IsZipBased(string type)
        => type.StartsWith("application/vnd.openxmlformats-officedocument.", StringComparison.Ordinal)
        || type.StartsWith("application/vnd.oasis.opendocument.", StringComparison.Ordinal)
        || type.StartsWith("application/vnd.ms-", StringComparison.Ordinal) && type.Contains(".macroenabled", StringComparison.Ordinal)
        || type.EndsWith("+zip", StringComparison.Ordinal)
        || type is "application/epub+zip" or "application/java-archive" or "application/vnd.android.package-archive" or "application/x-zip";
}
