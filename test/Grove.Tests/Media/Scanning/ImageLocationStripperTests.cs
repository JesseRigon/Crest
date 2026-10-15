using System.Buffers.Binary;
using System.Text;
using Grove.Media.Scanning.Processing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace Grove.Tests.Media.Scanning;

// Location removal beyond JPEG (requirement 87): PNG and WebP metadata chunks and HEIC's Exif
// and XMP items lose their GPS data in place; the image itself is untouched.
public sealed class ImageLocationStripperTests
{
    [Fact]
    public void PngExifLosesItsLocation()
    {
        var original = Encode((image, stream) => image.SaveAsPng(stream));

        Assert.True(ImageLocationStripper.TryStrip("image/png", original, out var stripped));
        AssertNoLocation(stripped);
        Assert.False(ImageLocationStripper.TryStrip("image/png", stripped, out _));
    }

    [Fact]
    public void WebPExifLosesItsLocation()
    {
        var original = Encode((image, stream) => image.SaveAsWebp(stream));

        Assert.True(ImageLocationStripper.TryStrip("image/webp", original, out var stripped));
        AssertNoLocation(stripped);
    }

    [Fact]
    public void HeicExifAndXmpItemsLoseTheirLocationInPlace()
    {
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "Camera");
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLatitude, [new Rational(51), new Rational(30), new Rational(0)]);
        var tiff = exif.ToByteArray()!;
        var xmp = Encoding.UTF8.GetBytes("""<x:xmpmeta><rdf:Description exif:GPSLatitude="51,30N" tiff:Make="Camera"><exif:GPSAltitude>12</exif:GPSAltitude></rdf:Description></x:xmpmeta>""");

        var heic = Heic(tiff, xmp);
        Assert.True(ImageLocationStripper.TryStrip("image/heic", heic, out var stripped));
        Assert.Equal(heic.Length, stripped.Length);

        var text = Encoding.Latin1.GetString(stripped);
        Assert.DoesNotContain("GPS", text);
        Assert.Contains("tiff:Make=\"Camera\"", text);
    }

    [Fact]
    public void OtherTypesAreLeftAlone() =>
        Assert.False(ImageLocationStripper.TryStrip("image/gif", [1, 2, 3], out _));

    private static byte[] Encode(Action<Image<Rgb24>, Stream> save)
    {
        using var image = new Image<Rgb24>(8, 8);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "Camera");
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLatitude, [new Rational(51), new Rational(30), new Rational(0)]);
        image.Metadata.ExifProfile = exif;

        using var buffer = new MemoryStream();
        save(image, buffer);
        return buffer.ToArray();
    }

    private static void AssertNoLocation(byte[] encoded)
    {
        using var result = Image.Load(encoded);
        var profile = result.Metadata.ExifProfile;
        Assert.NotNull(profile);
        Assert.False(profile.TryGetValue(ExifTag.GPSLatitude, out _));
        Assert.True(profile.TryGetValue(ExifTag.Make, out var make));
        Assert.Equal("Camera", make.Value);
    }

    // ftyp, a meta box naming an Exif item (1) and an XMP mime item (2) with iloc v0, and mdat.
    private static byte[] Heic(byte[] tiff, byte[] xmp)
    {
        var exifItem = new byte[4 + tiff.Length];
        tiff.CopyTo(exifItem, 4);

        byte[] Box(string type, params byte[][] content)
        {
            var body = content.SelectMany(part => part).ToArray();
            var box = new byte[8 + body.Length];
            BinaryPrimitives.WriteUInt32BigEndian(box, (uint)box.Length);
            Encoding.ASCII.GetBytes(type).CopyTo(box, 4);
            body.CopyTo(box, 8);
            return box;
        }

        byte[] U16(int value) { var b = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)value); return b; }
        byte[] U32(long value) { var b = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, (uint)value); return b; }
        byte[] FullBoxHeader(byte version) => [version, 0, 0, 0];

        var ftyp = Box("ftyp", "heic"u8.ToArray(), U32(0), "mif1heic"u8.ToArray());
        var iinf = Box("iinf", FullBoxHeader(0), U16(2),
            Box("infe", FullBoxHeader(2), U16(1), U16(0), "Exif"u8.ToArray(), [0]),
            Box("infe", FullBoxHeader(2), U16(2), U16(0), "mime"u8.ToArray(), [0], "application/rdf+xml\0"u8.ToArray()));

        byte[] Iloc(long exifOffset, long xmpOffset) => Box("iloc", FullBoxHeader(0), [0x44, 0x00], U16(2),
            U16(1), U16(0), U16(1), U32(exifOffset), U32(exifItem.Length),
            U16(2), U16(0), U16(1), U32(xmpOffset), U32(xmp.Length));

        // Lay out once to learn where mdat's payload starts, then fill in the offsets.
        var meta = Box("meta", FullBoxHeader(0), iinf, Iloc(0, 0));
        var dataStart = ftyp.Length + meta.Length + 8;
        meta = Box("meta", FullBoxHeader(0), iinf, Iloc(dataStart, dataStart + exifItem.Length));
        var mdat = Box("mdat", exifItem, xmp);

        return [.. ftyp, .. meta, .. mdat];
    }
}
