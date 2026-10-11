using System.Text;
using System.Text.Unicode;
using Crest.Media.Files;

namespace Crest.Media.Scanning.Detection;

/// <summary>
/// The built-in type detector, the fallback when libmagic is not configured: magic numbers for
/// the common types (images, PDF, zip and Office, OLE, ISO media, audio and video, executables),
/// and plain text. Null when the content is not recognised.
/// </summary>
public sealed class SignatureFileTypeDetector : IFileTypeDetector
{
    public const string DetectorName = "signature";

    private const int HeaderSize = 64 * 1024;

    public string Name => DetectorName;

    public async Task<string?> DetectAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var buffer = new byte[HeaderSize];
        var length = await content.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);

        return Detect(buffer.AsSpan(0, length));
    }

    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.IsEmpty)
        {
            return null;
        }

        if (header.StartsWith(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        if (header.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return "image/jpeg";
        }

        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
        {
            return "image/gif";
        }

        if (header.Length >= 12 && header.StartsWith("RIFF"u8))
        {
            var form = header.Slice(8, 4);
            if (form.SequenceEqual("WEBP"u8))
            {
                return "image/webp";
            }

            if (form.SequenceEqual("WAVE"u8))
            {
                return "audio/wav";
            }

            if (form.SequenceEqual("AVI "u8))
            {
                return "video/avi";
            }
        }

        // PDF readers accept the header anywhere in the first 1024 bytes.
        if (header[..Math.Min(header.Length, 1024)].IndexOf("%PDF-"u8) >= 0)
        {
            return "application/pdf";
        }

        if (header.StartsWith("PK\x03\x04"u8) || header.StartsWith("PK\x05\x06"u8) || header.StartsWith("PK\x07\x08"u8))
        {
            return DetectZip(header);
        }

        if (header.StartsWith(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }))
        {
            return "application/x-ole-storage";
        }

        if (header.Length >= 12 && header.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return DetectIsoMedia(header.Slice(8, 4));
        }

        if (header.StartsWith(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
        {
            return "video/webm";
        }

        if (header.StartsWith("OggS"u8))
        {
            return "audio/ogg";
        }

        if (header.Length >= 10 && header.StartsWith("ID3"u8) && header[3] is >= 2 and <= 4)
        {
            return "audio/mpeg";
        }

        // The four reserved bytes keep text that starts with "BM" from passing as a bitmap.
        if (header.Length >= 26 && header.StartsWith("BM"u8) && header.Slice(6, 4).IndexOfAnyExcept((byte)0) < 0)
        {
            return "image/bmp";
        }

        if (header.StartsWith("II*\0"u8) || header.StartsWith("MM\0*"u8))
        {
            return "image/tiff";
        }

        if (header.StartsWith(new byte[] { 0x1F, 0x8B }))
        {
            return "application/gzip";
        }

        if (header.Length >= 64 && header.StartsWith("MZ"u8) && !IsText(header))
        {
            return "application/x-dosexec";
        }

        if (header.StartsWith(new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F' }))
        {
            return "application/x-executable";
        }

        return IsText(header) ? "text/plain" : null;
    }

    private static string DetectZip(ReadOnlySpan<byte> header)
    {
        // OpenDocument and EPUB: a first, stored entry named "mimetype" holding the type.
        if (header.Length > 38 && header.Slice(30, 8).SequenceEqual("mimetype"u8))
        {
            var rest = header[38..];
            var end = rest.IndexOf("PK"u8);
            var type = Encoding.ASCII.GetString(end > 0 ? rest[..end] : rest[..Math.Min(rest.Length, 80)]).Trim();

            if (type.StartsWith("application/", StringComparison.Ordinal) && type.All(c => c > ' ' && c < 0x7F))
            {
                return type;
            }
        }

        // Office Open XML: the parts' folder names in the local file headers.
        if (header.IndexOf("[Content_Types].xml"u8) >= 0)
        {
            if (header.IndexOf("word/"u8) >= 0)
            {
                return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            }

            if (header.IndexOf("xl/"u8) >= 0)
            {
                return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            }

            if (header.IndexOf("ppt/"u8) >= 0)
            {
                return "application/vnd.openxmlformats-officedocument.presentationml.presentation";
            }
        }

        return "application/zip";
    }

    private static string DetectIsoMedia(ReadOnlySpan<byte> brand)
    {
        var name = Encoding.ASCII.GetString(brand);

        return name switch
        {
            "qt  " => "video/quicktime",
            "heic" or "heix" or "heim" or "heis" or "hevc" or "hevx" => "image/heic",
            "mif1" or "msf1" => "image/heif",
            "avif" or "avis" => "image/avif",
            "M4A " or "M4B " or "M4P " => "audio/mp4",
            _ when name.StartsWith("3g2", StringComparison.Ordinal) => "video/3gpp2",
            _ when name.StartsWith("3g", StringComparison.Ordinal) => "video/3gpp",
            _ => "video/mp4",
        };
    }

    /// <summary>Valid UTF-8 (or ASCII) with no control characters other than whitespace.</summary>
    private static bool IsText(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            header = header[3..];
        }

        foreach (var b in header)
        {
            if (b < 0x20 && b is not (byte)'\t' and not (byte)'\n' and not (byte)'\r' and not 0x0C)
            {
                return false;
            }

            if (b == 0x7F)
            {
                return false;
            }
        }

        // A multi-byte character cut off at the end of the header is still text.
        for (var trim = 0; trim < 4 && trim < header.Length; trim++)
        {
            if (Utf8.IsValid(header[..^trim]))
            {
                return true;
            }
        }

        return false;
    }
}
