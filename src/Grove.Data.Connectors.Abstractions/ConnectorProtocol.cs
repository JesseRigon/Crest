using System.Text.Json.Nodes;

namespace Grove.Data.Connectors;

/// <summary>
/// A protocol module's seam (<c>Grove.Data.Protocols.*</c>): stateless, holds no connection or
/// secret. Reads a schema into operations, builds and sends one attempt through the guarded
/// client, and classifies failures. Retrying, caching, limits and credentials are the engine's.
/// Registered with <c>services.AddConnectorProtocol&lt;T&gt;()</c>.
/// </summary>
public interface IConnectorProtocol
{
    /// <summary><see cref="ConnectorProtocols"/>.</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>The auth schemes this protocol carries (<see cref="AuthSchemes"/>).</summary>
    IReadOnlyList<string> AuthSchemes { get; }

    /// <summary>The schema formats it reads (<see cref="ConnectionSchemaFormats"/>); empty when it has none.</summary>
    IReadOnlyList<string> SchemaFormats { get; }

    /// <summary>Reads a schema into operations (with inputs, outputs, paging and the safe-to-retry flag).</summary>
    Task<SchemaReadResult> ReadOperationsAsync(ConnectionDefinition connection, ConnectionSchemaDocument schema, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes one attempt. Must not throw for a remote failure: returns a response with its
    /// <see cref="ProtocolResponse.Failure"/> classified. Throwing <see cref="ConnectorRefusedException"/>
    /// (or any exception the engine sees before sending) is classified as refused/not sent.
    /// </summary>
    Task<ProtocolResponse> SendAsync(IProtocolCallContext context, CancellationToken cancellationToken = default);

    /// <summary>Classifies an exception the attempt threw (network, timeout, provider exception).</summary>
    FailureClassification Classify(Exception exception);

    /// <summary>
    /// The operations a connection of this protocol has when it declares none and has no
    /// schema (a mail protocol's fixed send, fetch and delete).
    /// </summary>
    IReadOnlyList<ConnectionOperation> DefaultOperations => [];

    /// <summary>The non-secret settings a connection of this protocol takes (rendered by the Connections screen).</summary>
    IReadOnlyList<ConnectionSettingField> SettingFields => [];

    /// <summary>The credential values a connection of this protocol takes, beyond what its auth scheme implies.</summary>
    IReadOnlyList<ConnectionSettingField> CredentialFields => [];

    /// <summary>
    /// The protocol's policy defaults (larger size caps for mail, for example). A connection's
    /// own policy wins wherever it differs from <see cref="ConnectionPolicy"/>'s defaults.
    /// </summary>
    ConnectionPolicy? DefaultPolicy => null;
}

/// <summary>One field of a connection's settings or credential, as a protocol describes it.</summary>
/// <param name="Name">The settings (or credential value) key.</param>
/// <param name="Type"><see cref="ConnectionSettingFieldTypes"/>.</param>
/// <param name="Options">The allowed values of a select.</param>
public sealed record ConnectionSettingField(
    string Name,
    string Label,
    string Type = ConnectionSettingFieldTypes.Text,
    bool Required = false,
    string? Default = null,
    string? Description = null,
    IReadOnlyList<string>? Options = null);

public static class ConnectionSettingFieldTypes
{
    public const string Text = "text";
    public const string Number = "number";
    public const string Boolean = "boolean";
    public const string Select = "select";

    /// <summary>A write-only secret (credential values only).</summary>
    public const string Secret = "secret";
}

/// <summary>
/// What reading a schema produced: the operations; <see cref="BaseUrl"/>, the schema's first
/// server when it names one; <see cref="Auth"/>, the auth scheme the schema declares, when it
/// declares one (an import starts the connection from it).
/// </summary>
public sealed record SchemaReadResult(IReadOnlyList<ConnectionOperation> Operations, string? BaseUrl = null, string? Title = null, IReadOnlyList<string>? Warnings = null, ConnectionAuth? Auth = null);

/// <summary>Everything one attempt needs, given by the engine.</summary>
public interface IProtocolCallContext
{
    ConnectionDefinition Connection { get; }

    /// <summary>The operation run; null for a raw request.</summary>
    ConnectionOperation? Operation { get; }

    JsonObject? Arguments { get; }

    /// <summary>The raw request (or the caller's overrides of the operation's request).</summary>
    ConnectorRequest? Request { get; }

    /// <summary>The resolved credential (null for auth scheme none). A protocol with <see cref="AuthSchemes.Custom"/> applies it itself.</summary>
    ConnectionCredential? Credential { get; }

    string? IdempotencyKey { get; }

    string? Page { get; }

    /// <summary>1 for the first attempt.</summary>
    int Attempt { get; }

    /// <summary>The guarded client: refuses non-public addresses after DNS (unless the shell allows private networks), follows no redirects.</summary>
    HttpClient HttpClient { get; }

    /// <summary>
    /// The shell allows loopback and private addresses (a host's dev setup). A protocol that
    /// opens its own sockets (SQL, mail) checks its host with this before connecting.
    /// </summary>
    bool AllowPrivateNetworks { get; }

    /// <summary>The cap on what the protocol may read from a response; exceeding it throws <see cref="ConnectorRefusedException"/>.</summary>
    long MaxResponseBytes { get; }

    /// <summary>Resolves a relative path (with query) under the base URL; throws <see cref="ConnectorRefusedException"/> when it would leave it.</summary>
    Uri ResolveUri(string? relativePath);

    /// <summary>Applies the connection's auth scheme with the resolved credential (header, query, bearer token refreshed when due).</summary>
    Task AuthorizeAsync(HttpRequestMessage message, CancellationToken cancellationToken = default);

    /// <summary>Reads a response body within <see cref="MaxResponseBytes"/>.</summary>
    Task<string?> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken = default);

    IServiceProvider Services { get; }
}

/// <summary>One attempt's answer.</summary>
public sealed record ProtocolResponse
{
    public int StatusCode { get; init; }

    public string? Body { get; init; }

    public JsonNode? Value { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public FailureClassification Failure { get; init; } = FailureClassification.None;

    public string? Error { get; init; }

    public string? NextPage { get; init; }

    public static ProtocolResponse Failed(FailureClassification failure, string error, int statusCode = 0, string? body = null) =>
        new() { Failure = failure, Error = error, StatusCode = statusCode, Body = body };
}

/// <summary>Refused before a byte was sent (address guard, path guard, size cap): never retried, never an unsafe-op risk.</summary>
public sealed class ConnectorRefusedException(string message, Exception? inner = null) : Exception(message, inner);
