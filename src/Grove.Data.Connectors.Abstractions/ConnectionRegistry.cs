namespace Grove.Data.Connectors;

/// <summary>An operation with the connection it belongs to: one palette preset, one Queries source entry.</summary>
public sealed record ConnectionOperationEntry(string ConnectionKey, string ConnectionDisplayName, ConnectionOperation Operation);

/// <summary>
/// Every connection of the tenant, declared and defined, and their operations. What consumers
/// read: the workflow palette, Queries' sources, components that call a service, the screens.
/// </summary>
public interface IConnectionRegistry
{
    Task<IReadOnlyList<ConnectionDefinition>> ListAsync(CancellationToken cancellationToken = default);

    Task<ConnectionDefinition?> FindAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>The connection's operations: declared ones plus those read from its schema (cached per <see cref="ConnectionDefinition.Version"/>).</summary>
    Task<IReadOnlyList<ConnectionOperation>> GetOperationsAsync(string key, CancellationToken cancellationToken = default);

    Task<ConnectionOperation?> FindOperationAsync(string key, string operationId, CancellationToken cancellationToken = default);

    /// <summary>Every operation of every connection (optionally of one protocol).</summary>
    Task<IReadOnlyList<ConnectionOperationEntry>> ListOperationsAsync(string? protocol = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// One layer of the registry. The engine ships the declared layer (from
/// <c>services.AddConnection</c>) and the defined layer (the tenant's document); a module may
/// add a source that computes connections. On a key clash, the declared connection wins.
/// </summary>
public interface IConnectionSource
{
    ConnectionOrigin Origin { get; }

    Task<IEnumerable<ConnectionDefinition>> GetConnectionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>A connection declared in code: registered as a singleton by <c>services.AddConnection</c>.</summary>
public sealed record ConnectionDeclaration(ConnectionDefinition Definition);

/// <summary>The tenant's defined connections (data). Writes bump the definition's version.</summary>
public interface IDefinedConnectionStore
{
    Task<IReadOnlyList<ConnectionDefinition>> ListAsync(CancellationToken cancellationToken = default);

    Task<ConnectionDefinition?> FindAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Creates or replaces a defined connection; refuses a key a declared connection uses.</summary>
    Task<ConnectionDefinition> SaveAsync(ConnectionDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Deletes the connection and every credential stored for it.</summary>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Raised to the tenant when a connection, its credentials or app credentials change: a defined
/// connection saved or deleted, a tenant or user credential saved or deleted, app credentials
/// saved or deleted (the key is then the app credentials key; the definition is the connection
/// with that key, if any). Sign-in handlers release the shell on it.
/// </summary>
public interface IConnectionChangeHandler
{
    Task ConnectionChangedAsync(string key, ConnectionDefinition? definition, CancellationToken cancellationToken = default);
}
