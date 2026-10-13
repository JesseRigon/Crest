using System.Reflection;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace Crest.Media.Scanning.Processing;

/// <summary>
/// Removes location from a JPEG without re-encoding it (requirement 87): the EXIF segment is
/// rewritten without its GPS tags (parsed and written by ImageSharp), an XMP segment that carries
/// GPS is dropped, and every other byte, the image data included, is copied as it was.
/// </summary>
public static class JpegLocationStripper
{
    private const byte Marker = 0xFF;
    private const byte StartOfImage = 0xD8;
    private const byte StartOfScan = 0xDA;
    private const byte EndOfImage = 0xD9;
    private const byte App1 = 0xE1;

    private static readonly byte[] s_exifHeader = "Exif\0\0"u8.ToArray();
    private static readonly byte[] s_xmpHeader = "http://ns.adobe.com/xap/1.0/\0"u8.ToArray();
    private static readonly byte[] s_xmpGps = "GPS"u8.ToArray();

    private static readonly ExifTag[] s_gpsTags = typeof(ExifTag)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(property => property.Name.StartsWith("GPS", StringComparison.Ordinal) && typeof(ExifTag).IsAssignableFrom(property.PropertyType))
        .Select(property => (ExifTag)property.GetValue(null)!)
        .Concat(typeof(ExifTag)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.Name.StartsWith("GPS", StringComparison.Ordinal) && typeof(ExifTag).IsAssignableFrom(field.FieldType))
            .Select(field => (ExifTag)field.GetValue(null)!))
        .ToArray();

    /// <summary>The JPEG without location; false when it had none (or is not a JPEG), so nothing needs rewriting.</summary>
    public static bool TryStrip(ReadOnlySpan<byte> jpeg, out byte[] stripped)
    {
        stripped = [];

        if (jpeg.Length < 4 || jpeg[0] != Marker || jpeg[1] != StartOfImage)
        {
            return false;
        }

        using var output = new MemoryStream(jpeg.Length);
        output.Write(jpeg[..2]);

        var changed = false;
        var position = 2;

        while (position < jpeg.Length)
        {
            if (jpeg[position] != Marker)
            {
                // Not a marker where one belongs: leave the file alone.
                return false;
            }

            var markerStart = position;
            while (position < jpeg.Length && jpeg[position] == Marker)
            {
                position++;
            }

            if (position >= jpeg.Length)
            {
                return false;
            }

            var marker = jpeg[position++];

            if (marker is StartOfScan or EndOfImage)
            {
                // The entropy-coded data and everything after it are copied as they are.
                output.Write(jpeg[markerStart..]);
                break;
            }

            if (marker is >= 0xD0 and <= 0xD7 or 0x01)
            {
                output.Write(jpeg[markerStart..position]);
                continue;
            }

            if (position + 2 > jpeg.Length)
            {
                return false;
            }

            var length = (jpeg[position] << 8) | jpeg[position + 1];
            if (length < 2 || position + length > jpeg.Length)
            {
                return false;
            }

            var data = jpeg.Slice(position + 2, length - 2);
            var segmentEnd = position + length;

            if (marker == App1 && data.StartsWith(s_exifHeader))
            {
                if (StripExif(data[s_exifHeader.Length..]) is { } exif)
                {
                    changed = true;
                    WriteSegment(output, App1, [.. s_exifHeader, .. exif]);
                    position = segmentEnd;
                    continue;
                }
            }
            else if (marker == App1 && data.StartsWith(s_xmpHeader) && data.IndexOf(s_xmpGps) >= 0)
            {
                changed = true;
                position = segmentEnd;
                continue;
            }

            output.Write(jpeg[markerStart..segmentEnd]);
            position = segmentEnd;
        }

        if (!changed)
        {
            return false;
        }

        stripped = output.ToArray();
        return true;
    }

    /// <summary>The EXIF block without GPS tags; null when it had none.</summary>
    private static byte[]? StripExif(ReadOnlySpan<byte> tiff)
    {
        var profile = new ExifProfile(tiff.ToArray());
        var removed = false;

        foreach (var tag in s_gpsTags)
        {
            removed |= profile.RemoveValue(tag);
        }

        if (!removed)
        {
            return null;
        }

        return profile.ToByteArray() ?? [];
    }

    private static void WriteSegment(Stream output, byte marker, byte[] data)
    {
        var length = data.Length + 2;
        if (length > ushort.MaxValue)
        {
            throw new InvalidOperationException("The rewritten EXIF segment is too large.");
        }

        output.WriteByte(Marker);
        output.WriteByte(marker);
        output.WriteByte((byte)(length >> 8));
        output.WriteByte((byte)length);
        output.Write(data);
    }
}
