using Grove.Media.Sharing;
using Grove.Access.Sharing;
using Grove.Access.Sharing.Services;
using static Grove.Tests.Sharing.SharingWorld;

namespace Grove.Tests.Sharing;

/// <summary>Media's shareable kind: paths form the tree, the library root administers all
/// media, and the media permissions map to access levels (docs/sharing.md, docs/media.md).</summary>
public class MediaShareableKindTests
{
    private static (SharingResolver Resolver, SharingWorld World) Build()
    {
        var world = new SharingWorld();
        var kinds = new ShareableKinds([new MediaShareableKind()]);
        return (new SharingResolver(kinds, world.Store, world.Clock), world);
    }

    private static ShareableRef Media(string path) => new(MediaShareableKind.KindName, path);

    private static void Share(SharingWorld world, string path, params ShareEntry[] entries) =>
        world.Store.Documents[Media(path)] = new ShareDocument { Kind = MediaShareableKind.KindName, ObjectId = path, Entries = [.. entries] };

    [Fact]
    public async Task APathInheritsFromItsFoldersUpToTheLibraryRoot()
    {
        var kind = new MediaShareableKind();

        var file = await kind.GetAsync("/products/chairs/chair.jpg", TestContext.Current.CancellationToken);
        var root = await kind.GetAsync("", TestContext.Current.CancellationToken);

        Assert.Equal(Media("products/chairs"), file!.Parent);
        Assert.Equal(["jpg"], file.Facts["extension"]);
        Assert.True(root!.IsRoot);
        Assert.Equal(Media(""), (await kind.GetAsync("products", TestContext.Current.CancellationToken))!.Parent);
    }

    [Fact]
    public async Task AGrantOnAFolderReachesEveryFileBelowIt()
    {
        var (resolver, world) = Build();
        Share(world, "products", ShareEntry.Grant(SharePrincipal.Role("Customer"), AccessLevel.Viewer));

        Assert.Equal(AccessLevel.Viewer, (await resolver.ResolveAsync(User("c", "Customer"), Media("products/chairs/chair.jpg"), TestContext.Current.CancellationToken)).Level);
        Assert.Equal(AccessLevel.None, (await resolver.ResolveAsync(User("c", "Customer"), Media("private/x.pdf"), TestContext.Current.CancellationToken)).Level);
    }

    [Fact]
    public async Task TheLibraryRootIsWhereMediaAdminsAreGranted()
    {
        var (resolver, world) = Build();
        Share(world, "", ShareEntry.Grant(SharePrincipal.Role("MediaAdmin"), AccessLevel.Admin));

        Assert.Equal(AccessLevel.Admin, (await resolver.ResolveAsync(User("m", "MediaAdmin"), Media("any/where/file.png"), TestContext.Current.CancellationToken)).Level);
    }

    [Fact]
    public async Task ARuleByExtensionMakesFilesPublic()
    {
        var (resolver, world) = Build();
        world.Store.Documents[Media("press")] = new ShareDocument
        {
            Kind = MediaShareableKind.KindName,
            ObjectId = "press",
            Rules = [new ShareRule("pdf-public", "PDFs are public", [new ShareCondition("extension", ShareConditionOperator.Equals, ["pdf"])], [ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Viewer)])],
        };

        Assert.True((await resolver.ResolveAsync(Visitor, Media("press/kit.pdf"), TestContext.Current.CancellationToken)).IsPublic);
        Assert.False((await resolver.ResolveAsync(Visitor, Media("press/kit.docx"), TestContext.Current.CancellationToken)).IsPublic);
    }

    [Fact]
    public void TheMediaPermissionsMapToLevelsAndNothingElseIsMedias()
    {
        var kind = new MediaShareableKind();

        Assert.Equal(AccessLevel.Viewer, kind.LevelFor(global::Grove.Media.MediaPermissions.ViewMedia.Name));
        Assert.Equal(AccessLevel.Manager, kind.LevelFor(global::Grove.Media.MediaPermissions.ManageMediaFolder.Name));
        Assert.Null(kind.LevelFor("ManageSettings"));
    }

    [Fact]
    public async Task ThroughTheMapperAStringThatIsNotAMediaQuestionIsLeftAlone()
    {
        // A settings group is a string too: the media kind recognises it, but ManageSettings is
        // not a media permission, so sharing does not answer and the settings mapper decides.
        var (resolver, _) = Build();
        var kinds = new ShareableKinds([new MediaShareableKind()]);
        var mapper = new SharingPermissionMapper([new MediaShareableKind()], kinds, resolver);

        Assert.Null(await mapper.MapAsync("ManageSettings", "general", User("u"), TestContext.Current.CancellationToken));
        Assert.Empty(await mapper.MapAsync(global::Grove.Media.MediaPermissions.ViewMedia.Name, "private/x.pdf", User("u"), TestContext.Current.CancellationToken));
    }
}
