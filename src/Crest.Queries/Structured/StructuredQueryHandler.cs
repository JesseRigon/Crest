#nullable enable
using System.Text.Json;
using Crest.Entities;
using Crest.Queries;

namespace Crest.Queries.Structured;

/// <summary>Reads the definition from the data a query is created or updated with, and checks it resolves.</summary>
public sealed class StructuredQueryHandler : QueryHandlerBase
{
    private readonly IIndexTableCatalog _catalog;

    public StructuredQueryHandler(IIndexTableCatalog catalog)
    {
        _catalog = catalog;
    }

    public override Task InitializingAsync(InitializingQueryContext context)
        => UpdateQueryAsync(context);

    public override Task UpdatingAsync(UpdatingQueryContext context)
        => UpdateQueryAsync(context);

    private Task UpdateQueryAsync(DataQueryContextBase context)
    {
        if (context.Query.Source != StructuredQuerySource.SourceName)
        {
            return Task.CompletedTask;
        }

        var definition = context.Data?[nameof(StructuredQueryMetadata.Definition)];

        if (definition is not null)
        {
            var metadata = context.Query.GetOrCreate<StructuredQueryMetadata>();
            metadata.Definition = definition.Deserialize<StructuredQuery>(JOptions.Default) ?? new StructuredQuery();
            context.Query.Put(metadata);
        }

        if (context.Query.TryGet<StructuredQueryMetadata>(out var saved) && saved.Definition.Steps.Count > 0)
        {
            // Throws StructuredQueryException when a table, column or parameter does not resolve.
            QueryPlan.Build(saved.Definition, _catalog);
        }

        return Task.CompletedTask;
    }
}
