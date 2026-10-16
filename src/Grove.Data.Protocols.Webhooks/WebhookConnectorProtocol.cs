using Grove.Data.Connectors;

namespace Grove.Data.Protocols.Webhooks;

/// <summary>
/// An inbound-only connection: an external service posts to it, signed with the connection's
/// secret (<see cref="IWebhookVerifier"/>). It makes no calls.
/// </summary>
public sealed class WebhookConnectorProtocol : IConnectorProtocol
{
    public string Id => ConnectorProtocols.Webhook;

    public string DisplayName => "Signed webhook (inbound)";

    public IReadOnlyList<string> AuthSchemes { get; } = [Connectors.AuthSchemes.Hmac];

    public IReadOnlyList<string> SchemaFormats => [];

    public Task<SchemaReadResult> ReadOperationsAsync(ConnectionDefinition connection, ConnectionSchemaDocument schema, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SchemaReadResult([]));

    public Task<ProtocolResponse> SendAsync(IProtocolCallContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProtocolResponse.Failed(new FailureClassification(FailureKind.Refused, NotSent: true), $"Connection '{context.Connection.Key}' is an inbound webhook; it cannot make calls."));

    public FailureClassification Classify(Exception exception) => new(FailureKind.Permanent);
}
