#nullable enable
namespace Crest.Queries.Structured;

/// <summary>An index table the store holds, with the columns its index type declares.</summary>
public sealed record IndexTableDescriptor(string Name, Type IndexType, IReadOnlyList<QueryColumn> Columns)
{
    public QueryColumn? FindColumn(string name) =>
        Columns.FirstOrDefault(column => string.Equals(column.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>The index tables a structured query may read: every registered index type, by table name.</summary>
public interface IIndexTableCatalog
{
    IReadOnlyList<IndexTableDescriptor> Tables { get; }

    IndexTableDescriptor? Find(string name);
}
