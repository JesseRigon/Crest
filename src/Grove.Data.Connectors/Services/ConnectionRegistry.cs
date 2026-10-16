using System.Reflection;
using System.Text.RegularExpressions;
using Grove.Access;
using Grove.Data.Connectors.Models;
using Grove.Documents;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Grove.Data.Connectors.Services;

/// <summary>The declared layer: connections modules registered with <c>services.AddConnection</c>.</summary>
public sealed class DeclaredConnectionSource(IEnumerable<ConnectionDeclaration> declarations) : IConnectionSource
{
    public ConnectionOrigin Origin => ConnectionOrigin.Declared;

    public Task<IEnumerable<ConnectionDefinition>> GetConnectionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(declarations.Select(d => d.Definition));
}

/// <summary>The defined layer: the tenant's own connections.</summary>
public sealed class DefinedConnectionSource(IDefinedConnectionStore store) : IConnectionSource
{
    public ConnectionOrigin Origin => ConnectionOrigin.Defined;

    public async Task<IEnumerable<ConnectionDefinition>> GetConnectionsAsync(CancellationToken cancellationToken = default) =>
        await store.ListAsync(cancellationToken);
}

/// <summary>The tenant's defined connections, in one document. Deleting one deletes its credentials.</summary>
public sealed partial class DefinedConnectionStore(
    IDocumentManager<DefinedConnectionsDocument> documents,
    IEnumerable<ConnectionDeclaration> declarations,
    CredentialStore credentials,
    IPermissionVersion permissionVersion,
    IEnumerable<IConnectionChangeHandler> changeHandlers) : IDefinedConnectionStore
{
    public async Task<IReadOnlyList<ConnectionDefinition>> ListAsync(CancellationToken cancellationToken = default) =>
        (await documents.GetOrCreateImmutableAsync()).Connections.Values.Select(c => c.ToDefinition()).OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase).ToArray();

    public async Task<ConnectionDefinition?> FindAsync(string key, CancellationToken cancellationToken = default) =>
        (await documents.GetOrCreateImmutableAsync()).Connections.TryGetValue(key, out var record) ? record.ToDefinition() : null;

    public async Task<ConnectionDefinition> SaveAsync(ConnectionDefinition definition, CancellationToken cancellationToken = default)
    {
        if (!IsValidKey(definition.Key))
        {
            throw new ArgumentException("A connection key is 1-64 lowercase letters, digits or dashes, starting with a letter or digit.", nameof(definition));
        }

        if (declarations.Any(d => string.Equals(d.Definition.Key, definition.Key, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"'{definition.Key}' is a declared connection; a tenant cannot redefine it.");
        }

        var document = await documents.GetOrCreateMutableAsync();
        document.Connections.TryGetValue(definition.Key, out var previous);
        var record = DefinedConnectionRecord.From(definition);
        record.UpdatedUtc = DateTime.UtcNow;
        document.Connections[definition.Key] = record;
        await documents.UpdateAsync(document);

        if (!(previous?.CallerRoles ?? []).Order(StringComparer.OrdinalIgnoreCase).SequenceEqual(record.CallerRoles.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase))
        {
            // The connection's roles are rights: its cached caller state goes with the version.
            await permissionVersion.BumpAsync(cancellationToken);
        }

        var saved = record.ToDefinition();
        foreach (var handler in changeHandlers)
        {
            await handler.ConnectionChangedAsync(saved.Key, saved, cancellationToken);
        }

        return saved;
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var document = await documents.GetOrCreateMutableAsync();
        if (!document.Connections.Remove(key, out var removed))
        {
            return false;
        }

        await documents.UpdateAsync(document);
        await credentials.DeleteAllAsync(key);
        if (removed.CallerRoles.Count > 0)
        {
            await permissionVersion.BumpAsync(cancellationToken);
        }

        foreach (var handler in changeHandlers)
        {
            await handler.ConnectionChangedAsync(key, null, cancellationToken);
        }

        return true;
    }

    public static bool IsValidKey(string? key) => key is not null && KeyPattern().IsMatch(key);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex KeyPattern();
}

