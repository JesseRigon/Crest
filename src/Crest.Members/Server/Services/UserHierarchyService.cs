using System.Data.Common;
using Dapper;
using Crest.Members.Models;
using YesSql;

namespace Crest.Members.Services;

/// <summary>
/// Raw-table implementation that runs on the request's YesSql <see cref="ISession"/>:
/// the session's own connection and transaction, so every hierarchy write commits or
/// rolls back together with the documents the same unit of work saves (no second
/// connection, hence no SQLite write-lock conflict and no deferred task). The table
/// stays a plain table rather than a YesSql index because the node id is an identity
/// embedded in the materialized path and subtree reads are LIKE 'path%' matches.
/// Table name composed from the store's TablePrefix, identifiers quoted through the
/// ISqlDialect so SQLite/SqlServer/Postgres/MySql all work. The root guard is
/// structural: every statement filters on RootKind + OrganizationId.
/// </summary>
public class UserHierarchyService : IUserHierarchyService
{
    public const string TableName = "CrestUserHierarchy";

    private readonly ISession _session;
    private readonly IStore _store;

    public UserHierarchyService(ISession session)
    {
        _session = session;
        _store = session.Store;
    }

    // Writes join (or open) the unit of work's transaction; the session commits it.
    private Task<DbTransaction> WriteTransactionAsync(CancellationToken cancellationToken)
        => _session.BeginTransactionAsync(cancellationToken);

    // Reads use the session's connection and whatever transaction it already holds, the
    // same way YesSql's own queries do, so they see this unit's uncommitted writes.
    private async Task<(DbConnection Connection, DbTransaction? Transaction)> ReadContextAsync(CancellationToken cancellationToken)
        => (await _session.CreateConnectionAsync(cancellationToken), _session.CurrentTransaction);

    private string QuotedTable()
        => _store.Configuration.SqlDialect.QuoteForTableName(
            $"{_store.Configuration.TablePrefix}{TableName}",
            _store.Configuration.Schema);

    private string Col(string name) => _store.Configuration.SqlDialect.QuoteForColumnName(name);

    private string RootFilter()
        => $"{Col("RootKind")} = @RootKind AND ({Col("OrganizationId")} = @OrganizationId OR (@OrganizationId IS NULL AND {Col("OrganizationId")} IS NULL))";

    public async Task<HierarchyNodeModel> AddAsync(HierarchyRoot root, string userId, long? parentNodeId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var transaction = await WriteTransactionAsync(cancellationToken);
        var connection = transaction.Connection!;

        string? parentPath = null;
        if (parentNodeId is not null)
        {
            var parent = await connection.QuerySingleOrDefaultAsync<HierarchyRow>(
                $"SELECT * FROM {QuotedTable()} WHERE {Col("Id")} = @Id AND {RootFilter()}",
                new { Id = parentNodeId, root.RootKind, root.OrganizationId }, transaction)
                ?? throw new InvalidOperationException($"Parent node {parentNodeId} does not exist in this root.");
            parentPath = parent.Path;
        }

        // Two-step because the path contains the identity id: insert (with the
        // dialect's identity-select appended, the same way YesSql retrieves generated
        // ids), then stamp the real path in the same transaction.
        var id = await connection.ExecuteScalarAsync<long>(
            $"INSERT INTO {QuotedTable()} ({Col("UserId")}, {Col("ParentId")}, {Col("RootKind")}, {Col("OrganizationId")}, {Col("Path")}, {Col("Position")}) " +
            "VALUES (@UserId, @ParentId, @RootKind, @OrganizationId, '', 0)" +
            _store.Configuration.SqlDialect.IdentitySelectString,
            new { UserId = userId, ParentId = parentNodeId, root.RootKind, root.OrganizationId }, transaction);

        var path = HierarchyPathMath.BuildPath(parentPath, id);
        await connection.ExecuteAsync(
            $"UPDATE {QuotedTable()} SET {Col("Path")} = @Path WHERE {Col("Id")} = @Id",
            new { Path = path, Id = id }, transaction);

        return new HierarchyNodeModel(id, userId, parentNodeId, root.RootKind, root.OrganizationId, path, 0);
    }

    public async Task MoveAsync(HierarchyRoot root, long nodeId, long? newParentNodeId, CancellationToken cancellationToken = default)
    {
        var transaction = await WriteTransactionAsync(cancellationToken);
        var connection = transaction.Connection!;

        var node = await connection.QuerySingleOrDefaultAsync<HierarchyRow>(
            $"SELECT * FROM {QuotedTable()} WHERE {Col("Id")} = @Id AND {RootFilter()}",
            new { Id = nodeId, root.RootKind, root.OrganizationId }, transaction)
            ?? throw new InvalidOperationException($"Node {nodeId} does not exist in this root.");

        string? newParentPath = null;
        if (newParentNodeId is not null)
        {
            var parent = await connection.QuerySingleOrDefaultAsync<HierarchyRow>(
                $"SELECT * FROM {QuotedTable()} WHERE {Col("Id")} = @Id AND {RootFilter()}",
                new { Id = newParentNodeId, root.RootKind, root.OrganizationId }, transaction)
                ?? throw new InvalidOperationException($"Parent node {newParentNodeId} does not exist in this root.");

            if (HierarchyPathMath.IsWithin(parent.Path, node.Path))
            {
                throw new InvalidOperationException("Cannot move a node under its own subtree.");
            }

            newParentPath = parent.Path;
        }

        var newPath = HierarchyPathMath.BuildPath(newParentPath, node.Id);

        // Rewrite the node and every descendant path (prefix swap), all inside the root.
        var descendants = await connection.QueryAsync<HierarchyRow>(
            $"SELECT * FROM {QuotedTable()} WHERE {Col("Path")} LIKE @PathPrefix AND {RootFilter()}",
            new { PathPrefix = node.Path + "%", root.RootKind, root.OrganizationId }, transaction);

        foreach (var row in descendants)
        {
            await connection.ExecuteAsync(
                $"UPDATE {QuotedTable()} SET {Col("Path")} = @Path WHERE {Col("Id")} = @Id",
                new { Path = HierarchyPathMath.Reroot(row.Path, node.Path, newPath), row.Id }, transaction);
        }

        await connection.ExecuteAsync(
            $"UPDATE {QuotedTable()} SET {Col("ParentId")} = @ParentId WHERE {Col("Id")} = @Id",
            new { ParentId = newParentNodeId, Id = node.Id }, transaction);
    }

