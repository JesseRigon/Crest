using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Data.Connectors;

namespace Grove.Data.Protocols.Rest;

/// <summary>
/// Reads an OpenAPI 3 document (JSON) into a connection's operations: the first server's URL
/// as the base URL, the security scheme the document's global security names (else the first)
/// as the auth scheme, and every path operation with its parameters, body, response schema,
/// safe-to-retry flag (GET, HEAD, OPTIONS), idempotency-key support (an <c>Idempotency-Key</c>
/// header parameter) and paging (a <c>cursor</c>, <c>page</c> or <c>offset</c> query parameter).
/// No code generation: a caller binds arguments by parameter name.
/// </summary>
public static class OpenApiReader
{
    private static readonly string[] Methods = ["get", "post", "put", "patch", "delete", "head", "options"];

    public static SchemaReadResult Read(string document)
    {
        var root = JsonNode.Parse(document) as JsonObject ?? throw new JsonException("The document is not a JSON object.");
        if (root["openapi"]?.GetValue<string>() is not { } version || !version.StartsWith('3'))
        {
            throw new JsonException("Only OpenAPI 3.x documents are supported (the 'openapi' field).");
        }

        var warnings = new List<string>();
        var title = root["info"]?["title"]?.GetValue<string>();
        var baseUrl = (root["servers"] as JsonArray)?.OfType<JsonObject>().Select(s => s["url"]?.GetValue<string>()).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        if (baseUrl is null)
        {
            warnings.Add("The document names no server; set the base URL on the connection.");
        }
        else if (baseUrl.Contains('{'))
        {
            warnings.Add($"The server URL '{baseUrl}' has variables; set the base URL on the connection.");
            baseUrl = null;
        }

        var auth = ReadSecurity(root, warnings);
        var operations = new List<ConnectionOperation>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (root["paths"] is JsonObject paths)
        {
            foreach (var (path, pathItem) in paths)
            {
                if (pathItem is not JsonObject item)
                {
                    continue;
                }

                var shared = item["parameters"] as JsonArray;
                foreach (var method in Methods)
                {
                    if (item[method] is not JsonObject operation)
                    {
                        continue;
                    }

                    var id = operation["operationId"]?.GetValue<string>() ?? $"{method}{path.Replace('/', '-').Replace("{", "").Replace("}", "")}";
                    if (!ids.Add(id))
                    {
                        warnings.Add($"Operation id '{id}' appears twice; the second is skipped.");
                        continue;
                    }

                    operations.Add(ReadOperation(root, id, method, path, operation, shared));
                }
            }
        }

        if (operations.Count == 0)
        {
            warnings.Add("The document has no operations under 'paths'.");
        }

        return new SchemaReadResult(operations, baseUrl, title, warnings, auth);
    }

    private static ConnectionOperation ReadOperation(JsonObject root, string id, string method, string path, JsonObject operation, JsonArray? shared)
    {
        var parameters = new List<OperationParameter>();
        foreach (var node in (shared ?? []).Concat(operation["parameters"] as JsonArray ?? []))
        {
            if (Resolve(root, node) is not JsonObject parameter || parameter["name"]?.GetValue<string>() is not { } name)
            {
                continue;
            }

            var location = parameter["in"]?.GetValue<string>()?.ToLowerInvariant() switch
            {
                "path" => ParameterLocation.Path,
                "header" => ParameterLocation.Header,
                "query" => ParameterLocation.Query,
                _ => (ParameterLocation?)null,
            };
            if (location is null)
            {
                continue;
            }

            parameters.RemoveAll(p => p.Name == name && p.In == location);
            parameters.Add(new OperationParameter(name, location.Value, parameter["required"]?.GetValue<bool>() == true || location == ParameterLocation.Path,
                Resolve(root, parameter["schema"])?.ToJsonString(), parameter["description"]?.GetValue<string>()));
        }

        string? contentType = null;
        string? inputSchema = null;
        if (Resolve(root, operation["requestBody"]) is JsonObject body && body["content"] is JsonObject content && content.Count > 0)
        {
            var (type, media) = content.FirstOrDefault(c => c.Key.Contains("json", StringComparison.OrdinalIgnoreCase)) is { Value: not null } json ? json : content.First();
            contentType = type;
            inputSchema = Resolve(root, media?["schema"])?.ToJsonString();
            parameters.Add(new OperationParameter(HttpBodyName, ParameterLocation.Body, body["required"]?.GetValue<bool>() == true, inputSchema));
        }

        string? outputSchema = null;
        if (operation["responses"] is JsonObject responses)
        {
            var success = responses.FirstOrDefault(r => r.Key.StartsWith('2')).Value;
            if (Resolve(root, success)?["content"] is JsonObject responseContent && responseContent.Count > 0)
            {
                outputSchema = Resolve(root, responseContent.First().Value?["schema"])?.ToJsonString();
            }
        }

        var idempotency = parameters.FirstOrDefault(p => p.In == ParameterLocation.Header && p.Name.Equals("Idempotency-Key", StringComparison.OrdinalIgnoreCase));
        if (idempotency is not null)
        {
            parameters.Remove(idempotency);
        }

        var pagingParameter = parameters.FirstOrDefault(p => p.In == ParameterLocation.Query && p.Name is "cursor" or "page_token" or "pageToken" or "after" or "starting_after")?.Name;
        OperationPaging? paging = pagingParameter is not null
            ? new OperationPaging(PagingStyle.Cursor, pagingParameter)
            : parameters.FirstOrDefault(p => p.In == ParameterLocation.Query && p.Name is "page") is { } page
                ? new OperationPaging(PagingStyle.Page, page.Name, parameters.FirstOrDefault(p => p.Name is "per_page" or "pageSize" or "page_size" or "limit")?.Name)
                : null;

        return new ConnectionOperation
        {
            Id = id,
            DisplayName = operation["summary"]?.GetValue<string>() ?? id,
            Description = operation["description"]?.GetValue<string>(),
            Kind = OperationKinds.Http,
            Method = method.ToUpperInvariant(),
            Path = path,
            ContentType = contentType,
            InputSchema = inputSchema,
            OutputSchema = outputSchema,
            Parameters = parameters,
            IsSafeToRetry = ConnectionOperation.IsSafeMethod(method),
            SupportsIdempotencyKey = idempotency is not null,
            Paging = paging,
            Tags = (operation["tags"] as JsonArray)?.Select(t => t?.GetValue<string>()).OfType<string>().ToArray() ?? [],
        };
    }

