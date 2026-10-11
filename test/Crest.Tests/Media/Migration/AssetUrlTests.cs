#nullable enable
using System.Text.Json.Nodes;
using Crest.FileStorage.FileSystem;
using Crest.Media;
using Crest.Media.Files;
using Crest.Media.Filters;
using Crest.Media.Library;
using Crest.Sharing;
using Crest.Tests.Media.Files;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using static Crest.Tests.Media.Migration.LibraryWorld;

namespace Crest.Tests.Media.Migration;

/// <summary>
/// <c>asset_url</c> (ruling F1): ids, <c>/files</c> and <c>/media</c> URLs and library paths all
/// become <c>/files/{placementId}</c>, through the access check; private files are signed.
/// </summary>
public class AssetUrlTests
{
    private static async Task<string> AssetUrlAsync(MediaReferenceResolver resolver, FluidValue input) =>
        (await new AssetUrlFilter(resolver).ProcessAsync(input, new FilterArguments(), null!)).ToStringValue();

    private static async Task<(LibraryWorld World, MediaItem File)> PublicFileAsync()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var images = await world.FolderAsync(drive.Placement.Id, "images");
        var file = await world.FileAsync(images.Placement.Id, "logo.png");
        world.SharePublic(drive.Placement.Id);
        return (world, file);
    }

    [Fact]
    public async Task APathBecomesTheFilesUrlOfItsPlacement()
    {
        var (world, file) = await PublicFileAsync();
        var resolver = world.Resolver(FilesWorld.User("alice"));

        Assert.Equal($"/files/{file.Placement.Id}", await AssetUrlAsync(resolver, new StringValue("images/logo.png")));
        Assert.Equal($"/files/{file.Placement.Id}", await AssetUrlAsync(resolver, new StringValue("/media/images/logo.png")));
    }

    [Fact]
    public async Task AnIdOrAFilesUrlBecomesTheSameUrl()
    {
        var (world, file) = await PublicFileAsync();
        var resolver = world.Resolver(FilesWorld.User("alice"));

        Assert.Equal($"/files/{file.Placement.Id}", await AssetUrlAsync(resolver, new StringValue(file.Placement.Id)));
        Assert.Equal($"/files/{file.Placement.Id}", await AssetUrlAsync(resolver, new StringValue($"/files/{file.Placement.Id}")));
        Assert.Equal($"/files/{file.Placement.Id}/v/1", await AssetUrlAsync(resolver, new StringValue($"/files/{file.Placement.Id}/v/1")));
    }

    [Fact]
    public async Task TheQueryStringIsKeptForResizing()
    {
        var (world, file) = await PublicFileAsync();
        var resolver = world.Resolver(FilesWorld.User("alice"));

        Assert.Equal($"/files/{file.Placement.Id}?width=100", await AssetUrlAsync(resolver, new StringValue("images/logo.png?width=100")));
    }

    [Fact]
    public async Task AMediaFieldGivesItsFirstItemIdFirst()
    {
        var (world, file) = await PublicFileAsync();
        var resolver = world.Resolver(FilesWorld.User("alice"));
        var field = new JsonObject
        {
            ["Paths"] = new JsonArray("images/old-name.png"),
            ["PlacementIds"] = new JsonArray(file.Placement.Id),
        };

        Assert.Equal($"/files/{file.Placement.Id}", await AssetUrlAsync(resolver, new ObjectValue(field)));
    }

    [Fact]
    public async Task APrivateFileGetsASignedUrlAfterTheDecision()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var file = await world.FileAsync(drive.Placement.Id, "secret.png");
        world.Files.Share(file.Placement.Id, ShareEntry.Grant(SharePrincipal.User("alice"), AccessLevel.Viewer));

        var url = await AssetUrlAsync(world.Resolver(FilesWorld.User("alice")), new StringValue("secret.png"));

        var (path, query) = Split(url);
        Assert.Equal($"/files/{file.Placement.Id}", path);
        Assert.True(Signer.Verify(path, query[HmacMediaUrlSigner.ExpiresParameter], query[HmacMediaUrlSigner.SignatureParameter], new DateTimeOffset(world.Files.Sharing.Clock.UtcNow)));

        // Someone the file is not shared with gets no URL for it, signed or not.
        var other = await AssetUrlAsync(world.Resolver(FilesWorld.User("bob")), new StringValue("secret.png"));
        Assert.DoesNotContain(file.Placement.Id, other);
        Assert.DoesNotContain(HmacMediaUrlSigner.SignatureParameter + "=", other);
    }

    [Fact]
    public async Task AnUnresolvedPathKeepsItsMediaForm()
    {
        var (world, _) = await PublicFileAsync();
        var root = Path.Combine(Path.GetTempPath(), "media-url-" + Guid.NewGuid().ToString("n"));
        var store = new DefaultMediaFileStore(
            new LibraryFileStore(new FileSystemStore(root, NullLogger<FileSystemStore>.Instance)),
            "/media",
            null,
            [],
            [],
            null,
            NullLogger<DefaultMediaFileStore>.Instance);

        var resolver = world.Resolver(FilesWorld.User("alice"), store);

        Assert.Equal("/media/images/missing.png", await AssetUrlAsync(resolver, new StringValue("images/missing.png")));
        Assert.Equal("https://cdn.example.com/x.png", await AssetUrlAsync(resolver, new StringValue("https://cdn.example.com/x.png")));
    }

    private static (string Path, Dictionary<string, global::Microsoft.Extensions.Primitives.StringValues> Query) Split(string url)
    {
        var index = url.IndexOf('?');
        return index < 0 ? (url, []) : (url[..index], QueryHelpers.ParseQuery(url[index..]));
    }
}
