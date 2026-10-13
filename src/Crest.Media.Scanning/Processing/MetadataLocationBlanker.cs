using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace Crest.Media.Scanning.Processing;

/// <summary>
/// Removes location from an EXIF (TIFF) block or an XMP packet IN PLACE, without changing its
/// size, so a container whose other structures point at byte offsets (HEIC's item locations)
/// stays valid: the GPS IFD is emptied and its values zeroed, and GPS properties in XMP are
/// overwritten with spaces.
/// </summary>
public static partial class MetadataLocationBlanker
{
    private const ushort GpsInfoTag = 0x8825;

    /// <summary>Empties the GPS IFD of a TIFF block; false when it had none or is not TIFF.</summary>
    public static bool BlankExif(Span<byte> tiff)
    {
        if (tiff.Length < 8)
        {
            return false;
        }

        bool little;
        if (tiff[0] == 'I' && tiff[1] == 'I')
        {
            little = true;
        }
        else if (tiff[0] == 'M' && tiff[1] == 'M')
        {
            little = false;
        }
        else
        {
            return false;
        }

        if (U16(tiff, 2, little) != 42)
        {
            return false;
        }

        var ifd0 = U32(tiff, 4, little);
        if (!TryEntries(tiff, ifd0, little, out var count))
        {
            return false;
        }

        for (var index = 0; index < count; index++)
        {
            var entry = (int)ifd0 + 2 + (index * 12);
            if (U16(tiff, entry, little) != GpsInfoTag)
            {
                continue;
            }

            var gps = U32(tiff, entry + 8, little);
            if (!TryEntries(tiff, gps, little, out var gpsCount) || gpsCount == 0)
            {
                return false;
            }

            for (var gpsIndex = 0; gpsIndex < gpsCount; gpsIndex++)
            {
                var gpsEntry = (int)gps + 2 + (gpsIndex * 12);
                var size = (long)TypeSize(U16(tiff, gpsEntry + 2, little)) * U32(tiff, gpsEntry + 4, little);
                if (size > 4)
                {
                    var offset = U32(tiff, gpsEntry + 8, little);
                    if (offset + size <= tiff.Length)
                    {
                        tiff.Slice((int)offset, (int)size).Clear();
                    }
                }
            }

            // No entries, and the zeroed bytes after the count read as "no next IFD".
            tiff.Slice((int)gps, 2 + (gpsCount * 12)).Clear();
            return true;
        }

        return false;
    }

    /// <summary>Overwrites GPS properties in an XMP packet with spaces; false when it had none.</summary>
    public static bool BlankXmp(Span<byte> xmp)
    {
        // Latin-1 maps every byte to one char, so match positions are byte positions.
        var text = Encoding.Latin1.GetString(xmp);
        var changed = false;

        foreach (var regex in new[] { GpsElement(), GpsAttribute() })
        {
            foreach (Match match in regex.Matches(text))
            {
                xmp.Slice(match.Index, match.Length).Fill((byte)' ');
                changed = true;
            }

            if (changed)
            {
                text = Encoding.Latin1.GetString(xmp);
            }
        }

        return changed;
    }

    [GeneratedRegex(@"<([\w.-]+:GPS[\w]*)\b[^>]*?(/>|>.*?</\1\s*>)", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex GpsElement();

    [GeneratedRegex(@"[\w.-]+:GPS\w*\s*=\s*(""[^""]*""|'[^']*')", RegexOptions.CultureInvariant)]
    private static partial Regex GpsAttribute();

    private static bool TryEntries(ReadOnlySpan<byte> tiff, uint offset, bool little, out int count)
    {
        count = 0;
        if (offset + 2 > tiff.Length)
        {
            return false;
        }

        count = U16(tiff, (int)offset, little);
        return offset + 2 + (count * 12L) <= tiff.Length;
    }

    private static int TypeSize(ushort type) => type switch
    {
        1 or 2 or 6 or 7 => 1,
        3 or 8 => 2,
        4 or 9 or 11 => 4,
        5 or 10 or 12 => 8,
        _ => 1,
    };

    private static ushort U16(ReadOnlySpan<byte> data, int offset, bool little) =>
        little ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]) : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

    private static uint U32(ReadOnlySpan<byte> data, int offset, bool little) =>
        little ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]) : BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
}
