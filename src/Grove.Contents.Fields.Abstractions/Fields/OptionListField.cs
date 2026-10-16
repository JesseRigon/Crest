using Grove.Contents.Management;

namespace Grove.Fields;

/// <summary>
/// References one or more options drawn from an option SOURCE - a tenant-editable
/// Option list, or any other source a provider exposes (users, content items...).
/// </summary>
/// <remarks>
/// Stored data stays deliberately tiny: the source and the selected ids. What the
/// dropdown DISPLAYS - which columns of the referenced record, in what order - lives
/// in the field's settings for that attachment, because the same source is displayed
/// differently in different places ("sales rep shows name and badge number" vs
/// "approver shows name and department").
/// </remarks>
public class OptionListField : ContentField
{
    /// <summary>The source these options are drawn from (see
    /// <see cref="Grove.Models.OptionSourceKeys"/>). Mirrors the field settings,
    /// and is stored so a reader can resolve values without the type definition.</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>The selected option ids. For an option list source these are Option
    /// content item ids; code compares the option's Key, not this id.</summary>
    public string[] SelectedIds { get; set; } = [];

    /// <summary>
    /// In a frozen item of a snapshot embedding (docs/content-engine.md › Live and snapshot
    /// embeddings): by selected id, the referenced values the embedding names, as they were when
    /// the parent was published. Null everywhere else.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Copied { get; set; }
}
