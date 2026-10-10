#nullable enable
using Crest.ContentManagement.Records;
using Crest.Queries;
using Crest.Queries.Structured;
using YesSql.Indexes;

namespace Crest.Tests.Queries;

/// <summary>An index a test joins to: a part index with a path, keyed by document id.</summary>
public class PathPartIndex : MapIndex
{
    public long DocumentId { get; set; }

    public string? Path { get; set; }
}

public sealed class TestIndexCatalog : IIndexTableCatalog
{
    public IReadOnlyList<IndexTableDescriptor> Tables { get; } =
    [
        new(nameof(ContentItemIndex), typeof(ContentItemIndex), IndexTableCatalog.ColumnsOf(typeof(ContentItemIndex))),
        new(nameof(PathPartIndex), typeof(PathPartIndex), IndexTableCatalog.ColumnsOf(typeof(PathPartIndex))),
    ];

    public IndexTableDescriptor? Find(string name) =>
        Tables.FirstOrDefault(table => string.Equals(table.Name, name, StringComparison.OrdinalIgnoreCase));
}
