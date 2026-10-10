using Crest.Security.Permissions;

namespace Crest.ReCaptcha;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        ReCaptchaPermissions.ManageReCaptchaSettings,
    ];

    [Obsolete("This will be removed in a future release. Instead use 'ReCaptchaPermissions.ManageReCaptchaSettings'.")]
    public static readonly Permission ManageReCaptchaSettings = ReCaptchaPermissions.ManageReCaptchaSettings;
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
