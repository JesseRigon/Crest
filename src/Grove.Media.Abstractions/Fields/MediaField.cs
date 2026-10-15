using Grove.ContentManagement;

namespace Grove.Media.Fields;

/// <summary>
/// Media chosen for a content item (docs/media.md, ruling F1: ids are stored, paths are
/// placements). <see cref="PlacementIds"/> is what the field refers to; <see cref="Paths"/> is
/// kept beside it, position for position, as the media library path of each placement when it
/// has one, so content that was never migrated, templates that read paths and exports to
/// another tenant keep working. Read both through <see cref="MediaFieldReferences.GetReferences"/>,
/// which prefers the id.
/// </summary>
public class MediaField : ContentField
{
    public string[] Paths { get; set; }

    /// <summary>The placement of each item, aligned with <see cref="Paths"/>; an empty entry is an
    /// item known only by its path. Null on content written before ids were stored.</summary>
    public string[] PlacementIds { get; set; }

    public string[] MediaTexts { get; set; }
}
