using Grove.Access.Members.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Migration;
using Grove.Environment.Shell.Scope;
using YesSql;
using YesSql.Sql;

namespace Grove.Access.Members.Migrations;

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
        await CreateOwnerIndexAsync();

        return 3;
    }

    /// <summary>
    /// The one permission model (docs/permissions.md): a binding's stored roles become the
    /// organization's groups, one per role, holding the members that had it. The bindings keep
    /// the class and the membership.
    /// </summary>
    public int UpdateFrom1()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var users = await scope.ServiceProvider.GetRequiredService<ISession>().Query<Grove.Access.Users.Models.User>().ListAsync();
            await scope.ServiceProvider.GetRequiredService<Services.MemberOrganizationGroups>().MigrateBindingRolesAsync(users);
        });

        return 2;
    }

    /// <summary>
    /// The organization owner (docs/permissions.md › The organization owner): the owner index,
    /// and an owner for every organization that has members, taken from its member admins
    /// (the stored data does not say which was bound first; one admin is chosen).
    /// </summary>
    public async Task<int> UpdateFrom2Async()
    {
        await CreateOwnerIndexAsync();
        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var ownership = services.GetRequiredService<Grove.Access.IOrganizationOwnership>();
            var admins = await services.GetRequiredService<ISession>()
                .QueryIndex<UserConnectionIndex>(index => index.Class == Grove.Access.Members.Constants.UserClasses.Member && index.IsMemberAdmin)
                .ListAsync();
            foreach (var organization in admins.GroupBy(row => row.OrganizationId))
            {
                await ownership.EnsureOwnerAsync(organization.Key, organization.OrderBy(row => row.Id).First().UserId);
            }
        });

        return 3;
    }

    private async Task CreateOwnerIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<Services.OrganizationOwnerIndex>(table => table
            .Column<string>(nameof(Services.OrganizationOwnerIndex.OrganizationId), column => column.WithLength(26))
            .Column<string>(nameof(Services.OrganizationOwnerIndex.OwnerUserId), column => column.WithLength(26)));
        await SchemaBuilder.AlterIndexTableAsync<Services.OrganizationOwnerIndex>(table => table
            .CreateIndex("IDX_OrganizationOwnerIndex_Organization", "DocumentId", "OrganizationId"));
    }

    // The member ROLE TEMPLATES are ordinary Grove roles the tenant shapes in the
    // role editor; they must EXIST for bindings to reference. Created idempotently,
    // deferred like every content/identity write at first-time setup.
    private static void DeferEnsureMemberRoleTemplates()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Grove.Security.IRole>>();
            foreach (var roleName in new[] { Grove.Access.Members.Constants.MemberRoleTemplates.Member, Grove.Access.Members.Constants.MemberRoleTemplates.MemberAdministrator })
            {
                if (await roleManager.FindByNameAsync(roleName) is null)
                {
                    await roleManager.CreateAsync(new Grove.Security.Role
                    {
                        RoleName = roleName,
                        RoleDescription = "Member role template - shape its permissions in the role editor.",
                    });
                }
            }
        });
    }

}