/// <summary>
/// The registry consumers read: every layer merged (a declared connection wins a key clash),
/// and each connection's operations, declared plus those read from its schema (cached per
/// version; a URL schema for an hour).
/// </summary>
public sealed class ConnectionRegistry(
    IEnumerable<IConnectionSource> sources,
    IEnumerable<IConnectorProtocol> protocols,
    SchemaLoader schemas,
    IMemoryCache cache,
    ILogger<ConnectionRegistry> logger) : IConnectionRegistry
{
    private IReadOnlyList<ConnectionDefinition>? _connections;

    public async Task<IReadOnlyList<ConnectionDefinition>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (_connections is not null)
        {
            return _connections;
        }

        var merged = new Dictionary<string, ConnectionDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources.OrderBy(s => s.Origin == ConnectionOrigin.Declared ? 1 : 0))
        {
            foreach (var connection in await source.GetConnectionsAsync(cancellationToken))
            {
                merged[connection.Key] = connection;
            }
        }

        return _connections = merged.Values.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<ConnectionDefinition?> FindAsync(string key, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(key) ? null : (await ListAsync(cancellationToken)).FirstOrDefault(c => string.Equals(c.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));

    public async Task<IReadOnlyList<ConnectionOperation>> GetOperationsAsync(string key, CancellationToken cancellationToken = default)
    {
        var connection = await FindAsync(key, cancellationToken);
        if (connection is null)
        {
            return [];
        }

        if (connection.Schema is null)
        {
            // No schema: the declared operations, else the protocol's own (a mail protocol's fixed ones).
            return connection.Operations.Count > 0
                ? connection.Operations
                : protocols.FirstOrDefault(p => string.Equals(p.Id, connection.Protocol, StringComparison.OrdinalIgnoreCase))?.DefaultOperations ?? [];
        }

        var cacheKey = $"connectors:operations:{connection.Version}:{connection.Schema.Location}:{connection.Schema.Content?.Length}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<ConnectionOperation>? cached) && cached is not null)
        {
            return cached;
        }

        var read = await ReadSchemaAsync(connection, cancellationToken);
        var operations = connection.Operations.Concat(read.Operations.Where(o => connection.Operations.All(d => d.Id != o.Id))).ToArray();
        cache.Set(cacheKey, (IReadOnlyList<ConnectionOperation>)operations, connection.Schema.Kind == ConnectionSchemaSourceKind.Url ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1));
        return operations;
    }

    public async Task<ConnectionOperation?> FindOperationAsync(string key, string operationId, CancellationToken cancellationToken = default) =>
        (await GetOperationsAsync(key, cancellationToken)).FirstOrDefault(o => string.Equals(o.Id, operationId, StringComparison.Ordinal));

    public async Task<IReadOnlyList<ConnectionOperationEntry>> ListOperationsAsync(string? protocol = null, CancellationToken cancellationToken = default)
    {
        var entries = new List<ConnectionOperationEntry>();
        foreach (var connection in await ListAsync(cancellationToken))
        {
            if (protocol is not null && !string.Equals(connection.Protocol, protocol, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var operation in await GetOperationsAsync(connection.Key, cancellationToken))
            {
                entries.Add(new ConnectionOperationEntry(connection.Key, connection.DisplayName, operation));
            }
        }

        return entries;
    }

    /// <summary>Reads a connection's schema through its protocol; failures become warnings, never exceptions.</summary>
    public async Task<SchemaReadResult> ReadSchemaAsync(ConnectionDefinition connection, CancellationToken cancellationToken = default)
    {
        var protocol = protocols.FirstOrDefault(p => string.Equals(p.Id, connection.Protocol, StringComparison.OrdinalIgnoreCase));
        if (protocol is null || connection.Schema is null)
        {
            return new SchemaReadResult([], Warnings: [protocol is null ? $"No protocol '{connection.Protocol}' is registered." : "The connection has no schema."]);
        }

        try
        {
            var document = await schemas.LoadAsync(connection.Schema, cancellationToken);
            return await protocol.ReadOperationsAsync(connection, document, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Reading the schema of connection {Connection} failed.", connection.Key);
            return new SchemaReadResult([], Warnings: [$"The schema could not be read: {ex.Message}"]);
        }
    }
}

/// <summary>Loads a schema document from its source: an embedded resource, a URL (through the guarded client) or inline content.</summary>
public sealed class SchemaLoader(ConnectorHttp http)
{
    private const int MaxSchemaBytes = 8 * 1024 * 1024;

    public async Task<ConnectionSchemaDocument> LoadAsync(ConnectionSchemaSource source, CancellationToken cancellationToken = default)
    {
        switch (source.Kind)
        {
            case ConnectionSchemaSourceKind.Inline:
                return new ConnectionSchemaDocument(source.Format, source.Content ?? string.Empty, source.Location);
            case ConnectionSchemaSourceKind.Bundled:
                var assembly = Assembly.Load(source.AssemblyName ?? throw new InvalidOperationException("A bundled schema names its assembly."));
                var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n == source.Location || n.EndsWith("." + source.Location, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException($"No embedded resource '{source.Location}' in {assembly.GetName().Name}.");
                await using (var stream = assembly.GetManifestResourceStream(name)!)
                using (var reader = new StreamReader(stream))
                {
                    return new ConnectionSchemaDocument(source.Format, await reader.ReadToEndAsync(cancellationToken), source.Location);
                }

            case ConnectionSchemaSourceKind.Url:
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(30));
                    using var response = await http.Client.Client.GetAsync(source.Location, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                    response.EnsureSuccessStatusCode();
                    var content = await ConnectorHttp.ReadCappedAsync(response, MaxSchemaBytes, timeout.Token);
                    return new ConnectionSchemaDocument(source.Format, content ?? string.Empty, source.Location);
                }

            default:
                throw new InvalidOperationException($"Unknown schema source '{source.Kind}'.");
        }
    }
}
