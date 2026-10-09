using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;
using Crest.Security.Permissions;

namespace Crest.Users;

public sealed class CustomUserSettingsPermissions : IPermissionProvider
{
    // This permission is never checked it is only used as a template.
    private static readonly Permission s_manageOwnCustomUserSettings = new("ManageOwnCustomUserSettings_{0}", "Manage Own Custom User Settings - {0}", new[] { Permissions.ManageUsers });

    private readonly IContentDefinitionManager _contentDefinitionManager;

    public CustomUserSettingsPermissions(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
        => (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(x => x.GetStereotype() == "CustomUserSettings")
            .Select(CreatePermissionForType);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        => [];

    public static Permission CreatePermissionForType(ContentTypeDefinition type) =>
        new(
            string.Format(s_manageOwnCustomUserSettings.Name, type.Name),
            string.Format(s_manageOwnCustomUserSettings.Description, type.DisplayName),
            s_manageOwnCustomUserSettings.ImpliedBy
        );
}