    private const string HttpBodyName = Http.HttpComposer.BodyArgument;

    /// <summary>Follows a local <c>$ref</c> (<c>#/components/...</c>) once per level, at most 8 deep.</summary>
    private static JsonNode? Resolve(JsonObject root, JsonNode? node)
    {
        for (var depth = 0; depth < 8 && node is JsonObject obj && obj["$ref"]?.GetValue<string>() is { } reference; depth++)
        {
            if (!reference.StartsWith("#/", StringComparison.Ordinal))
            {
                return node;
            }

            JsonNode? target = root;
            foreach (var segment in reference[2..].Split('/'))
            {
                target = target?[segment.Replace("~1", "/").Replace("~0", "~")];
            }

            node = target;
        }

        return node;
    }

    private static ConnectionAuth? ReadSecurity(JsonObject root, List<string> warnings)
    {
        if (root["components"]?["securitySchemes"] is not JsonObject schemes || schemes.Count == 0)
        {
            return ConnectionAuth.None;
        }

        var preferred = (root["security"] as JsonArray)?.OfType<JsonObject>().SelectMany(s => s.Select(p => p.Key)).FirstOrDefault();
        var scheme = (preferred is not null && schemes[preferred] is JsonObject chosen ? chosen : schemes.First().Value as JsonObject) ?? new JsonObject();
        var type = scheme["type"]?.GetValue<string>()?.ToLowerInvariant();
        switch (type)
        {
            case "http":
                return scheme["scheme"]?.GetValue<string>()?.ToLowerInvariant() == "basic"
                    ? new ConnectionAuth { Scheme = AuthSchemes.Basic }
                    : new ConnectionAuth { Scheme = AuthSchemes.Bearer };
            case "apikey":
                var location = scheme["in"]?.GetValue<string>()?.ToLowerInvariant();
                if (location is not ("header" or "query"))
                {
                    warnings.Add("The API key scheme is a cookie; connections send keys as a header or a query parameter.");
                }

                return new ConnectionAuth
                {
                    Scheme = AuthSchemes.ApiKey,
                    KeyName = scheme["name"]?.GetValue<string>() ?? "X-Api-Key",
                    KeyLocation = location == "query" ? ApiKeyLocation.Query : ApiKeyLocation.Header,
                };
            case "oauth2":
                var flows = scheme["flows"] as JsonObject;
                if (flows?["authorizationCode"] is JsonObject code)
                {
                    return new ConnectionAuth
                    {
                        Scheme = AuthSchemes.OAuth2AuthorizationCode,
                        AuthorizeUrl = code["authorizationUrl"]?.GetValue<string>(),
                        TokenUrl = code["tokenUrl"]?.GetValue<string>(),
                        Scopes = Scopes(code),
                    };
                }

                if (flows?["clientCredentials"] is JsonObject client)
                {
                    return new ConnectionAuth
                    {
                        Scheme = AuthSchemes.OAuth2ClientCredentials,
                        TokenUrl = client["tokenUrl"]?.GetValue<string>(),
                        Scopes = Scopes(client),
                    };
                }

                warnings.Add("The OAuth2 scheme has neither an authorizationCode nor a clientCredentials flow; set the auth scheme on the connection.");
                return ConnectionAuth.None;
            default:
                warnings.Add($"Security scheme type '{type}' is not supported; set the auth scheme on the connection.");
                return ConnectionAuth.None;
        }
    }

    private static string[] Scopes(JsonObject flow) => (flow["scopes"] as JsonObject)?.Select(s => s.Key).ToArray() ?? [];
}
