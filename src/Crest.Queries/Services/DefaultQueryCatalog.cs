#nullable enable
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Queries.Services;

/// <summary>Lists every saved query and every built-in one as descriptors, for the registry.</summary>
public sealed class DefaultQueryCatalog : IQueryCatalog
{
    private readonly IQueryManager _queryManager;
    private readonly IEnumerable<IQueryDescriber> _describers;

    public DefaultQueryCatalog(IQueryManager queryManager, IEnumerable<IQueryDescriber> describers)
    {
        _queryManager = queryManager;
        _describers = describers;
    }

    public async Task<IReadOnlyList<QueryDescriptor>> ListAsync(CancellationToken cancellationToken = default)
    {
        var descriptors = new List<QueryDescriptor>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in await _queryManager.ListQueriesAsync(new QueryContext { Sorted = true }))
        {
            cancellationToken.ThrowIfCancellationRequested();
            descriptors.Add(Describe(query));
            names.Add(query.Name);
        }

        foreach (var describer in _describers)
        {
            foreach (var builtIn in describer.BuiltInQueries)
            {
                if (names.Add(builtIn.Name))
                {
                    descriptors.Add(builtIn);
                }
            }
        }

        return descriptors;
    }

    public async Task<QueryDescriptor?> FindAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var query = await _queryManager.GetQueryAsync(name);

        if (query is null)
        {
            return null;
        }

        return Describe(query);
    }

    private QueryDescriptor Describe(Query query)
    {
        var describer = _describers.FirstOrDefault(candidate => string.Equals(candidate.Source, query.Source, StringComparison.OrdinalIgnoreCase));

        return describer?.BuiltInQueries.FirstOrDefault(builtIn => string.Equals(builtIn.Name, query.Name, StringComparison.OrdinalIgnoreCase))
            ?? describer?.Describe(query)
            ?? QueryDescriptor.FromSchema(query);
    }
}
