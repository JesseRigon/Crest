using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Security.Permissions;

namespace Grove.Contents.Security;

public sealed class ContentTypePermissions : IPermissionProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ContentTypePermissions(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        // Manage rights only for Securable types.
        var securableTypes = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(ctd => ctd.IsSecurable());

        var result = new List<Permission>();

        foreach (var typeDefinition in securableTypes)
        {
            foreach (var permissionTemplate in ContentTypePermissionsHelper.PermissionTemplates.Values)
            {
                result.Add(ContentTypePermissionsHelper.CreateDynamicPermission(permissionTemplate, typeDefinition));
            }
        }

        return result;
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        => [];
}
