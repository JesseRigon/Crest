namespace Crest.Sharing;

/// <summary>The entries and rules set on one object (or on the tenant). Only objects with sharing of their own have one.</summary>
public sealed class ShareDocument
{
    public string Kind { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    public List<ShareEntry> Entries { get; set; } = [];

    public List<ShareRule> Rules { get; set; } = [];

    public ShareableRef Ref => new(Kind, ObjectId);
}
