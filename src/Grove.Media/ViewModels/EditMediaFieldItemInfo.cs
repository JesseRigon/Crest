using Grove.Media.Fields;

namespace Grove.Media.ViewModels;

/// <summary>A media field item as an attached media field posts it: its path and placement, and whether it was just uploaded or removed.</summary>
public class EditMediaFieldItemInfo
{
    public string Path { get; set; }

    /// <summary>The placement the item refers to (ruling F1); kept while <see cref="Path"/> is unchanged.</summary>
    public string PlacementId { get; set; }

    public string AttachedFileName { get; set; }

    // It will be true if the media item is a new upload from a attached media field.
    public bool IsNew { get; set; }

    // It will be true if the media item has been marked for deletion using a attached media field.
    public bool IsRemoved { get; set; }

    // Alt text is an option that maybe applied to a media field through settings.
    public string MediaText { get; set; } = string.Empty;
    public Anchor Anchor { get; set; } = new Anchor();
}
