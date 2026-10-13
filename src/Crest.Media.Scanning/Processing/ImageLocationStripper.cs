using System.Buffers.Binary;
using System.Text;

namespace Crest.Media.Scanning.Processing;

/// <summary>
/// Removes location from an uploaded image before it is committed (requirement 87), by its
/// detected type, never re-encoding the image: JPEG through <see cref="JpegLocationStripper"/>;
/// PNG (<c>eXIf</c>, uncompressed XMP <c>iTXt</c>), WebP (<c>EXIF</c>, <c>XMP </c>) and HEIC/HEIF
/// (the <c>Exif</c> and XMP items the <c>meta</c> box locates) through
/// <see cref="MetadataLocationBlanker"/>, in place.
/// </summary>
public static class ImageLocationStripper
{
    /// <summary>The types location is removed from.</summary>
    public static bool Handles(string? mimeType) => mimeType is "image/jpeg" or "image/png" or "image/webp" or "image/heic" or "image/heif";

    /// <summary>The image without location; false when it had none (or is not that type).</summary>
    public static bool TryStrip(string? mimeType, byte[] image, out byte[] stripped)
    {
        if (mimeType == "image/jpeg")
        {
            return JpegLocationStripper.TryStrip(image, out stripped);
        }

        stripped = (byte[])image.Clone();
        var changed = mimeType switch
        {
            "image/png" => StripPng(stripped),
            "image/webp" => StripWebP(stripped),
            "image/heic" or "image/heif" => StripHeif(stripped),
            _ => false,
        };

        if (!changed)
        {
            stripped = [];
        }

        return changed;
    }