    public async Task RemoveAsync(HierarchyRoot root, long nodeId, CancellationToken cancellationToken = default)
    {
        var transaction = await WriteTransactionAsync(cancellationToken);
        var connection = transaction.Connection!;

        var node = await connection.QuerySingleOrDefaultAsync<HierarchyRow>(
            $"SELECT * FROM {QuotedTable()} WHERE {Col("Id")} = @Id AND {RootFilter()}",
            new { Id = nodeId, root.RootKind, root.OrganizationId }, transaction)
            ?? throw new InvalidOperationException($"Node {nodeId} does not exist in this root.");

        var childCount = await connection.ExecuteScalarAsync<long>(
            $"SELECT COUNT(*) FROM {QuotedTable()} WHERE {Col("ParentId")} = @Id",
            new { Id = nodeId }, transaction);
        if (childCount > 0)
        {
            throw new InvalidOperationException("Cannot remove a node that still has children; re-parent them first.");
        }

        await connection.ExecuteAsync(
            $"DELETE FROM {QuotedTable()} WHERE {Col("Id")} = @Id",
            new { Id = nodeId }, transaction);
    }

    public async Task<IReadOnlyList<HierarchyNodeModel>> GetNodesAsync(HierarchyRoot root, CancellationToken cancellationToken = default)
    {
        var (connection, transaction) = await ReadContextAsync(cancellationToken);

        var rows = await connection.QueryAsync<HierarchyRow>(
            $"SELECT * FROM {QuotedTable()} WHERE {RootFilter()} ORDER BY {Col("Path")}",
            new { root.RootKind, root.OrganizationId }, transaction);

        return rows.Select(ToModel).ToArray();
    }

    public async Task<IReadOnlyList<string>> GetSubtreeUserIdsAsync(HierarchyRoot root, string userId, CancellationToken cancellationToken = default)
    {
        var (connection, transaction) = await ReadContextAsync(cancellationToken);

        var ownNodes = (await connection.QueryAsync<HierarchyRow>(
            $"SELECT * FROM {QuotedTable()} WHERE {Col("UserId")} = @UserId AND {RootFilter()}",
            new { UserId = userId, root.RootKind, root.OrganizationId }, transaction)).ToArray();

        if (ownNodes.Length == 0)
        {
            // Fail-closed expansion: no tree presence means no one under you - you still
            // see yourself, never everyone.
            return [userId];
        }

        var userIds = new HashSet<string>(StringComparer.Ordinal) { userId };
        foreach (var node in ownNodes)
        {
            var rows = await connection.QueryAsync<HierarchyRow>(
                $"SELECT * FROM {QuotedTable()} WHERE {Col("Path")} LIKE @PathPrefix AND {RootFilter()}",
                new { PathPrefix = node.Path + "%", root.RootKind, root.OrganizationId }, transaction);
            foreach (var row in rows)
            {
                userIds.Add(row.UserId);
            }
        }

        return userIds.ToArray();
    }

    public async Task<IReadOnlyList<string>> GetChainUserIdsAsync(HierarchyRoot root, string userId, CancellationToken cancellationToken = default)
    {
        var (connection, transaction) = await ReadContextAsync(cancellationToken);

        var node = (await connection.QueryAsync<HierarchyRow>(
            $"SELECT * FROM {QuotedTable()} WHERE {Col("UserId")} = @UserId AND {RootFilter()}",
            new { UserId = userId, root.RootKind, root.OrganizationId }, transaction)).FirstOrDefault();
        if (node is null)
        {
            return [];
        }

        var chainIds = HierarchyPathMath.Chain(node.Path);
        var rows = await connection.QueryAsync<HierarchyRow>(
            $"SELECT * FROM {QuotedTable()} WHERE {Col("Id")} IN @Ids",
            new { Ids = chainIds }, transaction);

        var byId = rows.ToDictionary(row => row.Id);
        return chainIds.Where(byId.ContainsKey).Select(id => byId[id].UserId).ToArray();
    }

    public async Task<IReadOnlyList<HierarchyNodeModel>> GetNodesForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var (connection, transaction) = await ReadContextAsync(cancellationToken);

        var rows = await connection.QueryAsync<HierarchyRow>(
            $"SELECT * FROM {QuotedTable()} WHERE {Col("UserId")} = @UserId",
            new { UserId = userId }, transaction);

        return rows.Select(ToModel).ToArray();
    }

    private static HierarchyNodeModel ToModel(HierarchyRow row)
        => new(row.Id, row.UserId, row.ParentId, row.RootKind, row.OrganizationId, row.Path, row.Position);

    // Dapper materialization row (public setters required).
    private sealed class HierarchyRow
    {
        public long Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public long? ParentId { get; set; }
        public string RootKind { get; set; } = string.Empty;
        public string? OrganizationId { get; set; }
        public string Path { get; set; } = string.Empty;
        public int Position { get; set; }
    }
}
