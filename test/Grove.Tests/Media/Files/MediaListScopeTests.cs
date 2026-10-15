#nullable enable
using Grove.Access;
using Grove.Media.Files;
using Grove.Sharing;
using Grove.Sharing.Indexes;
using Grove.Sharing.Migrations;
using Grove.Sharing.Services;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using static Grove.Tests.Media.Files.FilesWorld;

namespace Grove.Tests.Media.Files;

/// <summary>
/// File lists filter in the query through sharing's scope over the placement index
/// (requirement 77) and agree with the decision on each object (81): on a SQLite store with the
/// access index kept current by the file-object service, each caller's listing (paged in the
/// query) is exactly the children the resolver lets them view, soft links and hard links
/// included, before and after a move.
/// </summary>
public class MediaListScopeTests : IAsyncLifetime
{
    private IStore _store = null!;
    private string _file = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _store = await StoreFactory.CreateAndInitializeAsync(new Configuration().UseSqLite($"Data Source={_file};Cache=Shared"));

        await using (var session = _store.CreateSession())
        {
            var builder = new SchemaBuilder(_store.Configuration, await session.BeginTransactionAsync());
            await new SharingMigrations { SchemaBuilder = builder }.CreateAsync();
            await new FileMigrations { SchemaBuilder = builder }.CreateAsync();
            await session.SaveChangesAsync();
        }

