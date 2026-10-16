#nullable enable

using System.Text.Json.Nodes;

namespace Grove.Data.Queries.Connections;

/// <summary>A query over one operation of a connection (REST, GraphQL, SOAP): stored on the query under its type name.</summary>
public sealed class ConnectionQueryMetadata
{
    /// <summary>The connection key.</summary>
    public string? Connection { get; set; }

    /// <summary>The operation id, as the connection lists it.</summary>
    public string? Operation { get; set; }

    /// <summary>Fixed arguments; a run's parameters of the same name replace them.</summary>
    public JsonObject? Arguments { get; set; }

    /// <summary>Where the rows are in the answer (dot separated, e.g. <c>data.items</c>); the usual envelopes when empty.</summary>
    public string? RowsPath { get; set; }
}
