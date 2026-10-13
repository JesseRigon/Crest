using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Access;
using Crest.DisplayManagement.Components;

namespace Crest.Queries.Services;

/// <summary>
/// A display tree's data slots on the server (docs/blazor-display.md decision 4, § 4a): a named
/// query run for the request's caller, with the same decision as the query endpoint (the
/// query's own permission) and the caller's scope applied by the query system. Answers the
/// endpoint's shape, <c>{ items, columns, total, nextPageToken }</c>, so a slot's path reads
/// the same on the server and in the browser; null when the query is missing or refused.
/// </summary>
public sealed class QueryDisplayDataSource(
    IQueryManager queryManager,
    ICallerContextAccessor callers,
    IAccessDecision decision) : IDisplayDataSource
{
    public async Task<JsonNode?> QueryAsync(string query, IReadOnlyDictionary<string, JsonNode?> parameters, CancellationToken cancellationToken = default)
    {
        var definition = await queryManager.GetQueryAsync(query);
        if (definition is null || callers.Current is not { } caller)
        {
            return null;
        }

        if (!(await decision.DecideAsync(caller, Permissions.CreatePermissionForQuery(definition.Name).Name, cancellationToken: cancellationToken)).IsAllowed)
        {
            return null;
        }

        var values = parameters.ToDictionary(pair => pair.Key, pair => pair.Value is null ? null! : (object)JsonSerializer.Deserialize<JsonElement>(pair.Value.ToJsonString()));
        try
        {
            var result = await queryManager.ExecuteQueryAsync(definition, new QueryRequest { Parameters = values, CancellationToken = cancellationToken });
            return JsonSerializer.SerializeToNode(new
            {
                items = result.Items,
                columns = result.Columns.Select(column => new { name = column.Name, type = column.Type.Name }),
                total = result.Total,
                nextPageToken = result.NextPageToken,
            });
        }
        catch (ScopeRefusedException)
        {
            return null;
        }
    }
}
