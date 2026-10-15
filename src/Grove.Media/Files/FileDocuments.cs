#nullable enable
using YesSql.Indexes;

namespace Grove.Media.Files;

/// <summary>A content record as stored: the real object, whatever its placements.</summary>
public sealed class MediaContentDocument
{
    public long Id { get; set; }

    public MediaContent Content { get; set; } = null!;

    /// <summary>Versions to publish as soon as their verdict is clean (an overwrite through the
    /// media library's file store replaces what is served).</summary>
    public List<int> PublishWhenClean { get; set; } = [];
}

/// <summary>
/// A placement as stored, with its baked ancestor closure (ruling F4): every placement above it
/// through any path, recalculated only when a placement is created, moved or removed.
/// </summary>
public sealed class MediaPlacementDocument
{
    public long Id { get; set; }

    public MediaPlacement Placement { get; set; } = null!;

    /// <summary>The kind of the content it places (a soft link: its target's), for listings.</summary>
    public MediaObjectKind Kind { get; set; }

    public List<MediaAncestor> Ancestors { get; set; } = [];
}

/// <summary>One ancestor of a placement: a placement of a folder (or the drive) above it, its
/// content and its shortest distance (1 is the folder it is in).</summary>
public sealed record MediaAncestor(string PlacementId, string ContentId, int Depth);

public sealed class MediaContentIndex : MapIndex
{
    public string ContentId { get; set; } = string.Empty;

    public int Kind { get; set; }

    public int? DriveType { get; set; }

    public int OwnerKind { get; set; }

    public string? OwnerId { get; set; }

    public bool IsInactive { get; set; }
}

public sealed class MediaPlacementIndex : MapIndex
{
    public string PlacementId { get; set; } = string.Empty;

    public string ContentId { get; set; } = string.Empty;

    public string? ParentId { get; set; }

    public string DriveId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? LinkTargetPlacementId { get; set; }

    public bool IsSoftLink { get; set; }

    /// <summary>The placement whose access decides this row: a soft link's target, else itself.
    /// The sharing scope filters on it.</summary>
    public string AccessId { get; set; } = string.Empty;

    public int Kind { get; set; }
}

/// <summary>The closure as rows: (placement, ancestor, depth), so the loop gate and "everything
/// below" are single queries.</summary>
public sealed class MediaAncestorIndex : MapIndex
{
    public string PlacementId { get; set; } = string.Empty;

    public string AncestorPlacementId { get; set; } = string.Empty;

    public string AncestorContentId { get; set; } = string.Empty;

    public int Depth { get; set; }
}

public sealed class MediaContentIndexProvider : IndexProvider<MediaContentDocument>
{
    public override void Describe(DescribeContext<MediaContentDocument> context) =>
        context.For<MediaContentIndex>()
            .Map(document => new MediaContentIndex
            {
                ContentId = document.Content.Id,
                Kind = (int)document.Content.Kind,
                DriveType = document.Content.DriveType is { } type ? (int)type : null,
                OwnerKind = (int)document.Content.Owner.Kind,
                OwnerId = document.Content.Owner.Id,
                IsInactive = document.Content.IsInactive,
            });
}

public sealed class MediaPlacementIndexProvider : IndexProvider<MediaPlacementDocument>
{
    public override void Describe(DescribeContext<MediaPlacementDocument> context)
    {
        context.For<MediaPlacementIndex>()
            .Map(document => new MediaPlacementIndex
            {
                PlacementId = document.Placement.Id,
                ContentId = document.Placement.ContentId,
                ParentId = document.Placement.ParentId,
                DriveId = document.Placement.DriveId,
                Name = document.Placement.Name,
                LinkTargetPlacementId = document.Placement.LinkTargetPlacementId,
                IsSoftLink = document.Placement.IsSoftLink,
                AccessId = document.Placement.LinkTargetPlacementId ?? document.Placement.Id,
                Kind = (int)document.Kind,
            });

        context.For<MediaAncestorIndex>()
            .Map(document => document.Ancestors.Select(ancestor => new MediaAncestorIndex
            {
                PlacementId = document.Placement.Id,
                AncestorPlacementId = ancestor.PlacementId,
                AncestorContentId = ancestor.ContentId,
                Depth = ancestor.Depth,
            }));
    }
}
