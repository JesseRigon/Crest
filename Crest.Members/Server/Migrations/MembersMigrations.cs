using Crest.Members.Indexes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql.Sql;

namespace Crest.Members.Migrations;

/// <summary>
/// The Members feature's schema: the user-class index, the per-tenant user hierarchy
/// table and org bindings. The commercial half (subscriptions, seats, groups,
/// entitlements) has its own module and its own migration, so a tenant that enables
/// Members alone never gets that schema.
/// </summary>
public sealed class MembersMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<UserClassIndex>(table => table
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("Class", column => column.WithLength(16)));

        await SchemaBuilder.AlterIndexTableAsync<UserClassIndex>(table => table
            .CreateIndex("IDX_UserClassIndex_Class", "DocumentId", "Class", "UserId"));

        // The per-tenant user hierarchy: a plain custom table (NOT a YesSql index -
        // subtree queries are materialized-path prefix matches the query layer cannot
        // express). One shared forest: RootKind 'staff' (OrganizationId null) or 'org'.
        await SchemaBuilder.CreateTableAsync(Services.UserHierarchyService.TableName, table => table
            .Column<long>("Id", column => column.PrimaryKey().Identity())
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<long>("ParentId", column => column.Nullable())
            .Column<string>("RootKind", column => column.WithLength(8))
            .Column<string>("OrganizationId", column => column.WithLength(26))
            .Column<string>("Path", column => column.WithLength(1024))
            .Column<int>("Position"));

        await SchemaBuilder.AlterTableAsync(Services.UserHierarchyService.TableName, table =>
        {
            table.CreateIndex($"IDX_{Services.UserHierarchyService.TableName}_Root_Path", "RootKind", "OrganizationId", "Path");
            table.CreateIndex($"IDX_{Services.UserHierarchyService.TableName}_UserId", "UserId");
        });

        await SchemaBuilder.CreateMapIndexTableAsync<Indexes.MemberOrgBindingIndex>(table => table
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("OrganizationId", column => column.WithLength(26))
            .Column<bool>("IsMemberAdmin"));

        await SchemaBuilder.AlterIndexTableAsync<Indexes.MemberOrgBindingIndex>(table => table
            .CreateIndex("IDX_MemberOrgBindingIndex_Org", "DocumentId", "OrganizationId", "UserId"));

        DeferEnsureMemberRoleTemplates();

        return 1;
    }

    // The member ROLE TEMPLATES are ordinary Orchard roles the tenant shapes in the
    // role editor; they must EXIST for bindings to reference. Created idempotently,
    // deferred like every content/identity write at first-time setup.
    private static void DeferEnsureMemberRoleTemplates()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<OrchardCore.Security.IRole>>();
            foreach (var roleName in new[] { Crest.Members.Constants.MemberRoleTemplates.Member, Crest.Members.Constants.MemberRoleTemplates.MemberAdministrator })
            {
                if (await roleManager.FindByNameAsync(roleName) is null)
                {
                    await roleManager.CreateAsync(new OrchardCore.Security.Role
                    {
                        RoleName = roleName,
                        RoleDescription = "Member role template - shape its permissions in the role editor.",
                    });
                }
            }
        });
    }

}
