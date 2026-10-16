#nullable enable

using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Data.Connectors;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Data.Queries.Connections;

/// <summary>A connector call a query could not complete: refused, unauthorized, failed or asked to wait.</summary>
public sealed class ConnectionQueryException(string message, ConnectorResult? result = null) : InvalidOperationException(message)
{
    public ConnectorResult? Result { get; } = result;
}

/// <summary>
/// What the connection sources share: who a run calls as, the call through the engine, and
/// mapping its answer into typed rows.
/// </summary>
public static class ConnectionQueryRunner
{
    /// <summary>
    /// The caller a run calls the engine as. A staff caller (admin shell, or a system actor)
    /// calls as themselves, so a per-user connection uses their own credential. Anyone else was
    /// admitted by the query's own permission and runs on the tenant's credential through the
    /// tenant's system actor: member shells hold no credentials (docs/connectors.md › Credentials).
    /// </summary>
    public static async Task<(CallerContext Caller, CredentialScope? Scope)> ResolveCallerAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var current = services.GetService<ICallerContextAccessor>()?.Current;
        if (current is not null && ConnectorCallers.MayCall(current))
        {
            return (current, null);
        }

        var factory = services.GetService<ICallerContextFactory>()
            ?? throw new ConnectionQueryException("No caller: the access machinery is not available.");

        return (await factory.CreateSystemAsync(cancellationToken: cancellationToken), CredentialScope.Tenant);
    }

    /// <summary>Runs the call and throws <see cref="ConnectionQueryException"/> unless it succeeded.</summary>
    public static async Task<ConnectorResult> ExecuteAsync(IConnectorEngine engine, ConnectorCall call, CancellationToken cancellationToken)
    {
        var result = await engine.ExecuteAsync(call, cancellationToken);

        if (result.Succeeded)
        {
            return result;
        }

        throw new ConnectionQueryException(result.Outcome switch
        {
            ConnectorOutcome.NotAuthorized => $"Connection '{call.ConnectionKey}' has no usable credential for this caller: {result.Error}",
            ConnectorOutcome.RetryAfter => $"Connection '{call.ConnectionKey}' asked to wait until {result.RetryAt:u}.",
            ConnectorOutcome.Refused => $"Connection '{call.ConnectionKey}' refused the call: {result.Error}",
            _ => $"Connection '{call.ConnectionKey}' failed ({result.StatusCode}): {result.Error}",
        }, result);
    }

    /// <summary>
    /// The rows of an answer: an array, or the array at <paramref name="rowsPath"/> (dot
    /// separated), or the first array under a usual envelope (<c>data</c>, <c>items</c>,
    /// <c>value</c>, <c>results</c>, <c>records</c>), else the object itself as one row. A
    /// scalar element becomes a row with one <c>value</c> column.
    /// </summary>
    public static List<JsonObject> Rows(JsonNode? value, string? rowsPath)
    {
        var node = value;

        if (!string.IsNullOrWhiteSpace(rowsPath))
        {
            foreach (var segment in rowsPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                node = node is JsonObject obj ? obj[segment] : null;
            }
        }
        else if (node is JsonObject envelope)
        {
            foreach (var name in Envelopes)
            {
                if (envelope[name] is JsonArray array)
                {
                    node = array;
                    break;
                }
            }
        }

        return node switch
        {
            null => [],
            JsonArray array => [.. array.Select(ToRow)],
            _ => [ToRow(node)],
        };
    }

    /// <summary>The columns of the rows, in first-seen order, typed by their first non-null value.</summary>
    public static IReadOnlyList<QueryColumn> Columns(IReadOnlyList<JsonObject> rows)
    {
        var types = new Dictionary<string, Type?>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var row in rows)
        {
            foreach (var (name, cell) in row)
            {
                if (!types.TryGetValue(name, out var type))
                {
                    order.Add(name);
                    types[name] = null;
                    type = null;
                }

                if (type is null && cell is not null)
                {
                    types[name] = TypeOf(cell);
                }
            }
        }

        return [.. order.Select(name => new QueryColumn(name, types[name] ?? typeof(string)))];
    }

    /// <summary>The request's parameters as connector arguments.</summary>
    public static JsonObject Arguments(JsonObject? template, IDictionary<string, object> parameters)
    {
        var arguments = template?.DeepClone() as JsonObject ?? [];

        foreach (var (name, value) in parameters)
        {
            arguments[name] = value switch
            {
                null => null,
                JsonNode node => node.DeepClone(),
                JsonElement element => JsonNode.Parse(element.GetRawText()),
                _ => JsonSerializer.SerializeToNode(value, value.GetType()),
            };
        }

        return arguments;
    }

    private static readonly string[] Envelopes = ["data", "items", "value", "results", "records"];

    private static JsonObject ToRow(JsonNode? node) => node switch
    {
        JsonObject obj => (JsonObject)obj.DeepClone(),
        _ => new JsonObject { ["value"] = node?.DeepClone() },
    };

    private static Type TypeOf(JsonNode cell) => cell switch
    {
        JsonObject or JsonArray => typeof(JsonNode),
        JsonValue value => value.GetValueKind() switch
        {
            JsonValueKind.True or JsonValueKind.False => typeof(bool),
            JsonValueKind.Number => value.TryGetValue<long>(out _) ? typeof(long) : typeof(double),
            JsonValueKind.String when value.TryGetValue<DateTime>(out _) => typeof(DateTime),
            _ => typeof(string),
        },
        _ => typeof(string),
    };
}
