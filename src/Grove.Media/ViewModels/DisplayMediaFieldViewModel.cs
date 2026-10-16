using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;
using Grove.Media.Fields;

namespace Grove.Media.ViewModels;

public class DisplayMediaFieldViewModel
{
    /// <summary>The stored paths, for templates written before placement ids. Prefer <see cref="Items"/>.</summary>
    public string[] Paths => Field.Paths;

    /// <summary>The field's items the viewer may see, resolved through the access check, in order.</summary>
    public IReadOnlyList<MediaFieldDisplayItem> Items { get; set; } = [];

    public MediaField Field { get; set; }
    public ContentFieldSet FieldSet { get; set; }
    public ContentFieldDefinition FieldDefinition { get; set; }
}

/// <summary>One item of a media field, as a template shows it (ruling F1).</summary>
public sealed class MediaFieldDisplayItem
{
    public string PlacementId { get; init; }

    /// <summary>The media library path, when the item was stored by path.</summary>
    public string Path { get; init; }

    /// <summary>The <c>/files/{placementId}</c> URL, signed when the file is private.</summary>
    public string Url { get; init; }

    public string Name { get; init; }

    public bool IsImage { get; init; }

    public bool IsPublic { get; init; }

    public string MediaText { get; init; }

    public Anchor Anchor { get; init; }
}
