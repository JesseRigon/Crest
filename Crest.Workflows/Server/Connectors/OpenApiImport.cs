using System.Text.Json;
using System.Text.Json.Nodes;

namespace Crest.Workflows.Connectors;

/// <summary>What an OpenAPI 3 document yields: enough to create a connection and its operations.</summary>
public sealed record OpenApiImportResult(string? BaseUrl, string AuthKind, Dictionary<string, string> Settings, IReadOnlyList<WorkflowConnectionOperation> Operations, string? Title, IReadOnlyList<string> Warnings);

/// <summary>
/// Reads an OpenAPI 3 document (plans/workflows.md › Connectors): the first server's URL
/// becomes the base URL, the first security scheme the auth kind (http bearer → bearer,
/// http basic → basic, apiKey in header → api-key, oauth2 clientCredentials or
/// authorizationCode → the matching OAuth2 kind with its URLs and scopes), every path
/// operation an entry the registry shows as a palette preset on Call connector. No
/// code generation: a flow still binds the body and path itself.
/// </summary>
public static class OpenApiImport
{
    public static OpenApiImportResult Parse(string document)
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

        var (authKind, settings) = ReadSecurity(root, warnings);

        var operations = new List<WorkflowConnectionOperation>();
        if (root["paths"] is JsonObject paths)
        {
            foreach (var (path, pathItem) in paths)
            {
                if (pathItem is not JsonObject item)
                {
                    continue;
                }

                foreach (var method in new[] { "get", "post", "put", "patch", "delete" })
                {
                    if (item[method] is not JsonObject operation)
                    {
                        continue;
                    }

                    var operationId = operation["operationId"]?.GetValue<string>() ?? $"{method}{path.Replace('/', '-').Replace("{", "").Replace("}", "")}";
                    operations.Add(new(operationId, method.ToUpperInvariant(), path, operation["summary"]?.GetValue<string>() ?? operation["description"]?.GetValue<string>()));
                }
            }
        }

        if (operations.Count == 0)
        {
            warnings.Add("The document has no operations under 'paths'.");
        }

        return new(baseUrl, authKind, settings, operations, title, warnings);
    }

    private static (string AuthKind, Dictionary<string, string> Settings) ReadSecurity(JsonObject root, List<string> warnings)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root["components"]?["securitySchemes"] is not JsonObject schemes || schemes.Count == 0)
        {
            return (WorkflowConnectorAuthKinds.None, settings);
        }

        // The scheme the document's global security names, else the first.
        var preferred = (root["security"] as JsonArray)?.OfType<JsonObject>().SelectMany(s => s.Select(p => p.Key)).FirstOrDefault();
        var scheme = (preferred is not null && schemes[preferred] is JsonObject chosen ? chosen : schemes.First().Value as JsonObject) ?? new JsonObject();
        var type = scheme["type"]?.GetValue<string>()?.ToLowerInvariant();
        switch (type)
        {
            case "http":
                var httpScheme = scheme["scheme"]?.GetValue<string>()?.ToLowerInvariant();
                if (httpScheme == "basic")
                {
                    warnings.Add("Basic auth: set the 'username' setting and the password as the secret.");
                    return (WorkflowConnectorAuthKinds.Basic, settings);
                }

                return (WorkflowConnectorAuthKinds.Bearer, settings);
            case "apikey":
                if (!string.Equals(scheme["in"]?.GetValue<string>(), "header", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add("The API key scheme is not a header; connections send keys as headers only.");
                }

                settings[WorkflowConnectionSettingKeys.HeaderName] = scheme["name"]?.GetValue<string>() ?? WorkflowConnectionSettingKeys.DefaultApiKeyHeader;
                return (WorkflowConnectorAuthKinds.ApiKey, settings);
            case "oauth2":
                var flows = scheme["flows"] as JsonObject;
                if (flows?["clientCredentials"] is JsonObject clientCredentials)
                {
                    settings[WorkflowConnectionSettingKeys.TokenUrl] = clientCredentials["tokenUrl"]?.GetValue<string>() ?? string.Empty;
                    settings[WorkflowConnectionSettingKeys.Scope] = Scopes(clientCredentials);
                    return (WorkflowConnectorAuthKinds.OAuth2ClientCredentials, settings);
                }

                if (flows?["authorizationCode"] is JsonObject authorizationCode)
                {
                    settings[WorkflowConnectionSettingKeys.AuthorizeUrl] = authorizationCode["authorizationUrl"]?.GetValue<string>() ?? string.Empty;
                    settings[WorkflowConnectionSettingKeys.TokenUrl] = authorizationCode["tokenUrl"]?.GetValue<string>() ?? string.Empty;
                    settings[WorkflowConnectionSettingKeys.Scope] = Scopes(authorizationCode);
                    return (WorkflowConnectorAuthKinds.OAuth2AuthorizationCode, settings);
                }

                warnings.Add("The OAuth2 scheme has neither a clientCredentials nor an authorizationCode flow; set the auth kind on the connection.");
                return (WorkflowConnectorAuthKinds.None, settings);
            default:
                warnings.Add($"Security scheme type '{type}' is not supported; set the auth kind on the connection.");
                return (WorkflowConnectorAuthKinds.None, settings);
        }
    }

    private static string Scopes(JsonObject flow) => string.Join(' ', (flow["scopes"] as JsonObject)?.Select(s => s.Key) ?? []);
}

/// <summary>
/// Every imported operation of every connection as a palette entry: Call connector with the
/// connection, method and path preset, keyed <c>connector.{connection}.{operationId}</c>. A
/// flow author picks "Accountant API › createInvoice" and binds the body.
/// </summary>
public sealed class ConnectionOperationActivityProvider(WorkflowConnectionService connections) : IWorkflowActivityProvider
{
    public IEnumerable<WorkflowActivityDescriptor> Activities
    {
        get
        {
            var all = connections.ListAsync().GetAwaiter().GetResult();
            foreach (var connection in all)
            {
                var operations = WorkflowConnectionService.ReadOperations(connection);
                if (operations is null)
                {
                    continue;
                }

                var position = 2000;
                foreach (var operation in operations)
                {
                    yield return new WorkflowActivityDescriptor(
                        $"connector.{connection.Key}.{operation.OperationId}",
                        $"{connection.DisplayName} › {operation.OperationId}",
                        WorkflowsConstants.Objects.Workflow,
                        "Crest.Workflows.CallConnector",
                        operation.Summary ?? $"{operation.Method} {operation.Path}",
                        position++,
                        Category: connection.DisplayName,
                        Inputs: new Dictionary<string, string>
                        {
                            ["connection"] = connection.Key,
                            ["method"] = operation.Method,
                            ["path"] = operation.Path.TrimStart('/'),
                        });
                }
            }
        }
    }
}
