using Grove.Data;
using Grove.Data.Migration;
using Grove.Sharing.Indexes;
using Grove.Environment.Shell.Scope;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Sql;

namespace Grove.Sharing.Migrations;

public sealed class SharingMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ShareDocumentIndex>(table => table
            .Column<string>(nameof(ShareDocumentIndex.Kind), column => column.WithLength(64))
            .Column<string>(nameof(ShareDocumentIndex.ObjectId), column => column.WithLength(255)));

        await SchemaBuilder.AlterIndexTableAsync<ShareDocumentIndex>(table => table
            .CreateIndex("IDX_ShareDocumentIndex_Object", "DocumentId", nameof(ShareDocumentIndex.Kind), nameof(ShareDocumentIndex.ObjectId)));

        await SchemaBuilder.CreateMapIndexTableAsync<ShareEntryIndex>(table => table
            .Column<string>(nameof(ShareEntryIndex.Kind), column => column.WithLength(64))
            .Column<string>(nameof(ShareEntryIndex.ObjectId), column => column.WithLength(255))
            .Column<int>(nameof(ShareEntryIndex.PrincipalKind))
            .Column<string>(nameof(ShareEntryIndex.PrincipalId), column => column.WithLength(255))
            .Column<bool>(nameof(ShareEntryIndex.IsBlock)));

        await SchemaBuilder.AlterIndexTableAsync<ShareEntryIndex>(table => table
            .CreateIndex("IDX_ShareEntryIndex_Principal", "DocumentId", nameof(ShareEntryIndex.PrincipalKind), nameof(ShareEntryIndex.PrincipalId), nameof(ShareEntryIndex.IsBlock)));

        return await UpdateFrom1Async();
    }

    /// <summary>The access index, its role shadows, the per-object registry and the background
    /// recalculations (phase 2).</summary>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ShareAccessIndex>(table => table
            .Column<string>(nameof(ShareAccessIndex.Kind), column => column.WithLength(64))
            .Column<string>(nameof(ShareAccessIndex.ObjectId), column => column.WithLength(255))
            .Column<int>(nameof(ShareAccessIndex.PrincipalKind))
            .Column<string>(nameof(ShareAccessIndex.PrincipalId), column => column.WithLength(255))
            .Column<int>(nameof(ShareAccessIndex.Depth))
            .Column<int>(nameof(ShareAccessIndex.Level))
            .Column<bool>(nameof(ShareAccessIndex.IsBlock))
            .Column<bool>(nameof(ShareAccessIndex.IsDirect))
            .Column<DateTime>(nameof(ShareAccessIndex.StartsUtc), column => column.Nullable())
            .Column<DateTime>(nameof(ShareAccessIndex.EndsUtc), column => column.Nullable())
            .Column<string>(nameof(ShareAccessIndex.DepthKey), column => column.WithLength(DepthKeyLength)));

        // The list scopes select object ids by kind and principal.
        await SchemaBuilder.AlterIndexTableAsync<ShareAccessIndex>(table => table
            .CreateIndex("IDX_ShareAccessIndex_Principal", nameof(ShareAccessIndex.Kind), nameof(ShareAccessIndex.PrincipalKind), nameof(ShareAccessIndex.PrincipalId), nameof(ShareAccessIndex.ObjectId)));

        await SchemaBuilder.CreateMapIndexTableAsync<ShareRoleShadowIndex>(table => table
            .Column<string>(nameof(ShareRoleShadowIndex.Kind), column => column.WithLength(64))
            .Column<string>(nameof(ShareRoleShadowIndex.ObjectId), column => column.WithLength(255))
            .Column<string>(nameof(ShareRoleShadowIndex.Role), column => column.WithLength(255))
            .Column<string>(nameof(ShareRoleShadowIndex.DepthKey), column => column.WithLength(DepthKeyLength))
            .Column<int>(nameof(ShareRoleShadowIndex.Level))
            .Column<DateTime>(nameof(ShareRoleShadowIndex.StartsUtc), column => column.Nullable())
            .Column<DateTime>(nameof(ShareRoleShadowIndex.EndsUtc), column => column.Nullable()));

        await SchemaBuilder.AlterIndexTableAsync<ShareRoleShadowIndex>(table => table
            .CreateIndex("IDX_ShareRoleShadowIndex_Role", nameof(ShareRoleShadowIndex.Kind), nameof(ShareRoleShadowIndex.Role)));

        await SchemaBuilder.CreateMapIndexTableAsync<ShareAccessObjectIndex>(table => table
            .Column<string>(nameof(ShareAccessObjectIndex.Kind), column => column.WithLength(64))
            .Column<string>(nameof(ShareAccessObjectIndex.ObjectId), column => column.WithLength(255))
            .Column<bool>(nameof(ShareAccessObjectIndex.IsPublic))
            .Column<DateTime>(nameof(ShareAccessObjectIndex.NextBoundaryUtc), column => column.Nullable()));

        await SchemaBuilder.AlterIndexTableAsync<ShareAccessObjectIndex>(table => table
            .CreateIndex("IDX_ShareAccessObjectIndex_Object", "DocumentId", nameof(ShareAccessObjectIndex.Kind), nameof(ShareAccessObjectIndex.ObjectId)));

        // The time registry: the job reads only the rows now due.
        await SchemaBuilder.AlterIndexTableAsync<ShareAccessObjectIndex>(table => table
            .CreateIndex("IDX_ShareAccessObjectIndex_Due", "DocumentId", nameof(ShareAccessObjectIndex.NextBoundaryUtc)));

        await SchemaBuilder.CreateMapIndexTableAsync<ShareRecalculationIndex>(table => table
            .Column<string>(nameof(ShareRecalculationIndex.Kind), column => column.WithLength(64))
            .Column<string>(nameof(ShareRecalculationIndex.ObjectId), column => column.WithLength(255))
            .Column<DateTime>(nameof(ShareRecalculationIndex.RequestedUtc)));

        return await UpdateFrom2Async();
    }

    /// <summary>
    /// Multi-path inheritance (F3): an object reached through several paths has an access
    /// document per path, and the list scopes correlate rows by <c>AccessKey</c> (the object id
    /// for the primary path). Existing rows are all primary paths, so their key is their object id.
    /// </summary>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ShareAccessIndex>(table => table
            .AddColumn<string>(nameof(ShareAccessIndex.AccessKey), column => column.WithLength(AccessKeyLength)));
        await SchemaBuilder.AlterIndexTableAsync<ShareAccessIndex>(table => table
            .CreateIndex("IDX_ShareAccessIndex_AccessKey", nameof(ShareAccessIndex.Kind), nameof(ShareAccessIndex.PrincipalKind), nameof(ShareAccessIndex.PrincipalId), nameof(ShareAccessIndex.AccessKey)));

        await SchemaBuilder.AlterIndexTableAsync<ShareAccessObjectIndex>(table => table
            .AddColumn<string>(nameof(ShareAccessObjectIndex.AccessKey), column => column.WithLength(AccessKeyLength)));
        await SchemaBuilder.AlterIndexTableAsync<ShareAccessObjectIndex>(table => table
            .CreateIndex("IDX_ShareAccessObjectIndex_AccessKey", nameof(ShareAccessObjectIndex.Kind), nameof(ShareAccessObjectIndex.AccessKey), nameof(ShareAccessObjectIndex.ObjectId)));

        // The rows already there stand for primary paths, and their depth keys stay as they are.
        ShellScope.AddDeferredTask(async scope =>
        {
            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var dialect = session.Store.Configuration.SqlDialect;
            var prefix = session.Store.Configuration.TablePrefix;
            await using var connection = scope.ServiceProvider.GetRequiredService<IDbConnectionAccessor>().CreateConnection();
            await connection.OpenAsync();
            foreach (var table in new[] { nameof(ShareAccessIndex), nameof(ShareAccessObjectIndex) })
            {
                var quoted = dialect.QuoteForTableName(prefix + table, session.Store.Configuration.Schema);
                await connection.ExecuteAsync(
                    $"UPDATE {quoted} SET {dialect.QuoteForColumnName(nameof(ShareAccessIndex.AccessKey))} = {dialect.QuoteForColumnName(nameof(ShareAccessIndex.ObjectId))} WHERE {dialect.QuoteForColumnName(nameof(ShareAccessIndex.AccessKey))} IS NULL");
            }
        });

        return await UpdateFrom3Async();
    }

    /// <summary>
    /// The access indicator (requirement 98): whether an object is shared with someone other than
    /// its owner, beside the public flag, so lists filter by it in the query. Existing documents
    /// get it when recalculated: a rebuild is queued.
    /// </summary>
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ShareAccessObjectIndex>(table => table
            .AddColumn<bool>(nameof(ShareAccessObjectIndex.IsShared), column => column.WithDefault(false)));

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IShareAccessIndex>().QueueRebuildAsync());

        return 4;
    }

    /// <summary>An object id (255), '~' and a path key.</summary>
    public const int AccessKeyLength = 280;

    /// <summary>A kind (64), a separator, an access key (an object id and a path key), '#' and a depth.</summary>
    public const int DepthKeyLength = 360;
}
