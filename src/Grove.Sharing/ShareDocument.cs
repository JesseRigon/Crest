namespace Grove.Sharing;

/// <summary>The entries, rules and comments set on one object (or on the tenant). Only objects with sharing or comments of their own have one.</summary>
public sealed class ShareDocument
{
    public string Kind { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    public List<ShareEntry> Entries { get; set; } = [];

    public List<ShareRule> Rules { get; set; } = [];

    /// <summary>Comments on the object, when no comment store keeps them elsewhere
    /// (requirement 72: the object's metadata by default).</summary>
    public List<ShareComment> Comments { get; set; } = [];

    public ShareableRef Ref => new(Kind, ObjectId);
}
