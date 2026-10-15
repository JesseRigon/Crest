using Grove.Security.Permissions;

namespace Grove.Sms;

public static class SmsPermissions
{
    public static readonly Permission ManageSmsSettings = new("ManageSmsSettings", "Manage SMS Settings");
}
