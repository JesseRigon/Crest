#nullable enable

using Crest.Media.Scanning.Detection;

namespace Crest.Tests.Media.Scanning;

/// <summary>The built-in fallback detector: magic numbers for the common types (requirement 91).</summary>
public class SignatureFileTypeDetectorTests
{
    private static byte[] Bytes(params byte[] bytes) => bytes;

    private static byte[] Ascii(string text, int padTo = 0)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        return padTo > bytes.Length ? [.. bytes, .. new byte[padTo - bytes.Length]] : bytes;
    }

    private static byte[] ZipEntry(string name, string content = "")
    {
        var header = new byte[30];
        "PK\x03\x04"u8.CopyTo(header);
        BitConverter.TryWriteBytes(header.AsSpan(26), (ushort)name.Length);
        return [.. header, .. Encoding.ASCII.GetBytes(name), .. Encoding.ASCII.GetBytes(content)];
    }

    private static byte[] Ftyp(string brand) => [0, 0, 0, 0x18, .. Ascii("ftyp"), .. Ascii(brand), 0, 0, 0, 0];

    public static TheoryData<string, byte[]> Known => new()
    {
        { "image/png", Bytes(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13) },
        { "image/jpeg", Bytes(0xFF, 0xD8, 0xFF, 0xE0, 0, 16, (byte)'J', (byte)'F', (byte)'I', (byte)'F') },
        { "image/gif", Ascii("GIF89a\x01\0\x01\0") },
        { "image/gif", Ascii("GIF87a\x01\0\x01\0") },
        { "image/webp", Ascii("RIFF\x24\0\0\0WEBPVP8 ") },
        { "application/pdf", Ascii("%PDF-1.7\n%\xE2\xE3") },
        { "application/zip", ZipEntry("readme.md", "hello") },
        { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [.. ZipEntry("[Content_Types].xml"), .. ZipEntry("word/document.xml")] },
        { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [.. ZipEntry("[Content_Types].xml"), .. ZipEntry("xl/workbook.xml")] },
        { "application/vnd.oasis.opendocument.text", ZipEntry("mimetype", "application/vnd.oasis.opendocument.text") },
        { "application/x-ole-storage", Bytes(0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0) },
        { "video/mp4", Ftyp("isom") },
        { "video/quicktime", Ftyp("qt  ") },
        { "image/heic", Ftyp("heic") },
        { "audio/mp4", Ftyp("M4A ") },
        { "application/x-executable", Bytes(0x7F, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1, 0) },
        { "text/plain", Ascii("Hello, world.\r\nSecond line\twith a tab.") },
        { "text/plain", Encoding.UTF8.GetBytes("Grüße, 世界") },
    };

    [Theory]
    [MemberData(nameof(Known))]
    public async Task DetectsTheCommonTypes(string expected, byte[] content)
        => Assert.Equal(expected, await new SignatureFileTypeDetector().DetectAsync(new MemoryStream(content), TestContext.Current.CancellationToken));

    [Fact]
    public void AnExecutableDisguisedWithAnotherNameIsStillAnExecutable()
    {
        var executable = Ascii("MZ", padTo: 128);

        Assert.Equal("application/x-dosexec", SignatureFileTypeDetector.Detect(executable));
    }

    [Fact]
    public void TextThatStartsLikeASignatureIsStillText()
    {
        Assert.Equal("text/plain", SignatureFileTypeDetector.Detect(Ascii("BMW sales figures for the third quarter of the year")));
        Assert.Equal("text/plain", SignatureFileTypeDetector.Detect(Ascii("MZ is a two-letter abbreviation used in this sentence on purpose.")));
    }

    [Fact]
    public void UnknownBinaryAndEmptyContentAreUnknown()
    {
        Assert.Null(SignatureFileTypeDetector.Detect(Bytes(0x00, 0x01, 0x02, 0x03, 0xFE)));
        Assert.Null(SignatureFileTypeDetector.Detect([]));
    }
}
