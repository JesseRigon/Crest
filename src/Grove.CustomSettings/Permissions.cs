using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Models;
using Grove.Security.Permissions;

namespace Grove.CustomSettings;

public sealed class Permissions : IPermissionProvider
{
    private static readonly Permission s_manageCustomSettings = new("ManageCustomSettings_{0}", "Manage Custom Settings - {0}", new[] { new Permission("ManageResourceSettings") });

    // Reads the definitions directly, not through CustomSettingsService: that service needs
    // IAuthorizationService, whose handler needs IPermissionService, which enumerates every
    // IPermissionProvider - including this one (a DI cycle).
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Permissions(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var list = new List<Permission>();

        foreach (var type in (await _contentDefinitionManager.ListTypeDefinitionsAsync()).Where(type => type.StereotypeEquals(CustomSettingsConstants.Stereotype)))
        {
            list.Add(CreatePermissionForType(type));
        }

        return list;
    }

    public static string CreatePermissionName(string name)
        => string.Format(s_manageCustomSettings.Name, name);

    public static Permission CreatePermissionForType(ContentTypeDefinition type)
    {
        return new Permission(
                CreatePermissionName(type.Name),
                string.Format(s_manageCustomSettings.Description, type.DisplayName),
                s_manageCustomSettings.ImpliedBy
            );
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        => [];
}
