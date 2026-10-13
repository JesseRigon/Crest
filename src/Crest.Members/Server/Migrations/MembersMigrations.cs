using Crest.Members.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Crest.Data.Migration;
using Crest.Environment.Shell.Scope;
using YesSql.Sql;

namespace Crest.Members.Migrations;

/// <summary>
/// The Members feature's schema: the connection index (one row per binding) and the
/// closure-table hierarchy. The commercial half (subscriptions, seats, groups,
/// entitlements) has its own module and its own migration, so a tenant that enables
/// Members alone never gets that schema.
/// </summary>
public sealed class MembersMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        // A user's connections, one row per (user, organization, class).
        await SchemaBuilder.CreateMapIndexTableAsync<UserConnectionIndex>(table => table
            .Column<string>(nameof(UserConnectionIndex.UserId), column => column.WithLength(26))
            .Column<string>(nameof(UserConnectionIndex.OrganizationId), column => column.WithLength(26))
            .Column<string>(nameof(UserConnectionIndex.Class), column => column.WithLength(16))
            .Column<bool>(nameof(UserConnectionIndex.IsMemberAdmin)));

        await SchemaBuilder.AlterIndexTableAsync<UserConnectionIndex>(table => table
            .CreateIndex("IDX_UserConnectionIndex_Organization", "DocumentId", "OrganizationId", "Class", "UserId"));
        await SchemaBuilder.AlterIndexTableAsync<UserConnectionIndex>(table => table
            .CreateIndex("IDX_UserConnectionIndex_User", "DocumentId", "UserId", "Class"));

        // The reports-to hierarchy as a closure table on YesSql (ruling 2026-10-06): a
        // document per (user, organization), one index row per node, one per ancestor.
        await SchemaBuilder.CreateMapIndexTableAsync<UserHierarchyIndex>(table => table
            .Column<string>(nameof(UserHierarchyIndex.UserId), column => column.WithLength(26))
            .Column<string>(nameof(UserHierarchyIndex.OrganizationId), column => column.WithLength(26)));
        await SchemaBuilder.AlterIndexTableAsync<UserHierarchyIndex>(table => table
            .CreateIndex("IDX_UserHierarchyIndex_Organization", "DocumentId", "OrganizationId", "UserId"));

        await SchemaBuilder.CreateMapIndexTableAsync<UserHierarchyClosureIndex>(table => table
            .Column<string>(nameof(UserHierarchyClosureIndex.AncestorId), column => column.WithLength(26))
            .Column<string>(nameof(UserHierarchyClosureIndex.DescendantId), column => column.WithLength(26))
            .Column<string>(nameof(UserHierarchyClosureIndex.OrganizationId), column => column.WithLength(26)));
        await SchemaBuilder.AlterIndexTableAsync<UserHierarchyClosureIndex>(table => table
            .CreateIndex("IDX_UserHierarchyClosure_Ancestor", "DocumentId", "OrganizationId", "AncestorId"));
        await SchemaBuilder.AlterIndexTableAsync<UserHierarchyClosureIndex>(table => table
            .CreateIndex("IDX_UserHierarchyClosure_Descendant", "DocumentId", "OrganizationId", "DescendantId"));

        DeferEnsureMemberRoleTemplates();

        return 1;
    }

    // The member ROLE TEMPLATES are ordinary Crest roles the tenant shapes in the
    // role editor; they must EXIST for bindings to reference. Created idempotently,
    // deferred like every content/identity write at first-time setup.
    private static void DeferEnsureMemberRoleTemplates()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Crest.Security.IRole>>();
            foreach (var roleName in new[] { Crest.Members.Constants.MemberRoleTemplates.Member, Crest.Members.Constants.MemberRoleTemplates.MemberAdministrator })
            {
                if (await roleManager.FindByNameAsync(roleName) is null)
                {
                    await roleManager.CreateAsync(new Crest.Security.Role
                    {
                        RoleName = roleName,
                        RoleDescription = "Member role template - shape its permissions in the role editor.",
                    });
                }
            }
        });
    }

}
