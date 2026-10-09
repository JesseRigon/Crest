using System.Globalization;
using Crest.Localization;
using Crest.Localization.Data;
using Crest.Security.Permissions;

namespace Crest.DataLocalization;

/// <summary>
/// Provides permissions for the Data Localization module.
/// </summary>
public sealed class Permissions : IPermissionProvider
{
    private readonly ILocalizationService _localizationService;

    public Permissions(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>
        {
            DataLocalizationPermissions.ViewDynamicTranslations,
            DataLocalizationPermissions.ManageTranslations,
            DataLocalizationPermissions.ManageLocalization,
        };

        var supportedCultures = await _localizationService.GetSupportedCulturesAsync();

        foreach (var cultureName in supportedCultures)
        {
            var cultureInfo = CultureInfo.GetCultureInfo(cultureName);
            var displayName = !string.IsNullOrEmpty(cultureInfo.DisplayName)
                ? cultureInfo.DisplayName
                : cultureInfo.NativeName;

            permissions.Add(DataLocalizationPermissions.CreateCulturePermission(cultureName, displayName));
        }

        return permissions;
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Administrator,
            Permissions = [DataLocalizationPermissions.ManageTranslations],
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Editor,
            Permissions = [DataLocalizationPermissions.ViewDynamicTranslations],
        },
    ];
}
