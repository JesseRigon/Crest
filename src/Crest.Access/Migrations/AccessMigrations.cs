using Crest.Access.Store;
using Crest.Data.Migration;
using YesSql.Sql;

namespace Crest.Access.Migrations;

/// <summary>The access store's schema: groups and their members, per-user overrides, the projection.</summary>
public sealed class AccessMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AccessGroupIndex>(table => table
            .Column<string>(nameof(AccessGroupIndex.GroupId), column => column.WithLength(26))
            .Column<string>(nameof(AccessGroupIndex.Name), column => column.WithLength(256))
            .Column<string>(nameof(AccessGroupIndex.OrganizationId), column => column.WithLength(26))
            .Column<string>(nameof(AccessGroupIndex.TemplateId), column => column.WithLength(26)));
        await SchemaBuilder.AlterIndexTableAsync<AccessGroupIndex>(table => table
            .CreateIndex("IDX_AccessGroupIndex_Reach", "DocumentId", "OrganizationId", "GroupId"));

        await SchemaBuilder.CreateMapIndexTableAsync<AccessGroupMemberIndex>(table => table
            .Column<string>(nameof(AccessGroupMemberIndex.GroupId), column => column.WithLength(26))
            .Column<string>(nameof(AccessGroupMemberIndex.UserId), column => column.WithLength(26))
            .Column<string>(nameof(AccessGroupMemberIndex.OrganizationId), column => column.WithLength(26)));
        await SchemaBuilder.AlterIndexTableAsync<AccessGroupMemberIndex>(table => table
            .CreateIndex("IDX_AccessGroupMemberIndex_User", "DocumentId", "UserId", "OrganizationId"));

        await SchemaBuilder.CreateMapIndexTableAsync<UserAccessOverrideIndex>(table => table
            .Column<string>(nameof(UserAccessOverrideIndex.UserId), column => column.WithLength(26))
            .Column<string>(nameof(UserAccessOverrideIndex.OrganizationId), column => column.WithLength(26)));
        await SchemaBuilder.AlterIndexTableAsync<UserAccessOverrideIndex>(table => table
            .CreateIndex("IDX_UserAccessOverrideIndex_User", "DocumentId", "UserId", "OrganizationId"));

        await SchemaBuilder.CreateMapIndexTableAsync<UserAccessIndex>(table => table
            .Column<string>(nameof(UserAccessIndex.UserId), column => column.WithLength(26))
            .Column<string>(nameof(UserAccessIndex.OrganizationId), column => column.WithLength(26))
            .Column<string>(nameof(UserAccessIndex.Kind), column => column.WithLength(16))
            .Column<string>(nameof(UserAccessIndex.Name), column => column.WithLength(256))
            .Column<string>(nameof(UserAccessIndex.Source), column => column.WithLength(512))
            .Column<long>(nameof(UserAccessIndex.PermissionVersion)));
        await SchemaBuilder.AlterIndexTableAsync<UserAccessIndex>(table => table
            .CreateIndex("IDX_UserAccessIndex_User", "DocumentId", "UserId", "OrganizationId", "Kind"));

        return 1;
    }
}
