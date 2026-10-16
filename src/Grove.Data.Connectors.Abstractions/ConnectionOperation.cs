namespace Grove.Data.Connectors;

/// <summary>The operation kinds the shipped protocols produce.</summary>
public static class OperationKinds
{
    public const string Http = "http";
    public const string Soap = "soap";
    public const string GraphQLQuery = "graphql-query";
    public const string GraphQLMutation = "graphql-mutation";
    public const string SqlQuery = "sql-query";
    public const string SqlCommand = "sql-command";
    public const string Sdk = "sdk";
}

/// <summary>
/// One thing a connection can do, read from its schema or declared in code: listed by the
/// registry for the workflow palette, Queries' sources and components.
/// </summary>
public sealed record ConnectionOperation
{
    /// <summary>Unique within the connection (an OpenAPI operationId, a WSDL operation, a GraphQL field).</summary>
    public required string Id { get; init; }

    public string? DisplayName { get; init; }

    public string? Description { get; init; }

    /// <summary><see cref="OperationKinds"/>.</summary>
    public string Kind { get; init; } = OperationKinds.Http;

    /// <summary>HTTP method (GET, POST, ...); null for non-HTTP kinds.</summary>
    public string? Method { get; init; }

    /// <summary>The path template under the base URL (<c>/users/{id}</c>), the SOAP action, the GraphQL document or the SQL text.</summary>
    public string? Path { get; init; }

    public string? ContentType { get; init; }

    /// <summary>JSON Schema of the input (parameters and body), when known.</summary>
    public string? InputSchema { get; init; }

    /// <summary>JSON Schema of the output, when known.</summary>
    public string? OutputSchema { get; init; }

    public IReadOnlyList<OperationParameter> Parameters { get; init; } = [];

    /// <summary>
    /// True when repeating the operation cannot change anything (GET, HEAD, OPTIONS, a query):
    /// retried on any transient failure and cacheable. An unsafe one is retried only when the
    /// failure proves nothing was sent, or when it carries an idempotency key.
    /// </summary>
    public bool IsSafeToRetry { get; init; }

    /// <summary>The service accepts an idempotency key on this operation, in <see cref="IdempotencyKeyHeader"/>.</summary>
    public bool SupportsIdempotencyKey { get; init; }

    public string IdempotencyKeyHeader { get; init; } = "Idempotency-Key";

    public OperationPaging? Paging { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>The safe default for an HTTP method.</summary>
    public static bool IsSafeMethod(string? method) =>
        method is not null && (method.Equals("GET", StringComparison.OrdinalIgnoreCase)
            || method.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
            || method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase));
}

public enum ParameterLocation
{
    Path,
    Query,
    Header,
    Body,
}

public sealed record OperationParameter(string Name, ParameterLocation In, bool Required = false, string? Schema = null, string? Description = null);

public enum PagingStyle
{
    None,
    Offset,
    Page,
    Cursor,

    /// <summary>The next page's URL in a <c>Link</c> header or a body field (still under the base URL).</summary>
    Link,
}

/// <summary>How an operation pages: the parameter it takes and where the next position comes from.</summary>
public sealed record OperationPaging(PagingStyle Style, string? Parameter = null, string? SizeParameter = null, string? NextPath = null);
