#nullable enable

using Grove.Media.Files;
using Grove.Media.Scanning.Services;

namespace Grove.Tests.Media.Scanning;

/// <summary>Requirement 87: the content must match the claimed type and the name's extension.</summary>
public class TypeConsistencyTests
{
    [Theory]
    [InlineData("photo.png", "image/png", "image/png")]
    [InlineData("photo.jpg", "image/jpg", "image/jpeg")]
    [InlineData("report.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/zip")]
    [InlineData("sheet.xls", "application/vnd.ms-excel", "application/x-ole-storage")]
    [InlineData("clip.m4a", "audio/mp4", "video/mp4")]
    [InlineData("notes.txt", "text/plain", "text/plain")]
    [InlineData("data.json", "application/json", "text/plain")]
    [InlineData("blob.bin", null, null)]
    [InlineData("unnamed", "application/x-custom", "application/octet-stream")]
    public void MatchingContentIsClean(string name, string? claimed, string? detected)
        => Assert.Equal(ScanOutcome.Clean, TypeConsistency.Check(name, claimed, detected: true, detected).Outcome);

    [Theory]
    [InlineData("photo.png", "image/png", "application/x-dosexec")]
    [InlineData("photo.png", "image/png", "image/jpeg")]
    [InlineData("photo.png", "image/png", null)]
    [InlineData("invoice.pdf", "application/pdf", "text/plain")]
    [InlineData("notes.txt", "text/plain", "application/x-executable")]
    [InlineData("photo.jpg", "image/png", "image/png")]
    public void AMismatchIsRejected(string name, string? claimed, string? detected)
    {
        var verdict = TypeConsistency.Check(name, claimed, detected: true, detected);

        Assert.Equal(ScanOutcome.Rejected, verdict.Outcome);
        Assert.False(string.IsNullOrEmpty(verdict.Reason));
    }

    [Fact]
    public void BeforeDetectionItIsPendingAndADetectorErrorFails()
    {
        Assert.Equal(ScanOutcome.Pending, TypeConsistency.Check("a.png", "image/png", detected: false, null).Outcome);
        Assert.Equal(ScanOutcome.Failed, TypeConsistency.Check("a.png", "image/png", detected: false, null, "libmagic missing").Outcome);
    }
}
