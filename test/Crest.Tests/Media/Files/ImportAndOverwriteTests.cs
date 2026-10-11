#nullable enable
using Crest.Access;
using Crest.Media.Files;
using static Crest.Tests.Media.Files.FilesWorld;

namespace Crest.Tests.Media.Files;

/// <summary>The media library's writes: a trusted import is clean with its reason and never
/// scanned; an overwrite is published as soon as it is clean.</summary>
public class ImportAndOverwriteTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly CallerContext System = new() { Tenant = "Default", Side = CallerSide.System, IsSystem = true, IsSuperUser = true, UserName = "system" };

    [Fact]
    public async Task ATrustedImportIsCleanWithItsReasonAndNeverScanned()
    {
        var world = new FilesWorld();
        world.Quarantine = new RecordingQuarantine(world.Storage);
        var drive = await world.DriveAsync();

        var item = await world.Service(System).ImportTrustedAsync(Upload(drive.Placement.Id, "old.png"), "imported, pre-existing", Token);

        var version = Assert.Single(item.Content.Versions);
        Assert.Equal(MediaVersionState.Clean, version.State);
        Assert.Equal("imported, pre-existing", version.Reason);
        Assert.Equal(1, item.Content.PublishedVersion);
        Assert.Empty(world.Quarantine.Submitted);
        Assert.True(world.Storage.Blobs.ContainsKey((MediaStorageArea.Committed, version.BlobKey)));
    }

    [Fact]
    public async Task OnlyTheSystemImportsWithoutAScan()
    {
        var world = new FilesWorld();
        var drive = await world.DriveAsync();

        await Assert.ThrowsAsync<AccessDeniedException>(() => world.Service(Root).ImportTrustedAsync(Upload(drive.Placement.Id, "x.png"), "trust me", Token));
    }

    [Fact]
    public async Task AnOverwriteIsPublishedOnceClean()
    {
        var world = new FilesWorld();
        world.Quarantine = new RecordingQuarantine(world.Storage);
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var file = await files.UploadAsync(Upload(drive.Placement.Id, "f.txt", "first"), Token);
        await files.OnVerdictAsync(file.Content.Id, 1, ScanVerdict.Clean, Token);

        await files.ReplaceAsync(file.Placement.Id, new MemoryStream("second"u8.ToArray()), "text/plain", publishWhenClean: true, Token);
        Assert.Equal(1, world.Store.Contents[file.Content.Id].Content.PublishedVersion);

        await files.OnVerdictAsync(file.Content.Id, 2, ScanVerdict.Clean, Token);
        Assert.Equal(2, world.Store.Contents[file.Content.Id].Content.PublishedVersion);

        // A plain replace still waits to be published.
        await files.ReplaceAsync(file.Placement.Id, new MemoryStream("third"u8.ToArray()), "text/plain", Token);
        await files.OnVerdictAsync(file.Content.Id, 3, ScanVerdict.Clean, Token);
        Assert.Equal(2, world.Store.Contents[file.Content.Id].Content.PublishedVersion);
    }
}
