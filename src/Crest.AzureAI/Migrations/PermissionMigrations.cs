using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Crest.AzureAI;
using Crest.Data.Migration;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Scope;
using Crest.Indexing;
using Crest.Indexing.Core;
using Crest.Modules;
using Crest.Security;
using Crest.Security.Permissions;
using Crest.Security.Services;

namespace Crest.Search.AzureAI.Migrations;

internal sealed class PermissionMigrations : DataMigration
{
    private const string PermissionNamePrefix = "QueryAzureAISearchIndex_";

    private readonly ShellSettings _shellSettings;

    public PermissionMigrations(ShellSettings shellSettings) =>
        _shellSettings = shellSettings;

    public int Create()
    {
        if (!_shellSettings.IsInitializing())
        {
            ShellScope.AddDeferredTask(ReplaceObsoletePermissionsAsync);
        }

        return 1;
    }

    /// <summary>
    /// Selects the roles that need to be updated, and replaces their <c>QueryAzureAISearchIndex_{0}</c> permissions
    /// with the equivalent <c>QueryIndex_{0}</c> permissions.
    /// </summary>
    private static async Task ReplaceObsoletePermissionsAsync(ShellScope shellScope)
    {
        var indexProfileManager = shellScope.ServiceProvider.GetRequiredService<IIndexProfileManager>();
        var roleService = shellScope.ServiceProvider.GetRequiredService<IRoleService>();
        var roleStore = shellScope.ServiceProvider.GetRequiredService<IRoleStore<IRole>>();

        var allRoles = await roleService.GetRolesAsync();
        var rolesToUpdate = allRoles
            .Where(role => role is Role)
            .Cast<Role>()
            .Where(role => role.RoleClaims.Any(IsAzureAISearchIndexPermissionClaim))
            .ToList();

        foreach (var role in rolesToUpdate)
        {
            foreach (var claim in role.RoleClaims.Where(IsAzureAISearchIndexPermissionClaim))
            {
                var name = GetIndexNameFromPermissionName(claim.ClaimValue);
                var indexProfile = await indexProfileManager.FindByNameAndProviderAsync(
                    name,
                    AzureAISearchConstants.ProviderName);

                if (indexProfile != null)
                {
                    claim.ClaimValue = IndexingPermissions.CreateDynamicPermission(indexProfile).Name;
                }
            }

            await roleStore.UpdateAsync(role, CancellationToken.None);
        }
    }

    private static bool IsAzureAISearchIndexPermissionClaim(RoleClaim claim) =>
        claim.ClaimType == nameof(Permission) &&
        claim.ClaimValue.StartsWithOrdinalIgnoreCase(PermissionNamePrefix);

    private static string GetIndexNameFromPermissionName(string permissionName) =>
        permissionName[PermissionNamePrefix.Length..];
}
