#nullable enable
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Grove.Data.Connectors;
using Grove.Data.Protocols.Http;

namespace Grove.Data.Protocols.GraphQL.Client;

/// <summary>
/// An external GraphQL endpoint (the base URL). The schema (an introspection result as JSON,
/// or SDL) yields one operation per root field: a query field is safe to retry, a mutation is
/// not. A call posts <c>{ query, variables }</c>: the operation's document, or the
/// <c>query</c> argument's; the <c>variables</c> argument, or the other arguments, as variables.
/// A response with <c>errors</c> and no <c>data</c> is a permanent failure.
/// </summary>
public sealed partial class GraphQLConnectorProtocol : IConnectorProtocol
{
    public const string QueryArgument = "query";
    public const string VariablesArgument = "variables";

    public string Id => ConnectorProtocols.GraphQL;

    public string DisplayName => "GraphQL";

    public IReadOnlyList<string> AuthSchemes { get; } =
    [
        Connectors.AuthSchemes.None,
        Connectors.AuthSchemes.ApiKey,
        Connectors.AuthSchemes.Basic,
        Connectors.AuthSchemes.Bearer,
        Connectors.AuthSchemes.OAuth2ClientCredentials,
        Connectors.AuthSchemes.OAuth2AuthorizationCode,
        Connectors.AuthSchemes.OAuth2DeviceCode,
    ];

    public IReadOnlyList<string> SchemaFormats => [ConnectionSchemaFormats.GraphQL];

