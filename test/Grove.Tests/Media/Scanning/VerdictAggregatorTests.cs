#nullable enable

using Grove.Media.Files;
using Grove.Media.Scanning.Services;

namespace Grove.Tests.Media.Scanning;

/// <summary>Requirement 92: any rejection rejects, any failure fails, only all-clean with a matching type is clean.</summary>
public class VerdictAggregatorTests
{
    private static readonly ScanVerdict s_typeOk = new(ScanOutcome.Clean, null, "image/png");

    [Fact]
    public void AllCleanWithAMatchingTypeIsClean()
    {
        var verdict = VerdictAggregator.Aggregate([ScanVerdict.Clean, ScanVerdict.Clean], s_typeOk);

        Assert.Equal(ScanOutcome.Clean, verdict.Outcome);
        Assert.Equal("image/png", verdict.DetectedType);
    }

    [Fact]
    public void AnyRejectionRejectsEvenBesideFailuresAndPending()
    {
        var verdict = VerdictAggregator.Aggregate([ScanVerdict.Failed("down"), ScanVerdict.Rejected("Eicar"), ScanVerdict.Pending, null], s_typeOk);

        Assert.Equal(ScanOutcome.Rejected, verdict.Outcome);
        Assert.Equal("Eicar", verdict.Reason);
    }

    [Fact]
    public void AnyFailureFailsBeforePending()
    {
        var verdict = VerdictAggregator.Aggregate([ScanVerdict.Clean, ScanVerdict.Failed("timeout"), ScanVerdict.Pending], s_typeOk);

        Assert.Equal(ScanOutcome.Failed, verdict.Outcome);
        Assert.Contains("timeout", verdict.Reason);
    }

    [Fact]
    public void AScannerStillRunningOrNotRunIsPending()
    {
        Assert.Equal(ScanOutcome.Pending, VerdictAggregator.Aggregate([ScanVerdict.Clean, ScanVerdict.Pending], s_typeOk).Outcome);
        Assert.Equal(ScanOutcome.Pending, VerdictAggregator.Aggregate([ScanVerdict.Clean, null], s_typeOk).Outcome);
    }

    [Fact]
    public void CleanScannersWithoutTypeDetectionArePending()
        => Assert.Equal(ScanOutcome.Pending, VerdictAggregator.Aggregate([ScanVerdict.Clean], ScanVerdict.Pending).Outcome);

    [Fact]
    public void AFailedTypeDetectionFails()
        => Assert.Equal(ScanOutcome.Failed, VerdictAggregator.Aggregate([ScanVerdict.Clean], ScanVerdict.Failed("no libmagic")).Outcome);

    [Fact]
    public void ATypeMismatchRejectsWhateverTheScannersSay()
    {
        var verdict = VerdictAggregator.Aggregate([ScanVerdict.Clean], new ScanVerdict(ScanOutcome.Rejected, "not a png", "application/x-dosexec"));

        Assert.Equal(ScanOutcome.Rejected, verdict.Outcome);
        Assert.Equal("not a png", verdict.Reason);
    }

    [Fact]
    public void NoScannersFailsUnlessTheHostAllowsIt()
    {
        Assert.Equal(ScanOutcome.Failed, VerdictAggregator.Aggregate([], s_typeOk).Outcome);
        Assert.Equal(VerdictAggregator.NoScannersReason, VerdictAggregator.Aggregate([], s_typeOk).Reason);
        Assert.Equal(ScanOutcome.Clean, VerdictAggregator.Aggregate([], s_typeOk, allowNoScanners: true).Outcome);
    }
}
