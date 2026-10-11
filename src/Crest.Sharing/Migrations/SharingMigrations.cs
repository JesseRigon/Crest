using Crest.Data.Migration;
using Crest.Sharing.Indexes;
using YesSql.Sql;

namespace Crest.Sharing.Migrations;

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

        return 2;
    }

    /// <summary>A kind (64), a separator, an object id (255), '#' and a depth.</summary>
    public const int DepthKeyLength = 330;
}