    public Task<SchemaReadResult> ReadOperationsAsync(ConnectionDefinition connection, ConnectionSchemaDocument schema, CancellationToken cancellationToken = default)
    {
        var content = schema.Content.TrimStart();
        try
        {
            return Task.FromResult(content.StartsWith('{') ? ReadIntrospection(content) : ReadSdl(content));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new SchemaReadResult([], Warnings: [$"Not a GraphQL introspection result: {ex.Message}"]));
        }
    }

    public async Task<ProtocolResponse> SendAsync(IProtocolCallContext context, CancellationToken cancellationToken = default)
    {
        var arguments = context.Arguments ?? [];
        var document = context.Request?.Body is not null ? null : Text(arguments, QueryArgument) ?? context.Operation?.Path;
        string payload;
        if (context.Request?.Body is { } body)
        {
            payload = body;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(document))
            {
                return ProtocolResponse.Failed(new FailureClassification(FailureKind.Refused, NotSent: true), "No GraphQL document to send.");
            }

            JsonObject variables;
            if (arguments.TryGetPropertyValue(VariablesArgument, out var given) && given is JsonObject explicitVariables)
            {
                variables = (JsonObject)explicitVariables.DeepClone();
            }
            else
            {
                variables = [];
                foreach (var (name, value) in arguments)
                {
                    if (name is not (QueryArgument or VariablesArgument))
                    {
                        variables[name] = value?.DeepClone();
                    }
                }
            }

            payload = new JsonObject { ["query"] = document, ["variables"] = variables }.ToJsonString();
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, context.ResolveUri(context.Request?.Path ?? string.Empty))
        {
            Content = new StringContent(payload, Encoding.UTF8),
        };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        message.Headers.Accept.ParseAdd("application/json");
        var response = await HttpComposer.SendAsync(context, message, cancellationToken);
        if (!response.Failure.IsFailure && response.Value is JsonObject result && result["errors"] is JsonArray { Count: > 0 } errors && result["data"] is null)
        {
            return response with
            {
                Failure = new FailureClassification(FailureKind.Permanent),
                Error = "GraphQL errors: " + string.Join("; ", errors.Select(e => e?["message"]?.ToString()).Where(m => m is not null)),
            };
        }

        return response;
    }

    public FailureClassification Classify(Exception exception) => HttpFailures.Classify(exception);

    public static SchemaReadResult ReadIntrospection(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("Not a JSON object.");
        var schema = (root["data"]?["__schema"] ?? root["__schema"]) as JsonObject ?? throw new JsonException("No __schema.");
        var types = (schema["types"] as JsonArray ?? []).OfType<JsonObject>().ToDictionary(t => t["name"]?.GetValue<string>() ?? string.Empty, StringComparer.Ordinal);
        var operations = new List<ConnectionOperation>();
        AddRoot(schema["queryType"]?["name"]?.GetValue<string>(), "query", OperationKinds.GraphQLQuery);
        AddRoot(schema["mutationType"]?["name"]?.GetValue<string>(), "mutation", OperationKinds.GraphQLMutation);
        return new SchemaReadResult(operations, Warnings: operations.Count == 0 ? ["The schema has no query or mutation fields."] : null);

        void AddRoot(string? typeName, string keyword, string kind)
        {
            if (typeName is null || !types.TryGetValue(typeName, out var type))
            {
                return;
            }

            foreach (var field in (type["fields"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var name = field["name"]?.GetValue<string>();
                if (name is null)
                {
                    continue;
                }

                var args = (field["args"] as JsonArray ?? []).OfType<JsonObject>()
                    .Select(a => (Name: a["name"]?.GetValue<string>() ?? string.Empty, Type: TypeRef(a["type"]), Required: a["type"]?["kind"]?.GetValue<string>() == "NON_NULL"))
                    .Where(a => a.Name.Length > 0)
                    .ToList();
                var selection = Selection(types, field["type"]);
                var variables = args.Count == 0 ? string.Empty : "(" + string.Join(", ", args.Select(a => $"${a.Name}: {a.Type}")) + ")";
                var passing = args.Count == 0 ? string.Empty : "(" + string.Join(", ", args.Select(a => $"{a.Name}: ${a.Name}")) + ")";
                operations.Add(new ConnectionOperation
                {
                    Id = keyword == "query" ? name : $"{keyword}.{name}",
                    DisplayName = name,
                    Description = field["description"]?.GetValue<string>(),
                    Kind = kind,
                    Method = "POST",
                    Path = $"{keyword} {name[..1].ToUpperInvariant()}{name[1..]}{variables} {{ {name}{passing}{selection} }}",
                    Parameters = args.Select(a => new OperationParameter(a.Name, ParameterLocation.Body, a.Required, Description: a.Type)).ToArray(),
                    IsSafeToRetry = keyword == "query",
                });
            }
        }
    }

    public static SchemaReadResult ReadSdl(string sdl)
    {
        var operations = new List<ConnectionOperation>();
        foreach (Match type in RootType().Matches(sdl))
        {
            var keyword = type.Groups["name"].Value == "Mutation" ? "mutation" : "query";
            foreach (Match field in Field().Matches(type.Groups["body"].Value))
            {
                var name = field.Groups["name"].Value;
                var args = field.Groups["args"].Success
                    ? field.Groups["args"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(a => a.Split(':', 2, StringSplitOptions.TrimEntries)).Where(a => a.Length == 2).Select(a => (Name: a[0], Type: a[1].Split('=')[0].Trim())).ToList()
                    : [];
                var variables = args.Count == 0 ? string.Empty : "(" + string.Join(", ", args.Select(a => $"${a.Name}: {a.Type}")) + ")";
                var passing = args.Count == 0 ? string.Empty : "(" + string.Join(", ", args.Select(a => $"{a.Name}: ${a.Name}")) + ")";
                var scalar = IsScalar(field.Groups["type"].Value);
                operations.Add(new ConnectionOperation
                {
                    Id = keyword == "query" ? name : $"{keyword}.{name}",
                    DisplayName = name,
                    Kind = keyword == "query" ? OperationKinds.GraphQLQuery : OperationKinds.GraphQLMutation,
                    Method = "POST",
                    Path = $"{keyword} {name[..1].ToUpperInvariant()}{name[1..]}{variables} {{ {name}{passing}{(scalar ? string.Empty : " { __typename }")} }}",
                    Parameters = args.Select(a => new OperationParameter(a.Name, ParameterLocation.Body, a.Type.EndsWith('!'), Description: a.Type)).ToArray(),
                    IsSafeToRetry = keyword == "query",
                });
            }
        }

        return new SchemaReadResult(operations, Warnings: ["Read from SDL: object results select only __typename; pass a 'query' argument to select fields, or import an introspection result."]);
    }

    private static string TypeRef(JsonNode? type) => type?["kind"]?.GetValue<string>() switch
    {
        "NON_NULL" => TypeRef(type["ofType"]) + "!",
        "LIST" => "[" + TypeRef(type["ofType"]) + "]",
        _ => type?["name"]?.GetValue<string>() ?? "String",
    };

    private static string Selection(Dictionary<string, JsonObject> types, JsonNode? type)
    {
        while (type?["kind"]?.GetValue<string>() is "NON_NULL" or "LIST")
        {
            type = type["ofType"];
        }

        if (type?["kind"]?.GetValue<string>() is not ("OBJECT" or "INTERFACE") || type["name"]?.GetValue<string>() is not { } name || !types.TryGetValue(name, out var definition))
        {
            return string.Empty;
        }

        var scalars = (definition["fields"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(f => (f["args"] as JsonArray ?? []).Count == 0 && Leaf(f["type"]))
            .Select(f => f["name"]?.GetValue<string>())
            .OfType<string>()
            .ToList();
        return scalars.Count == 0 ? " { __typename }" : " { " + string.Join(" ", scalars) + " }";

        static bool Leaf(JsonNode? t)
        {
            while (t?["kind"]?.GetValue<string>() is "NON_NULL" or "LIST")
            {
                t = t["ofType"];
            }

            return t?["kind"]?.GetValue<string>() is "SCALAR" or "ENUM";
        }
    }

    private static bool IsScalar(string type) =>
        type.Trim('[', ']', '!', ' ') is "String" or "Int" or "Float" or "Boolean" or "ID" or "DateTime" or "Date" or "JSON" or "Long" or "Decimal";

    private static string? Text(JsonObject arguments, string name) =>
        arguments.TryGetPropertyValue(name, out var value) && value is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;

    [GeneratedRegex(@"type\s+(?<name>Query|Mutation)\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline)]
    private static partial Regex RootType();

    [GeneratedRegex(@"^\s*(?<name>[_A-Za-z][_0-9A-Za-z]*)\s*(\((?<args>[^)]*)\))?\s*:\s*(?<type>[^\s#]+)", RegexOptions.Multiline)]
    private static partial Regex Field();
}
