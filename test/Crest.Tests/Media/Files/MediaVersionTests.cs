#nullable enable
using Crest.Media.Files;
using static Crest.Tests.Media.Files.FilesWorld;

namespace Crest.Tests.Media.Files;

/// <summary>Version states (docs/media.md § 2-3): uploads land in quarantine, a clean verdict
/// commits and publishes the first version, rejected and failed ones are never served.</summary>
public class MediaVersionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(FilesWorld World, MediaObjectService Files, MediaItem File)> UploadedAsync()
    {
        var world = new FilesWorld();
        world.Quarantine = new RecordingQuarantine(world.Storage);
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var file = await files.UploadAsync(Upload(drive.Placement.Id, "f.txt", "first"), Token);
        return (world, files, file);
    }

    [Fact]
    public async Task AnUploadIsQuarantinedUntilItsVerdict()
    {
        var (world, files, file) = await UploadedAsync();

        var version = Assert.Single(file.Content.Versions);
        Assert.Equal(MediaVersionState.Quarantined, version.State);
        Assert.Equal(MediaBlobKeys.ForVersion(file.Content.Id, 1), version.BlobKey);
        Assert.Equal([(file.Content.Id, 1)], world.Quarantine!.Submitted);
        Assert.Null(file.Content.PublishedVersion);
        Assert.Null(await files.OpenReadAsync(file.Placement.Id, cancellationToken: Token));
        Assert.Null(await files.OpenReadAsync(file.Placement.Id, 1, Token));
    }

    [Fact]
    public async Task ACleanVerdictCommitsAndPublishesTheFirstVersion()
    {
        var (world, files, file) = await UploadedAsync();

        await files.OnVerdictAsync(file.Content.Id, 1, new ScanVerdict(ScanOutcome.Clean, DetectedType: "text/plain"), Token);

        var content = world.Store.Contents[file.Content.Id].Content;
        Assert.Equal(MediaVersionState.Clean, content.Versions[0].State);
        Assert.Equal("text/plain", content.Versions[0].DetectedType);
        Assert.Equal(1, content.PublishedVersion);
        Assert.True(world.Storage.Blobs.ContainsKey((MediaStorageArea.Committed, content.Versions[0].BlobKey)));
        Assert.False(world.Storage.Blobs.ContainsKey((MediaStorageArea.Quarantine, content.Versions[0].BlobKey)));
        await using var bytes = await files.OpenReadAsync(file.Placement.Id, cancellationToken: Token);
        Assert.Equal("first", await new StreamReader(bytes!).ReadToEndAsync(Token));
    }

    [Fact]
    public async Task SizeAndHashAreTakenFromTheCommittedBytes()
    {
        // Scanning may rewrite the bytes (location data stripped) before committing them.
        var (world, files, file) = await UploadedAsync();
        world.Storage.Blobs[(MediaStorageArea.Quarantine, MediaBlobKeys.ForVersion(file.Content.Id, 1))] = [1, 2, 3];

        await files.OnVerdictAsync(file.Content.Id, 1, ScanVerdict.Clean, Token);

        var version = world.Store.Contents[file.Content.Id].Content.Versions[0];
        Assert.Equal(3, version.Size);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(new byte[] { 1, 2, 3 })), version.Sha256);
    }

    [Fact]
    public async Task ARejectedVersionKeepsOnlyItsRecordAndReason()
    {
        var (world, files, file) = await UploadedAsync();

        await files.OnVerdictAsync(file.Content.Id, 1, ScanVerdict.Rejected("Eicar-Test-Signature"), Token);

        var version = world.Store.Contents[file.Content.Id].Content.Versions[0];
        Assert.Equal(MediaVersionState.Rejected, version.State);
        Assert.Equal("Eicar-Test-Signature", version.Reason);
        Assert.Empty(world.Storage.Blobs);
        await Assert.ThrowsAsync<MediaObjectConflictException>(() => files.PublishVersionAsync(file.Placement.Id, 1, Token));
    }

    [Fact]
    public async Task AFailedScanStaysInQuarantine()
    {
        var (world, files, file) = await UploadedAsync();

        await files.OnVerdictAsync(file.Content.Id, 1, ScanVerdict.Failed("scanner unreachable"), Token);

        var version = world.Store.Contents[file.Content.Id].Content.Versions[0];
        Assert.Equal(MediaVersionState.Failed, version.State);
        Assert.True(world.Storage.Blobs.ContainsKey((MediaStorageArea.Quarantine, version.BlobKey)));
        Assert.Null(await files.OpenReadAsync(file.Placement.Id, 1, Token));

        // A later verdict still decides.
        await files.OnVerdictAsync(file.Content.Id, 1, ScanVerdict.Clean, Token);
        Assert.Equal(MediaVersionState.Clean, world.Store.Contents[file.Content.Id].Content.Versions[0].State);
    }

    [Fact]
    public async Task ReplacingKeepsThePublishedVersionUntilTheNewOneIsPublished()
    {
        var (world, files, file) = await UploadedAsync();
        await files.OnVerdictAsync(file.Content.Id, 1, ScanVerdict.Clean, Token);

        var replaced = await files.ReplaceAsync(file.Placement.Id, new MemoryStream("second"u8.ToArray()), "text/plain", Token);
        Assert.Equal(2, replaced.Content.Versions.Count);
        Assert.Equal(1, replaced.Content.PublishedVersion);

        await files.OnVerdictAsync(file.Content.Id, 2, ScanVerdict.Clean, Token);
        Assert.Equal(1, world.Store.Contents[file.Content.Id].Content.PublishedVersion);

        await files.PublishVersionAsync(file.Placement.Id, 2, Token);
        await using var bytes = await files.OpenReadAsync(file.Placement.Id, cancellationToken: Token);
        Assert.Equal("second", await new StreamReader(bytes!).ReadToEndAsync(Token));
    }

    [Fact]
    public async Task WithoutAQuarantineAnUploadIsCommittedAtOnce()
    {
        var world = new FilesWorld();
        var drive = await world.DriveAsync();

        var file = await world.Service(Root).UploadAsync(Upload(drive.Placement.Id, "f.txt"), Token);

        Assert.Equal(MediaVersionState.Clean, file.Content.Versions[0].State);
        Assert.Equal(1, file.Content.PublishedVersion);
    }
}
