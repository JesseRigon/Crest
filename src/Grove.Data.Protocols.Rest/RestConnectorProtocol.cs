using System.Text.Json;
using Grove.Data.Connectors;
using Grove.Data.Protocols.Http;

namespace Grove.Data.Protocols.Rest;

/// <summary>REST described by OpenAPI: the HTTP protocol with its operations read from an OpenAPI 3 document.</summary>
public sealed class RestConnectorProtocol : HttpConnectorProtocol
{
    public override string Id => ConnectorProtocols.Rest;

    public override string DisplayName => "REST (OpenAPI)";

    public override IReadOnlyList<string> SchemaFormats => [ConnectionSchemaFormats.OpenApi];

    public override Task<SchemaReadResult> ReadOperationsAsync(ConnectionDefinition connection, ConnectionSchemaDocument schema, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(schema.Format, ConnectionSchemaFormats.OpenApi, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new SchemaReadResult([], Warnings: [$"The REST protocol reads OpenAPI documents, not '{schema.Format}'."]));
        }

        try
        {
            return Task.FromResult(OpenApiReader.Read(schema.Content));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new SchemaReadResult([], Warnings: [$"Not an OpenAPI 3 JSON document: {ex.Message}"]));
        }
    }
}
