#nullable enable
using System.Collections.Concurrent;
using System.Reflection;
using Crest.Data;
using Crest.Queries.Structured;
using YesSql;
using YesSql.Indexes;

namespace Crest.Queries.Structured;

/// <summary>
/// The index tables the store holds: every registered index provider's index types, named by
/// the store's table convention, with the columns the index type's properties declare.
/// </summary>
public sealed class IndexTableCatalog : IIndexTableCatalog
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<QueryColumn>> s_columns = new();

    private readonly Lazy<IReadOnlyList<IndexTableDescriptor>> _tables;

    public IndexTableCatalog(IStore store, IEnumerable<IIndexProvider> indexProviders, IEnumerable<IScopedIndexProvider> scopedIndexProviders)
    {
        _tables = new Lazy<IReadOnlyList<IndexTableDescriptor>>(() => Discover(store, indexProviders.Concat(scopedIndexProviders)));
    }

    public IReadOnlyList<IndexTableDescriptor> Tables => _tables.Value;

    public IndexTableDescriptor? Find(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : Tables.FirstOrDefault(table => string.Equals(table.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<IndexTableDescriptor> Discover(IStore store, IEnumerable<IIndexProvider> providers)
    {
        var convention = store.Configuration.TableNameConvention;
        var tables = new Dictionary<string, IndexTableDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            var documentType = provider.ForType();
            var context = (IDescriptor)Activator.CreateInstance(typeof(DescribeContext<>).MakeGenericType(documentType))!;
            provider.Describe(context);

            foreach (var descriptor in context.Describe(documentType))
            {
                var name = convention.GetIndexTable(descriptor.IndexType, provider.CollectionName);
                tables.TryAdd(name, new IndexTableDescriptor(name, descriptor.IndexType, ColumnsOf(descriptor.IndexType)));
            }
        }

        return tables.Values.OrderBy(table => table.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IReadOnlyList<QueryColumn> ColumnsOf(Type indexType) =>
        s_columns.GetOrAdd(indexType, static type => type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0 && IsColumnType(property.PropertyType))
            .Select(property => new QueryColumn(property.Name, property.PropertyType))
            .ToArray());

    private static bool IsColumnType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeSpan)
            || type == typeof(Guid) || type == typeof(byte[]);
    }
}
