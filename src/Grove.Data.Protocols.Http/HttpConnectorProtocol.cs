using Grove.Data.Connectors;

namespace Grove.Data.Protocols.Http;

/// <summary>
/// Plain HTTP: operations declared in code or entered by hand (method and path), or a raw
/// request under the base URL. The REST protocol adds the OpenAPI schema on top.
/// </summary>
public class HttpConnectorProtocol : IConnectorProtocol
{
    public virtual string Id => ConnectorProtocols.Http;

    public virtual string DisplayName => "HTTP";

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

    public virtual IReadOnlyList<string> SchemaFormats => [];

    public virtual Task<SchemaReadResult> ReadOperationsAsync(ConnectionDefinition connection, ConnectionSchemaDocument schema, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SchemaReadResult([], Warnings: [$"The {DisplayName} protocol reads no schema; declare the operations."]));

    public virtual async Task<ProtocolResponse> SendAsync(IProtocolCallContext context, CancellationToken cancellationToken = default)
    {
        using var message = HttpComposer.Compose(context);
        return await HttpComposer.SendAsync(context, message, cancellationToken);
    }

    public virtual FailureClassification Classify(Exception exception) => HttpFailures.Classify(exception);
}
