#nullable enable
using System.Text.Json.Nodes;
using Crest.Queries.Structured;

namespace Crest.Queries;

/// <summary>
/// What a registry needs to know about a query without running it: its name, the parameters
/// it takes and the columns it returns.
/// </summary>
public sealed record QueryDescriptor(
    string Name,
    string DisplayName,
    IReadOnlyList<QueryParameter> Parameters,
    IReadOnlyList<QueryColumn> Columns,
    string Source)
{
    /// <summary>
    /// Describes a saved query from its declared schema (the <c>properties</c> of
    /// <see cref="Query.Schema"/>, each with a <c>type</c> of <c>string</c>, <c>integer</c>,
    /// <c>number</c>, <c>boolean</c> or <c>datetime</c>); no columns when none is declared.
    /// </summary>
    public static QueryDescriptor FromSchema(Query query)
    {
        var columns = new List<QueryColumn>();

        if (!string.IsNullOrWhiteSpace(query.Schema))
        {
            try
            {
                var properties = JsonNode.Parse(query.Schema)?["properties"]?.AsObject();

                if (properties is not null)
                {
                    foreach (var property in properties)
                    {
                        var type = property.Value?["type"]?.ToString()?.ToLowerInvariant() switch
                        {
                            "integer" => typeof(long),
                            "number" => typeof(decimal),
                            "boolean" => typeof(bool),
                            "datetime" => typeof(DateTime),
                            _ => typeof(string),
                        };

                        columns.Add(new QueryColumn(property.Key, type));
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // An unreadable schema describes no columns.
            }
        }

        return new QueryDescriptor(query.Name, query.Name, [], columns, query.Source);
    }
}

/// <summary>
/// A source that can describe its queries: the ones it serves without a saved definition
/// (the system queries) and the saved ones whose shape it knows (a structured query).
/// </summary>
public interface IQueryDescriber
{
    /// <summary>The source name, as the keyed <see cref="IQuerySource"/> is registered.</summary>
    string Source { get; }

    /// <summary>Queries this source serves with no saved definition.</summary>
    IReadOnlyList<QueryDescriptor> BuiltInQueries { get; }

    /// <summary>Describes a saved query of this source; null when the source cannot.</summary>
    QueryDescriptor? Describe(Query query);
}

/// <summary>Every query the registry can list: the saved queries and the built-in ones.</summary>
public interface IQueryCatalog
{
    Task<IReadOnlyList<QueryDescriptor>> ListAsync(CancellationToken cancellationToken = default);

    Task<QueryDescriptor?> FindAsync(string name, CancellationToken cancellationToken = default);
}
