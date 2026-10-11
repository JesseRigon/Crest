#nullable enable
using System.Text.Json.Nodes;
using Crest.Media.Fields;
using Crest.Media.Library;
using Crest.Tests.Media.Files;
using static Crest.Tests.Media.Migration.LibraryWorld;

namespace Crest.Tests.Media.Migration;

/// <summary>Media fields with ids and with paths (ruling F1): ids are preferred, paths still read.</summary>
public class MediaFieldReadingTests
{
    [Fact]
    public void AFieldWithoutIdsReadsItsPaths()
    {
        var field = new MediaField { Paths = ["a.png", "docs/b.pdf"] };

        Assert.Equal([MediaReference.ForPath("a.png"), MediaReference.ForPath("docs/b.pdf")], field.GetReferences());
        Assert.False(field.IsMigrated());
    }

    [Fact]
    public void IdsAndPathsAreReadSideBySide()
    {
        var field = new MediaField { Paths = ["a.png", "b.png", ""], PlacementIds = ["id-a", "", "id-c"] };

        Assert.Equal(
            [new MediaReference("id-a", "a.png"), new MediaReference(null, "b.png"), new MediaReference("id-c", null)],
            field.GetReferences());
        Assert.False(field.IsMigrated());
    }

    [Fact]
    public void WritingKeepsEveryPosition()
    {
        var field = new MediaField();
        field.SetReferences([new MediaReference("id-a", "a.png"), new MediaReference(null, "b.png"), new MediaReference("id-c", null)]);

        Assert.Equal(["id-a", "", "id-c"], field.PlacementIds);
        Assert.Equal(["a.png", "b.png", ""], field.Paths);
    }

    [Fact]
    public void AFieldIsReadFromItsStoredJson()
    {
        var json = new JsonObject
        {
            ["Paths"] = new JsonArray("a.png", "b.png"),
            ["PlacementIds"] = new JsonArray("id-a", ""),
            ["MediaTexts"] = new JsonArray("Alt A", "Alt B"),
        };

        var field = MediaFieldReferences.FromJson(json);

        Assert.NotNull(field);
        Assert.Equal(new MediaReference("id-a", "a.png"), field.FirstReference());
        Assert.Equal(["Alt A", "Alt B"], field.MediaTexts);
        Assert.Null(MediaFieldReferences.FromJson(new JsonObject { ["Text"] = "not a media field" }));
    }

    [Fact]
    public async Task TheIdWinsOverAStalePath()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var file = await world.FileAsync(drive.Placement.Id, "renamed.png");
        await world.FileAsync(drive.Placement.Id, "old.png");
        world.SharePublic(drive.Placement.Id);

        // The path was the file's name before a rename; another file took the old name since.
        var field = new MediaField { Paths = ["old.png"], PlacementIds = [file.Placement.Id] };
        var entries = await world.Resolver(FilesWorld.User("alice")).ResolveAsync(field, cancellationToken: Token);

        var entry = Assert.Single(entries);
        Assert.Equal(file.Placement.Id, entry.Resolved?.Item.Placement.Id);
        Assert.Equal("renamed.png", entry.Name);
    }

    [Fact]
    public async Task APathIsReadWhenNoIdIsStored()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var docs = await world.FolderAsync(drive.Placement.Id, "docs");
        var file = await world.FileAsync(docs.Placement.Id, "a.png");
        world.SharePublic(drive.Placement.Id);

        var field = new MediaField { Paths = ["docs/a.png"] };
        var entry = Assert.Single(await world.Resolver(FilesWorld.User("alice")).ResolveAsync(field, cancellationToken: Token));

        Assert.Equal(file.Placement.Id, entry.Resolved?.Item.Placement.Id);
        Assert.Equal("docs/a.png", entry.Path);
        Assert.True(entry.IsImage);
    }

    [Fact]
    public async Task AnIdFromAnotherTenantFallsBackToThePath()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var file = await world.FileAsync(drive.Placement.Id, "a.png");
        world.SharePublic(drive.Placement.Id);

        var field = new MediaField { Paths = ["a.png"], PlacementIds = ["0123456789abcdefghjkmnpqrs"] };
        var entry = Assert.Single(await world.Resolver(FilesWorld.User("alice")).ResolveAsync(field, cancellationToken: Token));

        Assert.Equal(file.Placement.Id, entry.Resolved?.Item.Placement.Id);
    }

    [Fact]
    public async Task WhatTheViewerMayNotSeeDoesNotResolve()
    {
        var world = new LibraryWorld();
        var drive = await world.CreateLibraryAsync();
        var file = await world.FileAsync(drive.Placement.Id, "secret.png");

        // No grant for alice: the placement does not exist for her, by id or by path.
        var field = new MediaField { Paths = ["secret.png"], PlacementIds = [file.Placement.Id] };
        var entry = Assert.Single(await world.Resolver(FilesWorld.User("alice")).ResolveAsync(field, cancellationToken: Token));

        Assert.Null(entry.Resolved);
    }
}
