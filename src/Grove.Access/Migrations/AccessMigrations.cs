using Grove.Access.Store;
using Grove.Environment.Shell.Scope;
using Grove.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Migration;
using YesSql.Sql;

namespace Grove.Access.Migrations;

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

        EnsureManagementSets();
        return 2;
    }

    /// <summary>The management sets (docs/permissions.md › Who manages the permission model),
    /// created from their defaults and held by the Administrator role.</summary>
    public int UpdateFrom1()
    {
        EnsureManagementSets();
        return 2;
    }

    private static void EnsureManagementSets()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IPermissionSetService>().EnsureDefaultsAsync();
            var roles = services.GetRequiredService<RoleManager<IRole>>();
            if (await roles.FindByNameAsync(PlatformConstants.Roles.Administrator) is Role administrator)
            {
                var missing = new[] { AccessPermissions.ManagePermissionsSet, AccessPermissions.ManageGroupsSet }
                    .Where(set => !administrator.PermissionSets.Contains(set, StringComparer.OrdinalIgnoreCase))
                    .ToArray();
                if (missing.Length > 0)
                {
                    administrator.PermissionSets.AddRange(missing);
                    await roles.UpdateAsync(administrator);
                }
            }
        });
    }
}