    private static readonly byte[] s_pngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] s_exifPrefix = "Exif\0\0"u8.ToArray();
    private static readonly byte[] s_xmpKeyword = "XML:com.adobe.xmp\0"u8.ToArray();

    private static bool StripPng(byte[] png)
    {
        if (!png.AsSpan().StartsWith(s_pngSignature))
        {
            return false;
        }

        var changed = false;
        var position = s_pngSignature.Length;
        while (position + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position));
            if (length > png.Length - position - 12)
            {
                break;
            }

            var type = Encoding.ASCII.GetString(png, position + 4, 4);
            var data = png.AsSpan(position + 8, (int)length);
            var chunkChanged = type switch
            {
                "eXIf" => MetadataLocationBlanker.BlankExif(data),
                "iTXt" when data.StartsWith(s_xmpKeyword) && data.Length > s_xmpKeyword.Length && data[s_xmpKeyword.Length] == 0 =>
                    MetadataLocationBlanker.BlankXmp(data[s_xmpKeyword.Length..]),
                _ => false,
            };

            if (chunkChanged)
            {
                BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(position + 8 + (int)length), Crc32(png.AsSpan(position + 4, 4 + (int)length)));
                changed = true;
            }

            if (type == "IEND")
            {
                break;
            }

            position += 12 + (int)length;
        }

        return changed;
    }

    private static bool StripWebP(byte[] webp)
    {
        if (webp.Length < 12 || !webp.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !webp.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return false;
        }

        var changed = false;
        var position = 12;
        while (position + 8 <= webp.Length)
        {
            var size = BinaryPrimitives.ReadUInt32LittleEndian(webp.AsSpan(position + 4));
            if (size > webp.Length - position - 8)
            {
                break;
            }

            var data = webp.AsSpan(position + 8, (int)size);
            var fourCc = webp.AsSpan(position, 4);
            if (fourCc.SequenceEqual("EXIF"u8))
            {
                changed |= MetadataLocationBlanker.BlankExif(data.StartsWith(s_exifPrefix) ? data[s_exifPrefix.Length..] : data);
            }
            else if (fourCc.SequenceEqual("XMP "u8))
            {
                changed |= MetadataLocationBlanker.BlankXmp(data);
            }

            position += 8 + (int)size + (int)(size & 1);
        }

        return changed;
    }

    // ISO base media: the meta box's iinf names the Exif and XMP (mime) items, its iloc says
    // where their bytes are. Only items stored as one extent in this file are touched.
    private static bool StripHeif(byte[] heif)
    {
        var meta = FindBox(heif, 0, heif.Length, "meta");
        if (meta is not var (metaStart, metaEnd))
        {
            return false;
        }

        // A FullBox: version and flags precede the children.
        var children = metaStart + 4;
        var items = ReadItemTypes(heif, FindBox(heif, children, metaEnd, "iinf"));
        var locations = ReadLocations(heif, FindBox(heif, children, metaEnd, "iloc"));

        var changed = false;
        foreach (var (itemId, kind) in items)
        {
            if (!locations.TryGetValue(itemId, out var location) || location.Offset + location.Length > heif.Length)
            {
                continue;
            }

            var data = heif.AsSpan((int)location.Offset, (int)location.Length);
            if (kind == "Exif" && data.Length >= 4)
            {
                var headerOffset = BinaryPrimitives.ReadUInt32BigEndian(data);
                if (4 + headerOffset < data.Length)
                {
                    changed |= MetadataLocationBlanker.BlankExif(data[(4 + (int)headerOffset)..]);
                }
            }
            else if (kind == "xmp")
            {
                changed |= MetadataLocationBlanker.BlankXmp(data);
            }
        }

        return changed;
    }

    private static (int Start, int End)? FindBox(byte[] data, int start, int end, string type)
    {
        var position = start;
        while (position + 8 <= end)
        {
            long size = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position));
            var header = 8;
            if (size == 1)
            {
                if (position + 16 > end)
                {
                    return null;
                }

                size = (long)BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(position + 8));
                header = 16;
            }
            else if (size == 0)
            {
                size = end - position;
            }

            if (size < header || position + size > end)
            {
                return null;
            }

            if (Encoding.ASCII.GetString(data, position + 4, 4) == type)
            {
                return (position + header, (int)(position + size));
            }

            position += (int)size;
        }

        return null;
    }

    private static List<(uint ItemId, string Kind)> ReadItemTypes(byte[] data, (int Start, int End)? iinf)
    {
        var items = new List<(uint, string)>();
        if (iinf is not var (start, end) || start + 6 > end)
        {
            return items;
        }

        var position = start + 4 + (data[start] == 0 ? 2 : 4);
        while (FindBox(data, position, end, "infe") is var (infeStart, infeEnd))
        {
            var version = data[infeStart];
            var cursor = infeStart + 4;
            if (version >= 2 && cursor + 8 <= infeEnd)
            {
                uint itemId;
                if (version == 2)
                {
                    itemId = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(cursor));
                    cursor += 2;
                }
                else
                {
                    itemId = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(cursor));
                    cursor += 4;
                }

                cursor += 2; // item_protection_index
                var itemType = Encoding.ASCII.GetString(data, cursor, 4);
                cursor += 4;

                if (itemType == "Exif")
                {
                    items.Add((itemId, "Exif"));
                }
                else if (itemType == "mime")
                {
                    // item_name, then content_type, both null-terminated.
                    var nameEnd = Array.IndexOf(data, (byte)0, cursor, infeEnd - cursor);
                    if (nameEnd >= 0)
                    {
                        var typeEnd = Array.IndexOf(data, (byte)0, nameEnd + 1, infeEnd - nameEnd - 1);
                        var contentType = typeEnd > nameEnd ? Encoding.ASCII.GetString(data, nameEnd + 1, typeEnd - nameEnd - 1) : string.Empty;
                        if (contentType.Contains("rdf+xml", StringComparison.OrdinalIgnoreCase) || contentType.Contains("xmp", StringComparison.OrdinalIgnoreCase))
                        {
                            items.Add((itemId, "xmp"));
                        }
                    }
                }
            }

            position = infeEnd;
        }

        return items;
    }

    private static Dictionary<uint, (long Offset, long Length)> ReadLocations(byte[] data, (int Start, int End)? iloc)
    {
        var locations = new Dictionary<uint, (long, long)>();
        if (iloc is not var (start, end) || start + 8 > end)
        {
            return locations;
        }

        var version = data[start];
        var cursor = start + 4;
        var offsetSize = data[cursor] >> 4;
        var lengthSize = data[cursor] & 0xF;
        var baseOffsetSize = data[cursor + 1] >> 4;
        var indexSize = version is 1 or 2 ? data[cursor + 1] & 0xF : 0;
        cursor += 2;

        long Read(int size)
        {
            if (size == 0)
            {
                return 0;
            }

            if (cursor + size > end)
            {
                throw new InvalidDataException("iloc runs past its box.");
            }

            long value = size switch
            {
                4 => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(cursor)),
                8 => (long)BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(cursor)),
                2 => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(cursor)),
                _ => throw new InvalidDataException("Unsupported iloc field size."),
            };
            cursor += size;
            return value;
        }

        try
        {
            var itemCount = Read(version < 2 ? 2 : 4);
            for (var item = 0; item < itemCount; item++)
            {
                var itemId = (uint)Read(version < 2 ? 2 : 4);
                var constructionMethod = version is 1 or 2 ? Read(2) & 0xF : 0;
                Read(2); // data_reference_index
                var baseOffset = Read(baseOffsetSize);
                var extentCount = Read(2);
                long offset = 0, length = 0;
                for (var extent = 0; extent < extentCount; extent++)
                {
                    Read(indexSize);
                    offset = Read(offsetSize);
                    length = Read(lengthSize);
                }

                if (constructionMethod == 0 && extentCount == 1)
                {
                    locations[itemId] = (baseOffset + offset, length);
                }
            }
        }
        catch (InvalidDataException)
        {
        }

        return locations;
    }

    private static readonly uint[] s_crcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc = s_crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
