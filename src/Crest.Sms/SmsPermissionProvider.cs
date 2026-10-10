using Crest.Security.Permissions;

namespace Crest.Sms;

public sealed class SmsPermissionProvider : IPermissionProvider
{
    [Obsolete("This should not be used. Instead use SmsPermissions.ManageSmsSettings")]
    public static readonly Permission ManageSmsSettings = SmsPermissions.ManageSmsSettings;

    private readonly IEnumerable<Permission> _allPermissions =
    [
        SmsPermissions.ManageSmsSettings,
    ];

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
