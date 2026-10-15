using YesSql.Indexes;

namespace Grove.Sharing.Indexes;

/// <summary>One row per share document: the object it is about.</summary>
public sealed class ShareDocumentIndex : MapIndex
{
    public string Kind { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;
}

/// <summary>One row per entry: who an object is shared with, for "shared with me".</summary>
public sealed class ShareEntryIndex : MapIndex
{
    public string Kind { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    public int PrincipalKind { get; set; }

    public string PrincipalId { get; set; } = string.Empty;

    public bool IsBlock { get; set; }
}

public sealed class ShareIndexProvider : IndexProvider<ShareDocument>
{
    public override void Describe(DescribeContext<ShareDocument> context)
    {
        context.For<ShareDocumentIndex>()
            .Map(document => new ShareDocumentIndex { Kind = document.Kind, ObjectId = document.ObjectId });

        context.For<ShareEntryIndex>()
            .Map(document => document.Entries.Select(entry => new ShareEntryIndex
            {
                Kind = document.Kind,
                ObjectId = document.ObjectId,
                PrincipalKind = (int)entry.Principal.Kind,
                PrincipalId = entry.Principal.Id ?? string.Empty,
                IsBlock = entry.IsBlock,
            }));
    }
}
