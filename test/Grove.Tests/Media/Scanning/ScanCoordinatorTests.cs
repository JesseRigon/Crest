#nullable enable

using Grove.Media.Files;
using Grove.Media.Scanning.Services;

namespace Grove.Tests.Media.Scanning;

/// <summary>
/// The quarantine flow (docs/media.md § 2): a clean verdict commits, a type mismatch rejects
/// without scanning, a pending offsite scanner reports back later, and a failure keeps the
/// upload quarantined and retries with backoff.
/// </summary>
public class ScanCoordinatorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AllCleanCommitsAndReportsTheDetectedType()
    {
        var world = new ScanningWorld();
        var clamAv = world.AddScanner("ClamAV", ScanVerdict.Clean);
        var job = await world.SubmitAsync(ScanningWorld.Png, "photo.png", "image/png");

        await world.Coordinator().RunAsync(job.ScanId, Token);

        Assert.Equal(1, clamAv.Calls);
        Assert.Equal(ScanOutcome.Clean, world.Jobs.Jobs[job.ScanId].Status);
        Assert.True(world.Storage.IsCommitted(job.StorageKey));
        Assert.False(world.Storage.IsQuarantined(job.StorageKey));
        var received = Assert.Single(world.Verdicts.Received);
        Assert.Equal(("c1", 1, ScanOutcome.Clean, "image/png"), (received.ContentId, received.Version, received.Verdict.Outcome, received.Verdict.DetectedType));
        Assert.Null(world.Jobs.Jobs[job.ScanId].NextAttemptUtc);
    }

    [Fact]
    public async Task ATypeMismatchIsRejectedWithoutScanning()
    {
        var world = new ScanningWorld();
        var clamAv = world.AddScanner("ClamAV", ScanVerdict.Clean);
        var executable = new byte[128];
        executable[0] = (byte)'M';
        executable[1] = (byte)'Z';
        var job = await world.SubmitAsync(executable, "photo.png", "image/png");

        await world.Coordinator().RunAsync(job.ScanId, Token);

        Assert.Equal(0, clamAv.Calls);
        Assert.Equal(ScanOutcome.Rejected, world.Jobs.Jobs[job.ScanId].Status);
        Assert.False(world.Storage.IsCommitted(job.StorageKey));
        Assert.False(world.Storage.IsQuarantined(job.StorageKey));
        Assert.Equal(ScanOutcome.Rejected, Assert.Single(world.Verdicts.Received).Verdict.Outcome);
    }

    [Fact]
    public async Task APendingScannerReportsBackAndTheVerdictCommits()
    {
        var world = new ScanningWorld();
        world.AddScanner("ClamAV", ScanVerdict.Clean);
        var offsite = world.AddScanner("Cloud", ScanVerdict.Pending);
        var job = await world.SubmitAsync(ScanningWorld.Png, "photo.png", "image/png");
        var coordinator = world.Coordinator();

        await coordinator.RunAsync(job.ScanId, Token);

        Assert.Equal(ScanOutcome.Pending, world.Jobs.Jobs[job.ScanId].Status);
        Assert.True(world.Storage.IsQuarantined(job.StorageKey));
        Assert.Empty(world.Verdicts.Received);

        // A rerun before the pending timeout does not ask the offsite scanner again.
        world.Clock.UtcNow += TimeSpan.FromMinutes(5);
        await coordinator.RunAsync(job.ScanId, Token);
        Assert.Equal(1, offsite.Calls);

        Assert.True(await coordinator.ReportAsync(job.ScanId, "Cloud", ScanVerdict.Clean, Token));

        Assert.Equal(ScanOutcome.Clean, world.Jobs.Jobs[job.ScanId].Status);
        Assert.True(world.Storage.IsCommitted(job.StorageKey));
        Assert.Equal(ScanOutcome.Clean, Assert.Single(world.Verdicts.Received).Verdict.Outcome);
    }

    [Fact]
    public async Task APendingScannerThatRejectsLaterRejects()
    {
        var world = new ScanningWorld();
        world.AddScanner("Cloud", ScanVerdict.Pending);
        var job = await world.SubmitAsync(ScanningWorld.Png, "photo.png", "image/png");
        var coordinator = world.Coordinator();
        await coordinator.RunAsync(job.ScanId, Token);

        await coordinator.ReportAsync(job.ScanId, "Cloud", ScanVerdict.Rejected("Trojan"), Token);

        Assert.Equal(ScanOutcome.Rejected, world.Jobs.Jobs[job.ScanId].Status);
        Assert.Equal("Trojan", world.Jobs.Jobs[job.ScanId].Reason);
        Assert.False(world.Storage.IsCommitted(job.StorageKey));
    }

    [Fact]
    public async Task AReportFromAScannerThatWasNotAskedIsRefused()
    {
        var world = new ScanningWorld();
        world.AddScanner("Cloud", ScanVerdict.Pending);
        var job = await world.SubmitAsync(ScanningWorld.Png, "photo.png", "image/png");
        var coordinator = world.Coordinator();
        await coordinator.RunAsync(job.ScanId, Token);

        Assert.False(await coordinator.ReportAsync(job.ScanId, "Impostor", ScanVerdict.Clean, Token));
        Assert.False(await coordinator.ReportAsync("no-such-scan", "Cloud", ScanVerdict.Clean, Token));
        Assert.Equal(ScanOutcome.Pending, world.Jobs.Jobs[job.ScanId].Status);
        Assert.False(world.Storage.IsCommitted(job.StorageKey));
    }

    [Fact]
    public async Task AFailureStaysQuarantinedAndRetriesWithBackoff()
    {
        var world = new ScanningWorld();
        var clamAv = world.AddScanner("ClamAV", ScanVerdict.Failed("clamd unreachable"), ScanVerdict.Failed("clamd unreachable"), ScanVerdict.Clean);
        var job = await world.SubmitAsync(ScanningWorld.Png, "photo.png", "image/png");
        var coordinator = world.Coordinator();
        var start = world.Clock.UtcNow;

        await coordinator.RunAsync(job.ScanId, Token);

        var stored = world.Jobs.Jobs[job.ScanId];
        Assert.Equal(ScanOutcome.Failed, stored.Status);
        Assert.True(world.Storage.IsQuarantined(job.StorageKey));
        Assert.False(world.Storage.IsCommitted(job.StorageKey));
        Assert.Equal(start + world.Options.RetryDelay, stored.NextAttemptUtc);
        Assert.Equal(ScanOutcome.Failed, Assert.Single(world.Verdicts.Received).Verdict.Outcome);

        world.Clock.UtcNow = stored.NextAttemptUtc!.Value;
        await coordinator.RunAsync(job.ScanId, Token);

        Assert.Equal(ScanOutcome.Failed, stored.Status);
        Assert.Equal(world.Clock.UtcNow + (world.Options.RetryDelay * 2), stored.NextAttemptUtc);

        // The failed verdict is delivered once, not on every retry.
        Assert.Single(world.Verdicts.Received);

        world.Clock.UtcNow = stored.NextAttemptUtc!.Value;
        await coordinator.RunAsync(job.ScanId, Token);

        Assert.Equal(3, clamAv.Calls);
        Assert.Equal(ScanOutcome.Clean, stored.Status);
        Assert.True(world.Storage.IsCommitted(job.StorageKey));
        Assert.Equal([ScanOutcome.Failed, ScanOutcome.Clean], world.Verdicts.Received.Select(received => received.Verdict.Outcome));
    }

    [Fact]
    public async Task AFailedCommitKeepsTheUploadQuarantined()
    {
        var world = new ScanningWorld();
        world.AddScanner("ClamAV", ScanVerdict.Clean);
        world.Storage.FailCommit = true;
        var job = await world.SubmitAsync(ScanningWorld.Png, "photo.png", "image/png");

        await world.Coordinator().RunAsync(job.ScanId, Token);

        Assert.Equal(ScanOutcome.Failed, world.Jobs.Jobs[job.ScanId].Status);
        Assert.True(world.Storage.IsQuarantined(job.StorageKey));
        Assert.Equal(ScanOutcome.Failed, Assert.Single(world.Verdicts.Received).Verdict.Outcome);
    }

    [Fact]
    public async Task WithoutAnyScannerTheUploadStaysQuarantined()
    {
        var world = new ScanningWorld();
        var job = await world.SubmitAsync(ScanningWorld.Png, "photo.png", "image/png");

        await world.Coordinator().RunAsync(job.ScanId, Token);

        Assert.Equal(ScanOutcome.Failed, world.Jobs.Jobs[job.ScanId].Status);
        Assert.Equal(VerdictAggregator.NoScannersReason, world.Jobs.Jobs[job.ScanId].Reason);
        Assert.True(world.Storage.IsQuarantined(job.StorageKey));
    }

    [Fact]
    public async Task TheBackoffIsCapped()
    {
        var world = new ScanningWorld();
        var coordinator = world.Coordinator();

        Assert.Equal(world.Options.RetryDelay, coordinator.Backoff(1));
        Assert.Equal(world.Options.RetryDelay * 4, coordinator.Backoff(3));
        Assert.Equal(world.Options.MaxRetryDelay, coordinator.Backoff(50));
        await Task.CompletedTask;
    }
}
