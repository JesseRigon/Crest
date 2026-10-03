namespace Crest.Workflows.Connectors;

/// <summary>The generic connectors every tenant has: any HTTP API, and inbound signed webhooks.</summary>
public sealed class CrestConnectorProvider : IWorkflowConnectorProvider
{
    public IEnumerable<WorkflowConnectorDescriptor> Connectors =>
    [
        new("http", "HTTP API", WorkflowConnectorAuthKinds.None, Description: "Any HTTP API: set the base URL and pick the auth kind on the connection."),
        new("webhook", "Signed webhook", WorkflowConnectorAuthKinds.Hmac, Description: "Receive POSTs from an external service, verified with an HMAC-SHA256 signature.", Position: 1),
    ];
}
