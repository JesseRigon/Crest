#nullable enable

using Grove.Media.Files;
using Grove.Media.Scanning.Processing;
using Grove.Media.Scanning.Scanners;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace Grove.Tests.Media.Scanning;

/// <summary>The providers' edges: ClamAV's answers as verdicts, the signed offsite callback, and location removal.</summary>
public class ProviderTests
{
    [Fact]
    public void ClamAvAnswersBecomeVerdicts()
    {
        Assert.Equal(ScanOutcome.Clean, ClamAvUploadScanner.ParseResponse("stream: OK\0").Outcome);

        var found = ClamAvUploadScanner.ParseResponse("stream: Eicar-Test-Signature FOUND");
        Assert.Equal(ScanOutcome.Rejected, found.Outcome);
        Assert.Contains("Eicar-Test-Signature", found.Reason);

        Assert.Equal(ScanOutcome.Failed, ClamAvUploadScanner.ParseResponse("INSTREAM size limit exceeded. ERROR").Outcome);
        Assert.Equal(ScanOutcome.Failed, ClamAvUploadScanner.ParseResponse(null).Outcome);
    }

    [Fact]
    public void ACallbackIsTrustedOnlyWithTheProvidersSecretAndAFreshTimestamp()
    {
        var body = """{"scanId":"s1","scanner":"Cloud","outcome":"clean"}"""u8.ToArray();
        var now = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        var timestamp = now.ToUnixTimeSeconds();
        var signature = ScanSignature.Sign("secret", timestamp, body);
        var skew = TimeSpan.FromMinutes(5);

        Assert.True(ScanSignature.Verify("secret", timestamp.ToString(CultureInfo.InvariantCulture), signature, body, now, skew));
        Assert.False(ScanSignature.Verify("other", timestamp.ToString(CultureInfo.InvariantCulture), signature, body, now, skew));
        Assert.False(ScanSignature.Verify("secret", timestamp.ToString(CultureInfo.InvariantCulture), signature, """{"scanId":"s1","scanner":"Cloud","outcome":"rejected"}"""u8, now, skew));
        Assert.False(ScanSignature.Verify("secret", timestamp.ToString(CultureInfo.InvariantCulture), signature, body, now.AddMinutes(10), skew));
        Assert.False(ScanSignature.Verify("secret", null, signature, body, now, skew));
        Assert.False(ScanSignature.Verify("secret", timestamp.ToString(CultureInfo.InvariantCulture), null, body, now, skew));
    }

    [Fact]
    public void AVerdictPayloadNeedsAKnownOutcome()
    {
        Assert.Equal(ScanOutcome.Clean, new VerdictPayload { Outcome = "clean" }.ToVerdict()?.Outcome);
        Assert.Equal("Rejected by the scanner.", new VerdictPayload { Outcome = "Rejected" }.ToVerdict()?.Reason);
        Assert.Null(new VerdictPayload { Outcome = "maybe" }.ToVerdict());
        Assert.Null(new VerdictPayload { Outcome = "42" }.ToVerdict());
    }

    [Fact]
    public void LocationIsRemovedFromAJpegAndTheRestIsKept()
    {
        byte[] original;
        using (var image = new Image<Rgb24>(8, 8))
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Make, "Camera");
            exif.SetValue(ExifTag.GPSLatitudeRef, "N");
            exif.SetValue(ExifTag.GPSLatitude, [new Rational(51), new Rational(30), new Rational(0)]);
            image.Metadata.ExifProfile = exif;

            using var buffer = new MemoryStream();
            image.SaveAsJpeg(buffer);
            original = buffer.ToArray();
        }

        Assert.True(JpegLocationStripper.TryStrip(original, out var stripped));

        using var result = Image.Load(stripped);
        var profile = result.Metadata.ExifProfile;
        Assert.NotNull(profile);
        Assert.False(profile.TryGetValue(ExifTag.GPSLatitude, out _));
        Assert.False(profile.TryGetValue(ExifTag.GPSLatitudeRef, out _));
        Assert.True(profile.TryGetValue(ExifTag.Make, out var make));
        Assert.Equal("Camera", make.Value);

        // Nothing to remove the second time.
        Assert.False(JpegLocationStripper.TryStrip(stripped, out _));
    }

    [Fact]
    public void NonJpegContentIsLeftAlone()
        => Assert.False(JpegLocationStripper.TryStrip(ScanningWorld.Png, out _));
}