        _store.RegisterIndexes<ShareAccessIndexProvider>();
        _store.RegisterIndexes<ShareRecalculationIndexProvider>();
        _store.RegisterIndexes<MediaContentIndexProvider>();
        _store.RegisterIndexes<MediaPlacementIndexProvider>();
    }

    public ValueTask DisposeAsync()
    {
        _store?.Dispose();
        try
        {
            File.Delete(_file);
        }
        catch (IOException)
        {
        }

        return ValueTask.CompletedTask;
    }

    private static readonly CallerContext[] s_callers =
    [
        User("cora", "Customer"),
        User("sam", "Staff"),
        User("mia", "Marketing"),
        User("dana"),
        User("cm", "Customer", "Marketing"),
        User("eve"),
    ];

    // The access index's store over the test's session: each step commits through it, as a
    // request or the job ends its unit of work, so nothing it read outlives the commit.
    private YesSqlShareAccessStore _accessStore = null!;

    private Task CommitAsync() => _accessStore.CommitAsync();

    private FilesWorld World(YesSql.ISession session)
    {
        var world = new FilesWorld { StoreOverride = new YesSqlMediaObjectStore(session) };
        var kinds = new ShareableKinds([world.Kind]);
        var resolver = new SharingResolver(kinds, world.Sharing.Store, world.Sharing.Clock);
        _accessStore = new YesSqlShareAccessStore(session);
        world.AccessIndex = new ShareAccessIndexer(kinds, resolver, _accessStore, world.Sharing.Clock);
        world.Scopes = new SharingScopes(kinds, world.Sharing.Clock);
        return world;
    }

    /// <summary>The folder's children the resolver lets the caller view, by placement id.</summary>
    private static async Task<HashSet<string>> AllowedAsync(FilesWorld world, CallerContext caller, IEnumerable<MediaPlacementDocument> children)
    {
        var resolver = world.Resolver();
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in children)
        {
            world.Kind.TryIdentify(child.Placement, out var id);
            if ((await resolver.ResolveAsync(caller, new ShareableRef(FileShareableKind.Name, id), Token)).Allows(AccessLevel.Viewer))
            {
                allowed.Add(child.Placement.Id);
            }
        }

        return allowed;
    }

    private static async Task AssertListsAgreeAsync(FilesWorld world, YesSql.ISession session, MediaItem folder)
    {
        var store = new YesSqlMediaObjectStore(session);
        var holders = (await store.GetPlacementsOfContentAsync(folder.Content.Id)).Select(holder => holder.Placement.Id).ToList();
        var children = await store.GetChildrenAsync(holders);

        foreach (var caller in s_callers)
        {
            var files = world.Service(caller);
            if (await files.GetAsync(folder.Placement.Id, Token) is null)
            {
                continue;
            }

            var expected = await AllowedAsync(world, caller, children);

            // Two items a page: the pages together are the list, and the total is its size.
            var listed = new List<string>();
            for (var skip = 0; ; skip += 2)
            {
                var page = await files.ListChildrenPageAsync(folder.Placement.Id, skip, 2, Token);
                Assert.Equal(expected.Count, page.Total);
                listed.AddRange(page.Items.Select(item => item.Placement.Id));
                if (page.Items.Count < 2)
                {
                    break;
                }
            }

            Assert.True(expected.SetEquals(listed), $"The list differs from the resolver for {caller.UserId}.");
            Assert.Equal(listed.Count, listed.Distinct().Count());
        }
    }

    [Fact]
    public async Task EachCallersListIsExactlyWhatTheResolverAllows()
    {
        await using var session = _store.CreateSession();
        var world = World(session);
        var root = world.Service(Root);

        // The example drive of docs/sharing.md, as file objects.
        var drive = await world.DriveAsync("Product Info");
        var spec = await root.CreateFolderAsync(drive.Placement.Id, "Spec sheets", Token);
        var images = await root.CreateFolderAsync(drive.Placement.Id, "Images", Token);
        await root.UploadAsync(Upload(images.Placement.Id, "chair-original.jpg", type: "image/jpeg"), Token);
        var psd = await root.UploadAsync(Upload(images.Placement.Id, "chair.psd", type: "image/vnd.adobe.photoshop"), Token);
        var final = await root.UploadAsync(Upload(images.Placement.Id, "chair-final.jpg", type: "image/jpeg"), Token);
        await root.UpdateMetadataAsync(final.Placement.Id, new MediaMetadata(Tags: ["final"]), Token);
        var specFile = await root.UploadAsync(Upload(spec.Placement.Id, "spec.pdf", type: "application/pdf"), Token);
        await root.ShortcutAsync(specFile.Placement.Id, images.Placement.Id, "spec link", Token);
        await root.LinkAsync(psd.Placement.Id, spec.Placement.Id, "chair copy.psd", Token);

        world.Share(drive.Placement.Id, ShareEntry.Grant(SharePrincipal.Role("Customer"), AccessLevel.Viewer), ShareEntry.Grant(SharePrincipal.Role("Staff"), AccessLevel.Viewer), ShareEntry.Block(SharePrincipal.User("sam")));
        world.Share(spec.Placement.Id, ShareEntry.Grant(SharePrincipal.Tenant, AccessLevel.Viewer));
        world.Share(images.Placement.Id, ShareEntry.Grant(SharePrincipal.Role("Marketing"), AccessLevel.Editor));
        world.Share(psd.Placement.Id, ShareEntry.Block(SharePrincipal.Role("Customer")), ShareEntry.Grant(SharePrincipal.User("dana"), AccessLevel.Editor));
        world.Share(final.Placement.Id, ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Viewer));

        // Entries are sharing's to index; here they were written straight to its store.
        await world.AccessIndex!.RecalculateAsync(new ShareableRef(FileShareableKind.Name, drive.Placement.Id), includeDescendants: true, Token);
        await CommitAsync();

        await AssertListsAgreeAsync(world, session, images);
        await AssertListsAgreeAsync(world, session, spec);
        await AssertListsAgreeAsync(world, session, drive);

        // A customer never sees chair.psd, in any placement.
        var cora = world.Service(User("cora", "Customer"));
        Assert.DoesNotContain(psd.Placement.Id, (await cora.ListChildrenAsync(images.Placement.Id, Token)).Select(item => item.Placement.Id));

        // A move recalculates what moved: the spec sheet under Images now inherits Marketing.
        await root.MoveAsync(specFile.Placement.Id, images.Placement.Id, Token);
        await CommitAsync();
        await AssertListsAgreeAsync(world, session, images);
        await AssertListsAgreeAsync(world, session, spec);

        // A deleted object leaves every list.
        await root.DeleteAsync(final.Placement.Id, Token);
        await CommitAsync();
        Assert.DoesNotContain(final.Placement.Id, (await world.Service(User("mia", "Marketing")).ListChildrenAsync(images.Placement.Id, Token)).Select(item => item.Placement.Id));
        await AssertListsAgreeAsync(world, session, images);
    }

    [Fact]
    public async Task ADriveListIsWhatTheCallerMayView()
    {
        await using var session = _store.CreateSession();
        var world = World(session);
        var open = await world.DriveAsync("Open");
        await world.DriveAsync("Closed");
        world.Share(open.Placement.Id, ShareEntry.Grant(SharePrincipal.Tenant, AccessLevel.Viewer));
        await world.AccessIndex!.RecalculateAsync(new ShareableRef(FileShareableKind.Name, open.Placement.Id), includeDescendants: true, Token);
        await CommitAsync();

        Assert.Equal([open.Placement.Id], (await world.Service(User("eve")).ListDrivesAsync(Token)).Select(item => item.Placement.Id));
        Assert.Equal(2, (await world.Service(Root).ListDrivesAsync(Token)).Count);
    }
}
