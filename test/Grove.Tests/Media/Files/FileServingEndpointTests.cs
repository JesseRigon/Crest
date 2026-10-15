#nullable enable
using Grove.Access;
using Grove.Media.Files;
using Grove.Access.Sharing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using static Grove.Tests.Media.Files.FilesWorld;

namespace Grove.Tests.Media.Files;

/// <summary>The byte endpoint (ruling F2): the decision first, clean versions only, public files
/// cacheable, private ones never, and a signed URL the only proof of an earlier decision.</summary>
public class FileServingEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly HmacMediaUrlSigner Signer = new([.. Enumerable.Range(0, 32).Select(index => (byte)index)]);

    private static HttpContext Request(FilesWorld world, CallerContext? caller, string path, string query = "")
    {
        var services = new ServiceCollection()
            .AddSingleton<IMediaObjectStore>(world.Store)
            .AddSingleton<IMediaStorageProvider>(world.Storage)
            .AddSingleton(Signer)
            .AddSingleton<ICallerContextAccessor>(new Accessor(caller))
            .AddSingleton<IMediaObjectService>(world.Service(caller))
            .AddSingleton(world.Resolver())
            .BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return context;
    }

    private static int? Status(IResult result) => (result as IStatusCodeHttpResult)?.StatusCode;

    private static async Task<(FilesWorld World, MediaItem File)> FileAsync(bool scanned)
    {
        var world = new FilesWorld();
        world.Quarantine = new RecordingQuarantine(world.Storage);
        var files = world.Service(Root);
        var drive = await world.DriveAsync();
        var file = await files.UploadAsync(Upload(drive.Placement.Id, "f.txt"), Token);
        if (scanned)
        {
            await files.OnVerdictAsync(file.Content.Id, 1, ScanVerdict.Clean, Token);
        }

        return (world, file);
    }

    [Fact]
    public async Task AnUncleanVersionIsNeverServed()
    {
        var (world, file) = await FileAsync(scanned: false);

        var context = Request(world, Root, FileServingEndpoint.PathFor(file.Placement.Id, 1));
        Assert.Equal(StatusCodes.Status404NotFound, Status(await FileServingEndpoint.ServeAsync(context, file.Placement.Id, 1)));

        // Not even with a valid signature.
        var signed = Signer.Sign(new Uri(FileServingEndpoint.PathFor(file.Placement.Id, 1), UriKind.Relative), DateTimeOffset.UtcNow.AddMinutes(5)).OriginalString;
        var query = signed[signed.IndexOf('?')..];
        context = Request(world, null, FileServingEndpoint.PathFor(file.Placement.Id, 1), query);
        Assert.Equal(StatusCodes.Status404NotFound, Status(await FileServingEndpoint.ServeAsync(context, file.Placement.Id, 1)));
    }

    [Fact]
    public async Task ACallerWhoMayNotViewGetsNotFound()
    {
        var (world, file) = await FileAsync(scanned: true);

        var context = Request(world, User("eve"), FileServingEndpoint.PathFor(file.Placement.Id, null));
        Assert.Equal(StatusCodes.Status404NotFound, Status(await FileServingEndpoint.ServeAsync(context, file.Placement.Id, null)));

        context = Request(world, null, FileServingEndpoint.PathFor(file.Placement.Id, null));
        Assert.Equal(StatusCodes.Status404NotFound, Status(await FileServingEndpoint.ServeAsync(context, file.Placement.Id, null)));
    }

    [Fact]
    public async Task ABadOrExpiredSignatureIsRefused()
    {
        var (world, file) = await FileAsync(scanned: true);
        var path = FileServingEndpoint.PathFor(file.Placement.Id, null);

        var forged = Request(world, User("eve"), path, "?exp=9999999999&sig=forged");
        Assert.Equal(StatusCodes.Status403Forbidden, Status(await FileServingEndpoint.ServeAsync(forged, file.Placement.Id, null)));

        var expired = Signer.Sign(new Uri(path, UriKind.Relative), DateTimeOffset.UtcNow.AddMinutes(-1)).OriginalString;
        var context = Request(world, User("eve"), path, expired[expired.IndexOf('?')..]);
        Assert.Equal(StatusCodes.Status403Forbidden, Status(await FileServingEndpoint.ServeAsync(context, file.Placement.Id, null)));

        // Signed for another placement.
        var other = Signer.Sign(new Uri(FileServingEndpoint.PathFor("other", null), UriKind.Relative), DateTimeOffset.UtcNow.AddMinutes(5)).OriginalString;
        context = Request(world, User("eve"), path, other[other.IndexOf('?')..]);
        Assert.Equal(StatusCodes.Status403Forbidden, Status(await FileServingEndpoint.ServeAsync(context, file.Placement.Id, null)));
    }

    [Fact]
    public async Task AValidSignatureServesPrivately()
    {
        var (world, file) = await FileAsync(scanned: true);
        var path = FileServingEndpoint.PathFor(file.Placement.Id, null);
        var signed = Signer.Sign(new Uri(path, UriKind.Relative), DateTimeOffset.UtcNow.AddMinutes(5)).OriginalString;

        var context = Request(world, null, path, signed[signed.IndexOf('?')..]);
        var result = await FileServingEndpoint.ServeAsync(context, file.Placement.Id, null);

        Assert.IsType<FileStreamHttpResult>(result);
        Assert.StartsWith("private", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task APrivateFileIsNeverPubliclyCacheable()
    {
        var (world, file) = await FileAsync(scanned: true);
        world.Share(file.Placement.Id, ShareEntry.Grant(SharePrincipal.User("ann"), AccessLevel.Viewer));

        var context = Request(world, User("ann"), FileServingEndpoint.PathFor(file.Placement.Id, null));
        var result = await FileServingEndpoint.ServeAsync(context, file.Placement.Id, null);

        Assert.IsType<FileStreamHttpResult>(result);
        Assert.Equal("private, no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task APublicFileIsPubliclyCacheable()
    {
        var (world, file) = await FileAsync(scanned: true);
        world.Share(file.Placement.Id, ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Viewer));

        var context = Request(world, CallerContext.Anonymous("Default", CallerSide.Site), FileServingEndpoint.PathFor(file.Placement.Id, 1));
        var result = await FileServingEndpoint.ServeAsync(context, file.Placement.Id, 1);

        Assert.IsType<FileStreamHttpResult>(result);
        Assert.StartsWith("public", context.Response.Headers.CacheControl.ToString());
    }
}
